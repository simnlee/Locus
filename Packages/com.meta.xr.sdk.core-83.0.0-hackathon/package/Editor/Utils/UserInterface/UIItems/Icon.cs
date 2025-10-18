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
using static Meta.XR.Editor.UserInterface.Styles.Constants;
using static Meta.XR.Editor.UserInterface.Utils;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.Editor.UserInterface
{
    internal class Icon : IUserInterfaceItem
    {
        public bool Hide { get; set; }
        public string LabelText { get; set; }
        public GUIStyle Style { get; set; }
        public Color Color { get; set; }
        public GUIStyle LabelStyle { get; set; } = UIStyles.GUIStyles.Label;

        private readonly GUILayoutOption[] _options;
        private readonly TextureContent _icon;

        public Icon(TextureContent icon, params GUILayoutOption[] options) : this(icon, Color.white, "",
            options)
        {
        }

        public Icon(TextureContent icon, Color iconColor, string labelText = "", params GUILayoutOption[] options)
        {
            _icon = icon;
            Color = iconColor;
            LabelText = labelText;

            var layoutOptions = new List<GUILayoutOption>
            {
                GUILayout.Width(SmallIconSize),
                GUILayout.Height(SmallIconSize)
            };

            layoutOptions.AddRange(options);
            _options = layoutOptions.ToArray();
            Style = new GUIStyle(Styles.GUIStyles.IconUIItemStyle);
        }

        public Icon(TextureContent icon, Color iconColor, GUIStyle style, params GUILayoutOption[] options) :
            this(icon, iconColor, string.Empty, options)
        {
            Style = new GUIStyle(style);
        }

        public void Draw()
        {
            if (LabelTextAvailable)
            {
                EditorGUILayout.BeginHorizontal();
            }

            using (new ColorScope(ColorScope.Scope.Content, Color))
            {
                EditorGUILayout.LabelField(_icon, Style, _options);
            }

            if (LabelTextAvailable)
            {
                new Label(LabelText, LabelStyle).Draw();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool LabelTextAvailable => !String.IsNullOrEmpty(LabelText);
    }
}
