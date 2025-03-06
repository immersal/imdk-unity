/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Immersal.XR
{
    [CustomEditor(typeof(XRSpace))]
    public class XRSpaceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Place XR Maps and all content under this object.\n(you can also use a custom implementation of ISceneParent)", MessageType.Info);
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("DataProcessors added at runtime are not serialized and thus do not show up in the inspector.", MessageType.Warning);
            }
            DrawDefaultInspector();
        }
    }
}
#endif