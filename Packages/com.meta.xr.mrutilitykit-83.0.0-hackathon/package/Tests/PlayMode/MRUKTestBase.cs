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


using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace Meta.XR.MRUtilityKit.Tests
{
    /// <summary>
    /// Base class for Mixed Reality Utility Kit tests that provides common functionality
    /// for loading scenes, handling MRUK initialization, and managing test environments.
    /// This class serves as a foundation for all MRUK test implementations.
    /// </summary>
    public class MRUKTestBase
    {
        /// <summary>
        /// Default timeout in milliseconds for async operations.
        /// Used to prevent tests from hanging indefinitely when waiting for async operations.
        /// </summary>
        protected const int DefaultTimeoutMs = 10000;

        /// <summary>
        /// Path to an empty scene used for unloading all other scenes.
        /// This scene is loaded when cleaning up test environments.
        /// </summary>
        protected const string EmptyScene = "Packages/com.meta.xr.mrutilitykit/Tests/Empty.unity";

        /// <summary>
        /// Loads a scene asynchronously in play mode.
        /// This method handles the scene loading process and optionally waits for MRUK initialization.
        /// </summary>
        /// <param name="sceneToLoad">Path to the scene to load.</param>
        /// <param name="awaitMRUKInit">If true, waits for MRUK to initialize after loading the scene.</param>
        /// <returns>An IEnumerator for use with Unity's coroutine system to handle the asynchronous operation.</returns>
        protected IEnumerator LoadScene(string sceneToLoad, bool awaitMRUKInit = true)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(sceneToLoad,
                new LoadSceneParameters(LoadSceneMode.Single));
            if (awaitMRUKInit && MRUK.Instance != null)
            {
                yield return new WaitUntil(() => MRUK.Instance.IsInitialized);
            }
        }

        /// <summary>
        /// Unloads all scenes by loading an empty scene.
        /// This method is typically used for cleanup between tests to ensure a clean testing environment.
        /// </summary>
        /// <returns>An IEnumerator for use with Unity's coroutine system to handle the asynchronous operation.</returns>
        protected IEnumerator UnloadScene()
        {
            // Loading an empty scene as single will unload all other scenes
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(EmptyScene,
                new LoadSceneParameters(LoadSceneMode.Single));
        }

        /// <summary>
        /// Loads a scene from a JSON string representation and waits for the operation to complete.
        /// This method handles the asynchronous loading process and verifies successful completion.
        /// </summary>
        /// <param name="sceneJson">JSON string containing the scene definition to load.</param>
        /// <returns>An IEnumerator for use with Unity's coroutine system to handle the asynchronous operation.</returns>
        protected IEnumerator LoadSceneFromJsonStringAndWait(string sceneJson)
        {
            // Loading from JSON is an async operation in the shared library so wait
            // until the task completes before continuing
            var result = MRUK.Instance.LoadSceneFromJsonString(sceneJson);
            yield return new WaitUntil(() => result.IsCompleted);
            Assert.AreEqual(MRUK.LoadDeviceResult.Success, result.Result, "Failed to load scene from json string");
        }

        /// <summary>
        /// Loads a scene from a prefab and waits for the operation to complete.
        /// This method instantiates the prefab as a scene, handles the asynchronous loading process,
        /// and verifies successful completion.
        /// </summary>
        /// <param name="scenePrefab">The GameObject prefab to load as a scene.</param>
        /// <param name="clearSceneFirst">If true, clears the existing scene before loading the prefab.</param>
        /// <returns>An IEnumerator for use with Unity's coroutine system to handle the asynchronous operation.</returns>
        protected IEnumerator LoadSceneFromPrefabAndWait(GameObject scenePrefab, bool clearSceneFirst = true)
        {
            // Loading from prefab is an async operation in the shared library so wait
            // until the task completes before continuing
            var result = MRUK.Instance.LoadSceneFromPrefab(scenePrefab, clearSceneFirst);
            yield return new WaitUntil(() => result.IsCompleted);
            Assert.AreEqual(MRUK.LoadDeviceResult.Success, result.Result, "Failed to load scene from prefab");
        }
    }
}

