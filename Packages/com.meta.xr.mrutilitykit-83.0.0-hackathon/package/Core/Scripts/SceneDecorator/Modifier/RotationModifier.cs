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

using Meta.XR.Util;
using UnityEngine;

namespace Meta.XR.MRUtilityKit.SceneDecorator
{
    /// <summary>
    /// Rotates a decoration around an axis.
    /// </summary>
    [Feature(Feature.Scene)]
    public class RotationModifier : Modifier
    {
        [SerializeField]
        [Tooltip("The mask used to determine rotation values across the decoration area")]
        public Mask mask;

        [SerializeField]
        [Tooltip("Minimum limit for rotation angle in degrees")]
        public float limitMin = float.NegativeInfinity;

        [SerializeField]
        [Tooltip("Maximum limit for rotation angle in degrees")]
        public float limitMax = float.PositiveInfinity;

        [SerializeField]
        [Tooltip("Multiplier applied to the mask value to control rotation magnitude")]
        public float scale = 1f;

        [SerializeField]
        [Tooltip("Constant offset added to the rotation angle in degrees")]
        public float offset = 0f;

        [SerializeField]
        [Tooltip("Axis around which the decoration will rotate")]
        public Vector3 rotationAxis = new Vector3(0f, 1f, 0f);

        [SerializeField]
        [Tooltip("When enabled, rotation axis is relative to the decoration's local space; otherwise uses world space")]
        public bool localSpace = false;

        public override void ApplyModifier(GameObject decorationGO, MRUKAnchor sceneAnchor, SceneDecoration sceneDecoration, Candidate candidate)
        {
            var axis = (localSpace ? decorationGO.transform.rotation : Quaternion.identity) * rotationAxis;
            decorationGO.transform.rotation *= Quaternion.AngleAxis(mask.SampleMask(candidate, limitMin, limitMax, scale, offset), axis);
        }
    }
}
