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
using System.IO;
using Meta.XR.Editor.Id;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Editor.UserInterface
{
    [Serializable]
    internal class TextureContent
    {
        [Serializable]
        public class Category : IIdentified
        {
            [SerializeField]
            private string _pathKey;

            [SerializeField]
            private string _path;

            [SerializeField]
            private bool _isFullPath;

            public string Id => _path;

            private string _fullPath;

            public bool TryGetFullPath(out string fullPath)
            {
                if (_fullPath == null)
                {
                    if (TryGetRootPath(_pathKey, out var rootPath))
                    {
                        _fullPath = Path.Combine(rootPath, _path);
                    }
                }

                fullPath = _fullPath;
                return fullPath != null;
            }

            public Category(string path, bool isFullPath = false, string pathKey = "Oculus.VR.Editor")
            {
                _path = path;
                _isFullPath = isFullPath;
                _pathKey = pathKey;

                if (_isFullPath)
                {
                    _fullPath = path;
                }
            }
        }

        public static class Categories
        {
            public static readonly Category BuiltIn = new(null);
            public static readonly Category Generic = new("Icons");
        }

        public static TextureContent CreateContent(string name, TextureContent.Category category, string tooltip = null)
        {
            return new TextureContent(name, category, tooltip);
        }

        private static readonly Dictionary<object, string> RootPaths = new();

        private static bool TryGetRootPath(string key, out string rootPath)
        {
            if (!RootPaths.TryGetValue(key, out rootPath))
            {
                var g = AssetDatabase.FindAssets($"t:AssemblyDefinitionAsset {key}");
                if (g.Length > 0)
                {
                    rootPath = AssetDatabase.GUIDToAssetPath(g[0]);
                    rootPath = Path.GetDirectoryName(rootPath);
                }

                RootPaths[key] = rootPath;
            }

            return rootPath != null;
        }

        public static bool BuildPath(string path, Category category, out string contentPath)
        {
            contentPath = null;
            if (category.TryGetFullPath(out var fullPath))
            {
                contentPath = Path.Combine(fullPath, path);
            }

            return contentPath != null;
        }

        public static implicit operator GUIContent(TextureContent ovrTextureContent)
        {
            return ovrTextureContent.GUIContent;
        }

        private GUIContent _content;
        public virtual GUIContent Content => _content ??= new GUIContent
        {
            image = null,
            tooltip = _tooltip
        };

        [SerializeField]
        private string _name;

        [SerializeField]
        private Category _category;

        [SerializeField]
        private string _tooltip;

        public string Name => _name;

        public string Tooltip
        {
            set
            {
                _tooltip = value;
                Content.tooltip = value;
            }
            get => _tooltip;
        }

        protected TextureContent(string name, Category category, string tooltip = null)
        {
            _name = name;
            _tooltip = tooltip;
            _category = category;
        }

        public virtual GUIContent GUIContent
        {
            get
            {
                if (Content.image == null)
                {
                    LoadContent();
                }

                return Content;
            }
        }

        public virtual bool Valid => _name != null && GUIContent.image != null;
        public virtual Texture Image => GUIContent.image;

        private void LoadContent()
        {
            if (!Utils.ShouldRenderEditorUI()) return;

            if (_category == Categories.BuiltIn)
            {
                _content = EditorGUIUtility.TrIconContent(_name, _tooltip);
            }
            else if (BuildPath(_name, _category, out var fullPath))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
                if (texture)
                {
                    Content.image = texture;
                }
            }
        }

        public delegate void OnImageLoadedDelegate(Texture image);
        private event OnImageLoadedDelegate OnImageLoadedEvent;

        private void PollLoading()
        {
            if (!Valid) return;

            // Calling all delegates
            OnImageLoadedEvent?.Invoke(Image);

            // Clearing all registered delegates
            OnImageLoadedEvent = null;

            // Unregistering from Editor update
            EditorApplication.update -= PollLoading;
        }

        /// <summary>
        /// When Image is accessed too early, it may not successfully load, in which case we recommend
        /// delaying the loading and assignment using this method.
        /// If using the legacy OnGUI() pattern, you probably don't need to use this delay assignment.
        /// But when working with the new UI pattern of Unity, initialization of objects is done only once and
        /// potentially too early for this TextureContent to be valid.
        /// </summary>
        public virtual void RegisterToImageLoaded(OnImageLoadedDelegate @delegate)
        {
            if (Valid)
            {
                @delegate?.Invoke(Image);
                return;
            }

            if (OnImageLoadedEvent == null)
            {
                // Registering to Editor update
                EditorApplication.update += PollLoading;
            }

            // Registering the new delegate
            OnImageLoadedEvent += @delegate;
        }
    }
}
