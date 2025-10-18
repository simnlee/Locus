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
using System.Collections;
using Meta.XR.EnvironmentDepth;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Assertions;

namespace Meta.XR
{
    /// <summary>
    /// This component uses Depth API to provide raycasting functionality against the physical environment.<br/>
    /// Enabling this component adds the additional performance cost to the cost of using Depth API, so consider enabling it only when you need the raycasting functionality.<br/>
    /// This component automatically adds and enables the <see cref="EnvironmentDepthManager"/>.
    /// Consider disabling the <see cref="EnvironmentDepthManager"/> manually after disabling the <see cref="EnvironmentRaycastManager"/> to save performance.
    /// </summary>
    public class EnvironmentRaycastManager : MonoBehaviour
    {
        private static EnvironmentRaycastManager _instance;
        private static readonly IEnvironmentRaycastProvider _provider = CreateProvider();
        private static bool? _isSupported;

        private static bool IsUsingOpenXRProvider()
        {
#if META_USE_DEPTH_MANAGER_RAYCASTING
            return false;
#else
            return true;
#endif
        }

        private static IEnvironmentRaycastProvider CreateProvider()
        {
            if (IsUsingOpenXRProvider())
            {
                return new EnvironmentRaycastProviderMeta();
            }
            return new EnvironmentRaycastProviderDepthManager();
        }

        private void Awake()
        {
            Assert.IsNull(_instance, $"More than one {nameof(EnvironmentRaycastManager)} component. Only one instance is allowed at a time. New instance: {name}");
            if (!IsSupported)
            {
                Debug.LogError($"{nameof(EnvironmentRaycastManager)} is not supported. Please check the '{nameof(IsSupported)}' property before enabling this component.\n" +
                               "Open 'Meta > Tools > Project Setup Tool' to see requirements.\n");
#if UNITY_EDITOR
                Debug.LogError("When running in Editor over Meta Quest Link, please enable 'Settings > Beta > Spatial Data over Meta Quest Link'.");
#endif
            }
            _instance = this;
        }

        private void OnDestroy() => _instance = null;

        private void Start()
        {
            int markerId = IsUsingOpenXRProvider() ? TelemetryConstants.MarkerId.LoadEnvironmentRaycastManagerOpenXR : TelemetryConstants.MarkerId.LoadEnvironmentRaycastManager;
            OVRTelemetry.Start(markerId).Send();
        }

        private void OnEnable() => SetProviderEnabled(true);
        private void OnDisable() => SetProviderEnabled(false);

        private void SetProviderEnabled(bool isEnabled)
        {
            if (IsSupported)
            {
                _provider.SetEnabled(isEnabled, this);
            }
        }

        private class EnvironmentRaycastProviderDepthManager : IEnvironmentRaycastProvider
        {
            private EnvironmentDepthManager _depthManager;

            bool IEnvironmentRaycastProvider.IsReady
            {
                get
                {
                    EnsureDepthManagerIsPresent();
                    if (!_depthManager.enabled || !_depthManager.gameObject.activeInHierarchy)
                    {
                        Debug.LogError("Please enable the '" + nameof(EnvironmentDepthManager) + "' component and its GameObject.", _depthManager);
                        return false;
                    }
                    return true;
                }
            }

            private void EnsureDepthManagerIsPresent()
            {
                if (_depthManager == null)
                {
                    _depthManager = FindAnyObjectByType<EnvironmentDepthManager>(FindObjectsInactive.Include);
                    if (_depthManager == null)
                    {
                        _depthManager = new GameObject(nameof(EnvironmentDepthManager)).AddComponent<EnvironmentDepthManager>();
                        Debug.LogWarning("EnvironmentDepthManager was added to the scene by " + nameof(EnvironmentRaycastManager) + ". Please add EnvironmentDepthManager to prevent this warning.");
                    }
                }
            }

            void IEnvironmentRaycastProvider.SetEnabled(bool isEnabled, EnvironmentRaycastManager _)
            {
                if (isEnabled)
                {
                    if (IsSupported)
                    {
                        EnsureDepthManagerIsPresent();
                        _depthManager.SetRaycastWarmUpEnabled(true);
                    }
                    else
                    {
                        string message = $"{nameof(EnvironmentRaycastManager)} is not supported. Requirements: Quest 3 or newer, Unity >= 2022.3.\n";
                        if (Application.isEditor)
                        {
                            message += $"To run the {nameof(EnvironmentRaycastManager)} in Editor, please use Meta Quest Link.\n";
                        }
                        Debug.LogError(message);
                    }
                }
                else
                {
                    if (IsSupported && _depthManager != null)
                    {
                        _depthManager.SetRaycastWarmUpEnabled(false);
                    }
                }
            }

            bool IEnvironmentRaycastProvider.IsSupported => EnvironmentDepthManager.IsSupported;

            bool IEnvironmentRaycastProvider.Raycast(Ray ray, out EnvironmentRaycastHit hit, float maxDistance, bool reconstructNormal, bool allowOccludedRayOrigin)
            {
                bool result = _depthManager.Raycast(ray, out var depthManagerHit, maxDistance, reconstructNormal: reconstructNormal, allowOccludedRayOrigin: allowOccludedRayOrigin);
                hit = ToEnvRaycastHit(depthManagerHit);
                return result;
            }
        }

        /// <summary>
        /// Checks if the environment raycast is supported.
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                _isSupported ??= _provider.IsSupported;
                return _isSupported.Value;
            }
        }

        /// <summary>
        /// Casts a ray against the environment. Returns 'true' if the cast is successful.
        /// </summary>
        /// <param name="ray">The starting point and direction of the ray.</param>
        /// <param name="hit">The result of the raycast.</param>
        /// <param name="maxDistance">The max distance the ray should check for collisions.</param>
        /// <returns>'true' if <see cref="EnvironmentRaycastHit.status"/> is <see cref="EnvironmentRaycastHitStatus.Hit"/>.</returns>
        public bool Raycast(Ray ray, out EnvironmentRaycastHit hit, float maxDistance = 100f)
        {
            if (!IsReady)
            {
                hit = new EnvironmentRaycastHit { status = EnvironmentRaycastHitStatus.NotReady };
                return false;
            }
            if (!IsSupported)
            {
                hit = new EnvironmentRaycastHit { status = EnvironmentRaycastHitStatus.NotSupported };
                return false;
            }
            return _provider.Raycast(ray, out hit, maxDistance);
        }

        private static EnvironmentRaycastHit ToEnvRaycastHit(DepthRaycastHit depthHit)
        {
            return new EnvironmentRaycastHit
            {
                status = ToStatus(depthHit.result),
                point = depthHit.point,
                normal = depthHit.normal,
                normalConfidence = depthHit.normalConfidence
            };

            static EnvironmentRaycastHitStatus ToStatus(DepthRaycastResult depthHitResult)
            {
                switch (depthHitResult)
                {
                    case DepthRaycastResult.Success:
                        return EnvironmentRaycastHitStatus.Hit;
                    case DepthRaycastResult.NotReady:
                        return EnvironmentRaycastHitStatus.NotReady;
                    case DepthRaycastResult.HitPointOccluded:
                        return EnvironmentRaycastHitStatus.HitPointOccluded;
                    case DepthRaycastResult.RayOutsideOfDepthCameraFrustum:
                        return EnvironmentRaycastHitStatus.HitPointOutsideOfCameraFrustum;
                    case DepthRaycastResult.RayOccluded:
                        return EnvironmentRaycastHitStatus.RayOccluded;
                    case DepthRaycastResult.NoHit:
                        return EnvironmentRaycastHitStatus.NoHit;
                    default:
                        throw new Exception($"Invalid result type: {depthHitResult}.");
                }
            }
        }

        /// <summary>
        /// Tries to place the box on the flat and free surface.<br/>
        /// First, this method aligns the box with the flat surface: box.forward is aligned with the surface normal, box.up is aligned with <paramref name="upwards"/>.<br/>
        /// Then it checks if the box can fit the environment by checking collisions.<br/>
        /// </summary>
        /// <param name="ray">The desired direction of placement. The common use case is to construct this ray with:<code>new Ray(controllerTransform.position, controllerTransform.forward)</code></param>
        /// <param name="boxSize">Size of the box in local-space coordinates. Width is aligned with the local x-axis, height is aligned with the local y-axis, length is aligned with the local z-axis.<br/>
        /// 'x' and 'y' components should be greater than <see cref="EnvironmentDepthManagerRaycastExtensions.MinXYSize"/> to correctly determine the surface normal.<br/>
        /// 'z' component can be zero for flat objects.<br/>
        /// </param>
        /// <param name="upwards">The local y-axis of the box is aligned with <paramref name="upwards"/> vector before checking for collisions with the environment.</param>
        /// <param name="hit">Contains the placement result. Example of how to apply the pose to the object:<code>transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal, upwards));</code></param>
        /// <returns>'true' only if the surface is flat, free of clutter and big enough to fit the dimensions of the box.</returns>
        public bool PlaceBox(Ray ray, Vector3 boxSize, Vector3 upwards, out EnvironmentRaycastHit hit)
        {
            if (!IsReady)
            {
                hit = new EnvironmentRaycastHit { status = EnvironmentRaycastHitStatus.NotReady };
                return false;
            }
            if (!IsSupported)
            {
                hit = new EnvironmentRaycastHit { status = EnvironmentRaycastHitStatus.NotSupported };
                return false;
            }
            return _provider.PlaceBox(ray, boxSize, upwards, out hit);
        }

        /// <summary>
        /// Checks whether the given box overlaps with the environment.
        /// </summary>
        /// <param name="center">Center of the box.</param>
        /// <param name="halfExtents">Half the size of the box in each dimension.</param>
        /// <param name="orientation">Rotation of the box.</param>
        /// <returns>Returns 'true' if the box overlaps with the environment.</returns>
        public bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation)
        {
            if (!IsReady)
            {
                return false;
            }
            return IsSupported && _provider.CheckBox(center, halfExtents, orientation);
        }

        private bool IsReady
        {
            get
            {
                if (!enabled || !gameObject.activeInHierarchy)
                {
                    Debug.LogError("Please enable the '" + nameof(EnvironmentRaycastManager) + "' component and its GameObject.", this);
                    return false;
                }
                return _provider.IsReady;
            }
        }

        /// <summary>
        /// This transform allows you to override the default tracking space.
        /// </summary>
        [SerializeField] public Transform CustomTrackingSpace;

        private Transform GetTrackingSpace()
        {
            if (CustomTrackingSpace != null)
            {
                return CustomTrackingSpace;
            }
            return OVRCameraRig.GetTrackingSpace();
        }

        private class EnvironmentRaycastProviderMeta : IEnvironmentRaycastProvider
        {
            private unsafe readonly OVRPlugin.RaycastFilterHeader*[] _filters = new OVRPlugin.RaycastFilterHeader*[1];
            private bool _isEnabled;
            private bool _isCreating;
            private ulong? _handle;
            private int _lastTrackingSpaceUpdateFrame = -1;
            private Matrix4x4 _worldToTrackingMatrix = Matrix4x4.identity;
            private Matrix4x4 _trackingToWorldMatrix = Matrix4x4.identity;

            bool IEnvironmentRaycastProvider.IsSupported => OVRPlugin.GetEnvironmentRaycastSupported(out bool isSupported).IsSuccess() && isSupported;

            void IEnvironmentRaycastProvider.SetEnabled(bool isEnabled, EnvironmentRaycastManager raycastManager)
            {
                _isEnabled = isEnabled;
                if (!_isCreating)
                {
                    raycastManager.StopCoroutine(WaitForPermissionsAndCreateHandle());
                    if (isEnabled)
                    {
                        if (Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
                        {
                            CreateHandle();
                        }
                        else
                        {
                            Debug.LogWarning($"{nameof(EnvironmentRaycastManager)} doesn't have the required camera permission: {OVRPermissionsRequester.ScenePermission}. Waiting for permission before enabling environment raycast...", raycastManager);
                            raycastManager.StartCoroutine(WaitForPermissionsAndCreateHandle());
                        }
                    }
                    else if (_handle.HasValue)
                    {
                        DestroyEnvironmentRaycaster(_handle.Value);
                        _handle = null;
                    }
                }
            }

            private IEnumerator WaitForPermissionsAndCreateHandle()
            {
                while (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
                {
                    yield return null;
                }
                CreateHandle();
            }

            private async void CreateHandle()
            {
                var result = OVRPlugin.CreateEnvironmentRaycasterAsync(out ulong future);
                if (!result.IsSuccess())
                {
                    Debug.LogError($"OVRPlugin.CreateEnvironmentRaycasterAsync() failed with result {result}.");
                    return;
                }

                _isCreating = true;
                result = await OVRFuture.When(future);
                _isCreating = false;

                if (!result.IsSuccess())
                {
                    Debug.LogError($"OVRPlugin.CreateEnvironmentRaycasterAsync() future failed with result {result}.");
                    return;
                }

                result = OVRPlugin.CreateEnvironmentRaycasterComplete(future, out var completion);
                if (!result.IsSuccess())
                {
                    Debug.LogError($"OVRPlugin.CreateEnvironmentRaycasterComplete() failed with result {result}.");
                    return;
                }

                var handle = completion.EnvironmentRaycaster;
                if (_isEnabled)
                {
                    _handle = handle;
                }
                else
                {
                    DestroyEnvironmentRaycaster(handle);
                }
            }

            private static void DestroyEnvironmentRaycaster(ulong handle)
            {
                OVRPlugin.DestroyEnvironmentRaycaster(handle); // can fail when the app is quitting, so no need to check the result and log an error
            }

            public bool IsReady => _handle.HasValue;

            bool IEnvironmentRaycastProvider.Raycast(Ray ray, out EnvironmentRaycastHit hit, float maxDistance, bool reconstructNormal, bool allowOccludedRayOrigin)
            {
                int curFrame = Time.frameCount;
                if (_lastTrackingSpaceUpdateFrame != curFrame)
                {
                    _lastTrackingSpaceUpdateFrame = curFrame;
                    var trackingSpace = _instance.GetTrackingSpace();
                    if (trackingSpace != null)
                    {
                        _worldToTrackingMatrix = trackingSpace.worldToLocalMatrix;
                        _trackingToWorldMatrix = trackingSpace.localToWorldMatrix;
                    }
                }

                Assert.IsTrue(_handle.HasValue);
                hit = new EnvironmentRaycastHit { status = EnvironmentRaycastHitStatus.NoHit };

                unsafe
                {
                    var distanceFilter = new OVRPlugin.RaycastDistanceFilter
                    {
                        Type = OVRPlugin.RaycastFilterType.Distance,
                        MaxDistance = maxDistance
                    };

                    _filters[0] = (OVRPlugin.RaycastFilterHeader*)&distanceFilter;
                    fixed (OVRPlugin.RaycastFilterHeader** pinnedFiltersPointer = &_filters[0])
                    {
                        var getInfo = new OVRPlugin.RaycastHitPointGetInfo
                        {
                            StartPoint = _worldToTrackingMatrix.MultiplyPoint3x4(ray.origin).ToFlippedZVector3f(),
                            Direction = _worldToTrackingMatrix.MultiplyVector(ray.direction).ToFlippedZVector3f(),
                            NumFilter = (uint)_filters.Length,
                            Filters = pinnedFiltersPointer
                        };

                        var result = OVRPlugin.PerformEnvironmentRaycast(_handle.Value, getInfo, out var raycastHit);
                        if (!result.IsSuccess())
                        {
                            return false;
                        }

                        hit.status = raycastHit.Status switch
                        {
                            OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_Hit or OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_InvalidOrientation => EnvironmentRaycastHitStatus.Hit,
                            OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_NoHit => EnvironmentRaycastHitStatus.NoHit,
                            OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_HitPointOccluded => EnvironmentRaycastHitStatus.HitPointOccluded,
                            OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_HitPointOutsideFoV => EnvironmentRaycastHitStatus.HitPointOutsideOfCameraFrustum,
                            OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_RayOccluded => EnvironmentRaycastHitStatus.RayOccluded,
                            _ => throw new Exception($"Unknown OVRPlugin.PerformEnvironmentRaycast() status: {raycastHit.Status}.")
                        };

                        var pose = MRUK.FlipZRotateY180(new Pose(raycastHit.Pose.Position.FromVector3f(), raycastHit.Pose.Orientation.FromQuatf()));
                        hit.point = _trackingToWorldMatrix.MultiplyPoint3x4(pose.position);
                        if (raycastHit.Status == OVRPlugin.EnvironmentRaycastStatus.EnvironmentRaycastStatus_Hit)
                        {
                            hit.normal = _trackingToWorldMatrix.MultiplyVector(pose.rotation * Vector3.forward);
                            hit.normalConfidence = 1f;
                        }

                        return hit.status == EnvironmentRaycastHitStatus.Hit;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Contains the result of the environment raycast.
    /// </summary>
    public struct EnvironmentRaycastHit
    {
        /// Status of the environment raycast.
        public EnvironmentRaycastHitStatus status;
        /// Intersection point in world space where the ray hit the environment.
        public Vector3 point;
        /// The normal of the surface the ray intersected with.
        public Vector3 normal;
        /// Normal confidence in the range of [0,1].
        public float normalConfidence;
    }

    /// <summary>
    /// Status of the environment raycast.
    /// </summary>
    public enum EnvironmentRaycastHitStatus
    {
        /// The intersection with the environment is found. <see cref="EnvironmentRaycastManager.Raycast"/> returns 'true' only in this case.
        Hit,
        /// The ray intersects with the environment, but the actual hit point is invisible.<br/>
        /// The <see cref="EnvironmentRaycastHit.point"/> will contain the first point on the ray that's occluded by the environment, but <see cref="EnvironmentRaycastManager.Raycast"/> will return 'false'.
        HitPointOccluded,
        /// The raycasting system is not ready yet or the <see cref="EnvironmentRaycastManager"/> is not enabled. It takes several frames after the <see cref="EnvironmentRaycastManager"/> is enabled before the raycast is ready.
        NotReady,
        /// The hit point can't be determined because it lies outside of the depth camera frustum.
        HitPointOutsideOfCameraFrustum,
        /// The ray is completely occluded by the environment.
        RayOccluded,
        /// The intersection with the environment is not found.
        NoHit,
        /// The environment raycast is not supported. Check the <see cref="EnvironmentRaycastManager.IsSupported"/> before calling raycasting methods.
        NotSupported
    }

    internal interface IEnvironmentRaycastProvider
    {
        bool IsSupported { get; }
        void SetEnabled(bool isEnabled, EnvironmentRaycastManager raycastManager);
        bool IsReady { get; }
        bool Raycast(Ray ray, out EnvironmentRaycastHit hit, float maxDistance = 100f, bool reconstructNormal = true, bool allowOccludedRayOrigin = true);
    }
}
