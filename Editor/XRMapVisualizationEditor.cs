/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

#if UNITY_EDITOR
using System;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace Immersal.XR
{
    [CustomEditor(typeof(XRMapVisualization))]
    public class XRMapVisualizationEditor : Editor
    {
        // visualization
        private SerializedProperty renderModeProperty;
        private SerializedProperty pointColorProperty;
        
        private static float pointSizeSliderValue = 0.33f;
        private static bool renderAs3dPointsToggle = true;

        private void OnEnable()
        {
            pointSizeSliderValue = EditorPrefs.GetFloat("pointSizeSliderValue", pointSizeSliderValue);
            renderAs3dPointsToggle = EditorPrefs.GetBool("pointSizeSliderValue", renderAs3dPointsToggle);
            renderModeProperty = serializedObject.FindProperty("renderMode");
            pointColorProperty = serializedObject.FindProperty("m_PointColor");
        }

        public override void OnInspectorGUI()
        {
            if (BuildPipeline.isBuildingPlayer)
                return;
            
            XRMapVisualization obj = (XRMapVisualization)target;
            
            obj.UpdateMaterial();
            
            if (obj.IsVisualized)
            {
                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Reset visualization"))
                    {
                        obj.ClearVisualization();
                    }
                }

                EditorGUILayout.PropertyField(renderModeProperty);
                EditorGUILayout.PropertyField(pointColorProperty);
            
                EditorGUI.BeginChangeCheck();
                pointSizeSliderValue = EditorGUILayout.Slider("Point Size", pointSizeSliderValue, 0f, 1f);
                renderAs3dPointsToggle = EditorGUILayout.Toggle("Render as 3D Points", renderAs3dPointsToggle);
                if (EditorGUI.EndChangeCheck())
                {
                    XRMapVisualization.pointSize = pointSizeSliderValue;
                    XRMapVisualization.renderAs3dPoints = renderAs3dPointsToggle;

                    EditorPrefs.SetFloat("pointSizeSliderValue", pointSizeSliderValue);
                    EditorPrefs.SetBool("pointSizeSliderValue", renderAs3dPointsToggle);
                    
                    obj.UpdateMaterial();

                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("To visualize the ARMap in the Editor, load either the sparse or dense PLY file.", MessageType.Warning);
                
                // Ply loading
                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Load local sparse ply file"))
                    {
                        PickAndLoadPly(obj);
                    }
                    if (GUILayout.Button("Download sparse ply file"))
                    {
                        EditorCoroutineUtility.StartCoroutine(MapManager.DownloadSparseFile(
                            obj.Map.mapId, obj.Map.mapName,
                            (result, path) =>
                            {
                                // apply bytes and process
                                obj.LoadPly(path);
                                            
                            }), this);
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
        
        private void PickAndLoadPly(XRMapVisualization obj)
        {
            string path = EditorUtility.OpenFilePanel("Select Ply", "", "ply");

            if (path.Length != 0)
            {
                obj.LoadPly(path);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
    }
}
#endif
