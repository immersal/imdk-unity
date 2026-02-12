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
    [CustomEditor(typeof(GeoPoseLocalization))]
    public class GeoPoseLocalizationEditor : Editor
    {
        private SerializedProperty configurationMode;

        private SerializedProperty onProgressEventProperty;
        private SerializedProperty locationProviderProperty;
        private SerializedProperty searchRadiusProperty;

        private void OnEnable()
        {
            configurationMode = serializedObject.FindProperty("m_ConfigurationMode");

            onProgressEventProperty = serializedObject.FindProperty("OnProgress");
            locationProviderProperty = serializedObject.FindProperty("m_LocationProvider");
            searchRadiusProperty = serializedObject.FindProperty("m_SearchRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Configuration mode
            
            EditorGUILayout.HelpBox(configurationMode.enumValueIndex == (int)ConfigurationMode.Always ? "This localization method will be active even if there are no maps configured to use it." : "This localization method will only be active if there are maps configured to use it.", MessageType.Info);

            configurationMode.enumValueIndex = EditorGUILayout.Popup("Configuration mode", configurationMode.enumValueIndex, configurationMode.enumNames);

            EditorGUILayout.Separator();

            EditorGUILayout.PropertyField(locationProviderProperty, new GUIContent("GPS location provider"));

            EditorGUILayout.Separator();

            EditorGUILayout.Separator();

            EditorGUILayout.PropertyField(onProgressEventProperty, new GUIContent("Upload progress"));

            EditorGUILayout.Separator();

            EditorGUILayout.HelpBox("Optional Immersal-specific parameters that are not part of the OSCP GeoPose Protocol definition", MessageType.Info);

            EditorGUILayout.PropertyField(searchRadiusProperty, new GUIContent("Search radius in metres"));

            // Apply
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif