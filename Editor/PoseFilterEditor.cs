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
    [CustomEditor(typeof(PoseFilter))]
    public class PoseFilterEditor : Editor
    {
        private SerializedProperty filterMethodProperty;
        private SerializedProperty historySizeProperty;
        private SerializedProperty useConfidenceProperty;
        private SerializedProperty confidenceMaxProperty;
        private SerializedProperty confidenceImpactProperty;
        
        private void OnEnable()
        {
            filterMethodProperty = serializedObject.FindProperty("m_FilterMethod");
            historySizeProperty = serializedObject.FindProperty("m_HistorySize");
            useConfidenceProperty = serializedObject.FindProperty("m_UseConfidence");
            confidenceMaxProperty = serializedObject.FindProperty("m_ConfidenceMax");
            confidenceImpactProperty = serializedObject.FindProperty("m_ConfidenceImpact");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            if (filterMethodProperty.enumValueIndex == (int)FilterMethod.Legacy)
            {
                EditorGUILayout.HelpBox("Legacy filtering method is can cause issues with large maps and offsets. The simple filtering method is recommended instead.", MessageType.Warning);
            }
            else if (filterMethodProperty.enumValueIndex == (int)FilterMethod.Advanced)
            {
                EditorGUILayout.HelpBox("Using advanced features and parameters can decrease the quality of filtering in some configurations. Some features are experimental. Extensive testing is advised.", MessageType.Info);
            }

            // Method
            filterMethodProperty.enumValueIndex = EditorGUILayout.Popup("Filter method", filterMethodProperty.enumValueIndex, filterMethodProperty.enumNames);

            if (filterMethodProperty.enumValueIndex == (int)FilterMethod.Advanced)
            {
                EditorGUILayout.PropertyField(historySizeProperty, new GUIContent("History size"));
                EditorGUILayout.PropertyField(useConfidenceProperty, new GUIContent("Apply confidence"));
                if (useConfidenceProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(confidenceImpactProperty, new GUIContent("Confidence impact"));
                    EditorGUILayout.PropertyField(confidenceMaxProperty, new GUIContent("Confidence max value"));
                }
            }
            
            // Apply
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif