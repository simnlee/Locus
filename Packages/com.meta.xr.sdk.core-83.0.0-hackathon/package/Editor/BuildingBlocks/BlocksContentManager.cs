/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meta.XR.Editor.Callbacks;
using Meta.XR.Editor.RemoteContent;
using Meta.XR.Editor.Tags;
using Meta.XR.Editor.UserInterface;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.BuildingBlocks.Editor
{
    [InitializeOnLoad]
    internal static class BlocksContentManager
    {
        public static event Action OnContentChanged;

        private const double CacheDurationInHours = 6;
        private const string CommonTag = "Common";

        private const string DownloadPath = "https://www.facebook.com/building-blocks-content";

        private static readonly RemoteJsonContentDownloader Downloader;

        private static BlockData[] _contentFilter;
        private static readonly Dictionary<string, int> BlockOrderDictionary = new();

        private static Dictionary<string, BlockModifiableProperty[]> _blockModifiableProperties;

        private static Dictionary<Tag, BlockUrl[]> _documentationsData;

        internal static readonly HashSet<Tag> RemoteCollectionTags = new();
        private static readonly Dictionary<Tag, List<Editor.BlockData>> RemoteCollections = new();

        static BlocksContentManager()
        {
            Downloader = new RemoteJsonContentDownloader("bb_content.json", DownloadPath)
                .WithCacheDuration(TimeSpan.FromHours(CacheDurationInHours))
                .WithMachineIdUrlParameter()
                .WithSDKVersionUrlParameter()
                .WithCachePerSDKVersion();

            InitializeOnLoad.Register(Initialize);
        }

        private static void Initialize()
        {
#pragma warning disable CS4014
            InitializeAsync();
#pragma warning restore CS4014
        }

        private static async Task InitializeAsync()
        {
            var successfulLoad = await Reload(false);
            if (!successfulLoad)
            {
                await Reload(true);
            }
        }

        public static async Task<bool> Reload(bool forceRedownload)
        {
            if (forceRedownload)
            {
                Downloader.ClearCache();
            }

            var result = await Downloader.Fetch();
            return result.IsSuccess && LoadContentJsonData(result.Content);
        }

        public static BlockModifiableProperty[] GetBlockModifiablePropertyById(string blockId)
        {
            return _blockModifiableProperties == null
                ? Array.Empty<BlockModifiableProperty>()
                : _blockModifiableProperties.GetValueOrDefault(blockId, Array.Empty<BlockModifiableProperty>());
        }

        internal static BlockUrl[] GetBlockUrls(Tag tag)
        {
            if (_documentationsData == null)
                return Array.Empty<BlockUrl>();

            return _documentationsData.TryGetValue(tag, out var urls) ? urls : Array.Empty<BlockUrl>();
        }

        // For common / generic docs related to building blocks.
        internal static BlockUrl[] GetCommonDocs() => GetBlockUrls(CommonTag);


        #region Data Parsing

        // ReSharper disable InconsistentNaming
        [Serializable]
        internal struct BlockData
        {
            public string id;
            public string blockName;
            public string description;
            public ulong thumbnailContentId;
            public string[] tags;
            public BlockModifiableProperty[] modifiableProperties;
        }

        [Serializable]
        internal struct BlockModifiableProperty
        {
            public string highlightIdentifier;
            public string name;
            public string description;

        }

        [Serializable]
        internal struct BlockDocumentation
        {
            public string tag;
            public BlockUrl[] urls;
        }

        [Serializable]
        internal struct BlockUrl
        {
            public string title;
            public string url;
        }

        [Serializable]
        internal struct BlockDataResponse
        {
            public BlockData[] content;
            public BlockDocumentation[] docs;
            public TagsData tags;
        }

        [Serializable]
        internal struct TagsData
        {
            public CollectionTag[] collections;
            public FeatureTag[] features;
        }

        [Serializable]
        internal struct CollectionTag
        {
            public string tag;
            public string description;
            public ulong thumbnailContentId;
            public BlockData[] blocks;
        }

        [Serializable]
        internal struct FeatureTag
        {
            public string tag;
        }
        // ReSharper restore InconsistentNaming

        internal static BlockDataResponse ParseJsonData(string jsonData)
        {
            BlockDataResponse response;
            try
            {
                response = JsonUtility.FromJson<BlockDataResponse>(jsonData);
            }
            catch (Exception)
            {
                response = default;
            }

            response.content ??= Array.Empty<BlockData>();
            response.docs ??= Array.Empty<BlockDocumentation>();
            response.tags.collections ??= Array.Empty<CollectionTag>();
            response.tags.features ??= Array.Empty<FeatureTag>();

            return response;
        }

        #endregion

        #region Blocks Content

        internal static bool LoadContentJsonData(string jsonData)
        {
            var response = ParseJsonData(jsonData);

            _contentFilter = response.content;

            ClearBlockOrder();
            for (var i = 0; i < response.content.Length; i++)
            {
                SetBlockOrder(response.content[i].id, i);
            }

            ParseModifiableProperties(response);

            ParseDocumentations(response);

            ParseTagsData(response);

            var success = _contentFilter is { Length: > 0 };
            OnContentChanged?.Invoke();

            if (success)
            {
                PreloadRemoteTextures(response);
            }

            return success;
        }

        private static void PreloadRemoteTextures(BlockDataResponse response)
        {
            var blockContentIds = response.content.Select(block => block.thumbnailContentId);
            var collectionContentIds =
                response.tags.collections.Select(collection => collection.thumbnailContentId);
            _ = RemoteTextureContent.PreloadRemoteTextures(blockContentIds.Concat(collectionContentIds));
        }

        private static void ParseModifiableProperties(BlockDataResponse response)
        {
            if (response.content is { Length: > 0 })
            {
                _blockModifiableProperties = response.content
                    .ToDictionary(item => item.id, item => item.modifiableProperties);
            }
        }

        private static void ParseDocumentations(BlockDataResponse response)
        {
            _documentationsData = new Dictionary<Tag, BlockUrl[]>();
            foreach (var doc in response.docs)
            {
                _documentationsData[doc.tag] = doc.urls;
            }
        }

        private static void ParseTagsData(BlockDataResponse response)
        {
            RemoteCollections.Clear();
            RemoteCollectionTags.Clear();

            foreach (var collection in response.tags.collections)
            {
                var tag = new Tag(collection.tag);

                if (!CustomTagBehaviors.CollectionTags.Contains(tag))
                {
                    // Generate the tag, and its behavior if it is not a preexisting one
                    CustomTagBehaviors.SetDefaultCollectionTagBehavior(tag);
                }

                tag.OnValidate();

                // If the tag has not been properly setup with a behavior, ignore it
                if (tag.Behavior is not CollectionTagBehavior behavior)
                    continue;

                // Update with remote overrides, optional
                if (!string.IsNullOrEmpty(collection.description))
                {
                    behavior.Description = collection.description;
                }
                if (collection.thumbnailContentId != 0)
                {
                    behavior.RemoteThumbnailContentId = collection.thumbnailContentId;
                }

                RemoteCollectionTags.Add(tag);
                RemoteCollections.TryAdd(tag, new());

                foreach (var block in collection.blocks ?? Array.Empty<BlockData>())
                {
                    var blockData = Utils.GetBlockData(block.id);
                    if (blockData != null &&
                        !RemoteCollections[tag].Contains(blockData) &&
                        _contentFilter.Any(b => b.id == block.id))
                    {
                        RemoteCollections[tag].Add(blockData);
                    }
                }
            }
        }

        public static IReadOnlyList<BlockBaseData> FilterBlockWindowContent(IReadOnlyList<BlockBaseData> content)
        {
            return FilterBlockWindowContent(content, _contentFilter);
        }

        private static void ClearBlocksOverrides(IEnumerable<BlockBaseData> content)
        {
            foreach (var block in content)
            {
                block.BlockName.RemoveOverride();
                block.Description.RemoveOverride();
                block.OverridableTags.RemoveOverride();
                block.RemoteThumbnailContentId.RemoveOverride();
            }
        }

        public static int GetBlockOrder(BlockBaseData blockBaseData)
        {
            return BlockOrderDictionary.GetValueOrDefault(blockBaseData.Id, int.MaxValue);
        }

        internal static void SetBlockOrder(string blockId, int order)
        {
            BlockOrderDictionary[blockId] = order;
        }

        internal static void ClearBlockOrder()
        {
            BlockOrderDictionary.Clear();
        }

        internal static IReadOnlyList<BlockBaseData> FilterBlockWindowContent(IReadOnlyList<BlockBaseData> content,
            BlockData[] contentFilter)
        {
            if (contentFilter == null || contentFilter.Length == 0)
            {
                ClearBlocksOverrides(content);
                return content;
            }

            var contentFilterDictionary = contentFilter
                .Select((value, index) => new { value, index })
                .ToDictionary(pair => pair.value.id, pair => new { pair.value, pair.index });

            var filteredContent = content
                .Where(block => contentFilterDictionary.ContainsKey(block.Id))
                .OrderBy(block => contentFilterDictionary[block.Id].index)
                .ToList();

            foreach (var blockBaseData in filteredContent)
            {
                blockBaseData.BlockName.SetOverride(contentFilterDictionary[blockBaseData.Id].value.blockName);
                blockBaseData.Description.SetOverride(contentFilterDictionary[blockBaseData.Id].value.description);
                var thumbnailContentId = contentFilterDictionary[blockBaseData.Id].value.thumbnailContentId;
                if (thumbnailContentId != 0)
                {
                    blockBaseData.RemoteThumbnailContentId.SetOverride(thumbnailContentId);
                }
                blockBaseData.OverridableTags.SetOverride(
                    GenerateTagArrayFromTags(contentFilterDictionary[blockBaseData.Id].value.tags));
            }

            return filteredContent;
        }

        private static TagArray GenerateTagArrayFromTags(string[] tags)
        {
            if (tags == null)
            {
                return null;
            }

            var tagArray = new TagArray();
            tagArray.Add(tags.Select(tag => new Tag(tag)));
            return tagArray;
        }


        public static IReadOnlyCollection<BlockBaseData> GetCollection(CollectionTagBehavior collection)
        {
            return collection == null ? null : RemoteCollections.GetValueOrDefault(collection.Tag, null);
        }

        #endregion
    }
}
