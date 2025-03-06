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
    [CustomEditor(typeof(ServerLocalization))]
    public class ServerLocalizationEditor : Editor
    {
        private SerializedProperty configurationMode;
        private SerializedProperty solverTypeProperty;
        
        private SerializedProperty priorNNCountMinProperty;
        private SerializedProperty priorNNCountMaxProperty;
        private SerializedProperty priorScaleProperty;
        private SerializedProperty priorRadiusProperty;
        
        private SerializedProperty onProgressEventProperty;
        
        private void OnEnable()
        {
            configurationMode = serializedObject.FindProperty("m_ConfigurationMode");
            solverTypeProperty = serializedObject.FindProperty("m_SolverType");
            
            priorNNCountMinProperty = serializedObject.FindProperty("m_PriorNNCountMin");
            priorNNCountMaxProperty = serializedObject.FindProperty("m_PriorNNCountMax");
            priorScaleProperty = serializedObject.FindProperty("m_PriorScale");
            priorRadiusProperty = serializedObject.FindProperty("m_PriorRadius");
            
            onProgressEventProperty = serializedObject.FindProperty("OnProgress");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Configuration mode
            
            EditorGUILayout.HelpBox(configurationMode.enumValueIndex == (int)ConfigurationMode.Always ? "This localization method will be active even if there are no maps configured to use it." : "This localization method will only be active if there are maps configured to use it.", MessageType.Info);
            
            configurationMode.enumValueIndex = EditorGUILayout.Popup("Configuration mode", configurationMode.enumValueIndex, configurationMode.enumNames);
       
            EditorGUILayout.Separator();
            
            if (solverTypeProperty.enumValueIndex is (int)SolverType.Prior or (int)SolverType.Lean)
            {
                EditorGUILayout.HelpBox("This Solver type is experimental. Extensive testing is advised.", MessageType.Warning);
            }
            
            // SolverType
            solverTypeProperty.enumValueIndex = EditorGUILayout.Popup("Solver type", solverTypeProperty.enumValueIndex, solverTypeProperty.enumNames);

            if (solverTypeProperty.enumValueIndex == (int)SolverType.Prior)
            {
                EditorGUILayout.PropertyField(priorNNCountMinProperty, new GUIContent("Nearest neighbour min"));
                EditorGUILayout.PropertyField(priorNNCountMaxProperty, new GUIContent("Nearest neighbour max"));
                EditorGUILayout.PropertyField(priorScaleProperty, new GUIContent("Scale"));
                EditorGUILayout.PropertyField(priorRadiusProperty, new GUIContent("Radius"));
            }
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.PropertyField(onProgressEventProperty, new GUIContent("Upload progress"));
            
            // Apply
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif