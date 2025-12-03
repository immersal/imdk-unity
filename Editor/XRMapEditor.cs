/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.IO;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Unity.EditorCoroutines.Editor;
using Immersal.REST;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
    [CustomEditor(typeof(XRMap))]
    public class XRMapEditor : Editor
    {
        private ImmersalSDK sdk;
        private GameObject sceneParentObj;
        private ISceneUpdateable sceneParent;
        private int userEnteredMapId = -1;
        private bool[] downloadSelections = { true, false, false };

        // Localization method requires some extra variables
        private static ILocalizationMethod[] availableMethods;
        private static string[] availableMethodNames;
        private int selectedTypeIndex; // Current selection
        
        // Other properties
        private SerializedProperty mapFileProperty;
        private SerializedProperty mapIdProperty;
        private SerializedProperty mapNameProperty;
        private SerializedProperty privacyProperty;
        private SerializedProperty alignmentProperty;
        private SerializedProperty wgs84Property;

        private void OnEnable()
        {
            XRMap map = (XRMap)target;
            mapFileProperty = serializedObject.FindProperty("mapFile");
            mapIdProperty = serializedObject.FindProperty("m_MapId");
            mapNameProperty = serializedObject.FindProperty("m_MapName");
            privacyProperty = serializedObject.FindProperty("privacy");
            alignmentProperty = serializedObject.FindProperty("mapAlignment");
            wgs84Property = serializedObject.FindProperty("wgs84");
            
            // Localization method
            if (!CheckAvailableLocalizationMethods())
            {
                RefreshAvailableLocalizationMethods();
            }
            
            // Check for null
            if (map.LocalizationMethod.IsNullOrDead())
            {
                ImmersalLogger.LogWarning($"Map {map.name} has invalid localization method, resetting to DeviceLocalization.");
                if (!TrySetLocalizationMethod(map, typeof(DeviceLocalization)))
                {
                    ImmersalLogger.LogError($"Failed. Uninitializing map {map.name}");
                    map.Uninitialize();
                }
            }
            
            // Get current index
            if (!map.LocalizationMethod.IsNullOrDead() && availableMethodNames != null)
            {
                selectedTypeIndex = Array.IndexOf(availableMethodNames, map.LocalizationMethod.GetType().Name);
                if (selectedTypeIndex == -1) selectedTypeIndex = 0;
            }
        }

        private bool CheckAvailableLocalizationMethods()
        {
            if (availableMethods == null || availableMethodNames == null)
                return false;

            foreach (ILocalizationMethod localizationMethod in availableMethods)
            {
                if (localizationMethod.IsNullOrDead())
                    return false;
            }

            return true;
        }

        private void RefreshAvailableLocalizationMethods()
        {
            ILocalizationMethod[] methods = MapManager.AvailableLocalizationMethods;
            if (methods != null)
            {
                availableMethods = methods;
                availableMethodNames = availableMethods.Select(m => m.GetType().Name).ToArray();
            }
        }

        [InitializeOnLoadMethod]
        private static void ClearAvailableLocalizationMethodTypes()
        {
            availableMethods = null;
            availableMethodNames = null;
        }

        public override void OnInspectorGUI()
        {
            if (BuildPipeline.isBuildingPlayer)
                return;

            serializedObject.Update();
            XRMap obj = (XRMap)target;
            
            // Configure styles
            GUIStyle centeredBoldLabel = new GUIStyle(EditorStyles.boldLabel);
            centeredBoldLabel.alignment = TextAnchor.MiddleCenter;
            GUIStyle bigLabel = new GUIStyle(EditorStyles.boldLabel);
            bigLabel.fontSize = 16;

            // The Custom Editor has two states
            
            // Map is configured
            if (obj.IsConfigured)
            {
                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.5f, 1f, 0.6f);
                EditorGUILayout.HelpBox("Map configured!", MessageType.Info);
                GUI.backgroundColor = oldColor;
                
                // Reconfigure
                if (GUILayout.Button("Reconfigure map"))
                {
                    obj.Uninitialize();
                }
                EditorGUILayout.Space();
                
                // Localization method
                EditorGUI.BeginChangeCheck();
                
                if (availableMethods != null && availableMethodNames.Length > 0)
                {
                    selectedTypeIndex =
                        EditorGUILayout.Popup("Localization method", selectedTypeIndex, availableMethodNames);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    TrySetLocalizationMethod(obj, availableMethods[selectedTypeIndex]);
                }
                
                // Map options
                MapOptionsSection(obj);

                // Metadata
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Map Metadata", EditorStyles.boldLabel);

                GUI.enabled = false;
                EditorGUILayout.PropertyField(mapIdProperty);
                EditorGUILayout.PropertyField(mapNameProperty);
                EditorGUILayout.PropertyField(privacyProperty);
                EditorGUILayout.PropertyField(wgs84Property);
                EditorGUILayout.PropertyField(alignmentProperty);
                GUI.enabled = true;
                
                // Map Alignment controls
                EditorGUILayout.HelpBox("Alignment metadata stored in right-handed coordinate system. Captured (default) alignment is in ECEF coordinates", MessageType.Info);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Load Alignment", "Loads alignment from map metadata. Coordinate system is unknown (ECEF or Unity's)")))
                {
                    EditorCoroutineUtility.StartCoroutine(MapAlignmentLoad(), this);
                }

                if (GUILayout.Button(new GUIContent("Save Alignment", "Saves current (local transform) alignment to map metadata")))
                {
                    EditorCoroutineUtility.StartCoroutine(MapAlignmentSave(), this);
                }

                oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.5f, 0.6f);

                if (GUILayout.Button(new GUIContent("Reset Alignment", "Fetches the original captured alignment metadata in ECEF coordinates")))
                {
                    EditorCoroutineUtility.StartCoroutine(MapAlignmentReset(), this);
                }

                GUI.backgroundColor = oldColor;

                GUILayout.EndHorizontal();
                
                // Visualization
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);

                if (obj.Visualization != null)
                {
                    if (GUILayout.Button("Select visualization"))
                    {
                        Selection.SetActiveObjectWithContext(obj.Visualization.gameObject, null);
                    }
                    if (GUILayout.Button("Remove visualization"))
                    {
                        obj.RemoveVisualization();
                    }
                }
                else
                {
                    if (GUILayout.Button("Add visualization"))
                    {
                        obj.CreateVisualization();
                        Selection.SetActiveObjectWithContext(obj.Visualization.gameObject, null);
                    }
                }
            }
            
            // Map unconfigured
            else
            {
                // Check that a parent implements ISceneUpdateable
                ISceneUpdateable sceneUpdateable = obj.transform.GetComponentInParent<ISceneUpdateable>(true);
                
                if (sceneUpdateable != null)
                {
                    // Configuration instructions and options
                    
                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.6f);
                    EditorGUILayout.HelpBox("Map has not been configured! Add or download map data below.", MessageType.Warning);
                    GUI.backgroundColor = oldColor;
                    
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Local map file", bigLabel);
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Drag and drop your map file (.bytes) here or use the selector to find one.", EditorStyles.wordWrappedLabel);
                    
                    // Mapfile option
                    
                    EditorGUILayout.PropertyField(mapFileProperty);
                    
                    //currentMapFile = obj.mapFile;
                    TextAsset mapFile = mapFileProperty.objectReferenceValue as TextAsset;
                    if (mapFile != null)
                    {
                        string bytesPath = AssetDatabase.GetAssetPath(mapFile);
                        if (bytesPath.EndsWith(".bytes"))
                        {
                            if (TrySetLocalizationMethod(obj, typeof(DeviceLocalization)))
                            {
                                obj.Configure(mapFile);
                            }
                        }
                        else
                        {
                            ImmersalLogger.Log($"{AssetDatabase.GetAssetPath(mapFile)} is not a valid map file");
                            obj.mapFile = null;
                        }
                    }
                    
                    // Download options
                    
                    EditorGUILayout.Space(12f);
                    EditorGUILayout.LabelField("OR", centeredBoldLabel, GUILayout.ExpandWidth(true));
                    EditorGUILayout.Space(12f);
                    EditorGUILayout.LabelField("Download from cloud", bigLabel);
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Input your map id and select which data to download. You can configure the save path in the ImmersalSDK configuration.", EditorStyles.wordWrappedLabel);
                    
                    userEnteredMapId = EditorGUILayout.IntField("Map id: ", userEnteredMapId);
                    EditorGUILayout.Space();
                    
                    // Section for selecting data types to download
                    DownloadSelectionSection();
                    
                    // Download
                    
                    if (GUILayout.Button("Download"))
                    {
                        // metadata is always downloaded
                        EditorCoroutineUtility.StartCoroutine(
                            MapManager.DownloadMapMetadata(userEnteredMapId, metadata =>
                            {
                                // apply meta
                                obj.SetMetadata(metadata, true);
                                
                                // mapfile
                                if (downloadSelections[1])
                                {
                                    EditorCoroutineUtility.StartCoroutine(MapManager.DownloadMapFile(
                                        obj.mapId, obj.mapName, (result, mapFileAsset) =>
                                    {
                                        if (TrySetLocalizationMethod(obj, typeof(DeviceLocalization)))
                                        {
                                            // apply map file and process
                                            obj.Configure(mapFileAsset);
                                        }
                                    }), this);
                                }
                                else
                                {
                                    if (TrySetLocalizationMethod(obj, typeof(ServerLocalization)))
                                    {
                                        obj.Configure();
                                    }
                                }

                                // vis
                                if (downloadSelections[2])
                                {
                                    EditorCoroutineUtility.StartCoroutine(MapManager.DownloadSparseFile(
                                        obj.mapId, obj.mapName, (result, path) =>
                                    {
                                        // apply bytes and process
                                        obj.CreateVisualization();
                                        obj.Visualization.LoadPly(path);
                                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                                    
                                    }), this);
                                }
                            }), this);
                    }
                }
                // Invalid parent
                else
                {
                    EditorGUILayout.HelpBox("Scene parent does not implement ISceneUpdateable.", MessageType.Error, true);
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private bool TrySetLocalizationMethod(XRMap map, Type localizationMethodType)
        {
            ILocalizationMethod method = availableMethods.FirstOrDefault(m => m.GetType() == localizationMethodType);
            return TrySetLocalizationMethod(map, method);
        }
        
        private bool TrySetLocalizationMethod(XRMap map, ILocalizationMethod localizationMethod)
        {
            if (localizationMethod.IsNullOrDead())
            {
                ImmersalLogger.LogError("Invalid localization method assignment.");
                MapManager.RefreshLocalizationMethods();
                RefreshAvailableLocalizationMethods();
                return false;
            }
            
            // Ensure index is also updated when call is not originating from direct index change
            selectedTypeIndex = Array.IndexOf(availableMethodNames, localizationMethod.GetType().Name);
            if (selectedTypeIndex == -1) selectedTypeIndex = 0;
            
            //map.LocalizationMethodType = localizationMethodType;
            map.LocalizationMethod = localizationMethod;
            serializedObject.ApplyModifiedProperties();
            map.UpdateMapOptions();
            EditorUtility.SetDirty(map);

            return true;
        }
        
        // Render map configurations
        private void MapOptionsSection(XRMap map)
        {
            if (map.MapOptions == null)
                return;
            
            if (map.MapOptions.Count == 0)
                return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{availableMethodNames[selectedTypeIndex]} options", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            foreach (IMapOption configuration in map.MapOptions)
            {
                configuration.DrawEditorGUI(map);
            }
            if (EditorGUI.EndChangeCheck())
            {
                map.SerializeMapOptions();
                EditorUtility.SetDirty(map);
            }
        }

        // Render download options
        private void DownloadSelectionSection()
        {
            string[] labels = { "Metadata", "Mapfile", "Visualization" };
            string[] minis = {
                "The map metadata contains information about the map alignment, privacy, etc. It is the minimum requirement for configuring maps.",
                "The map file is a binary representation of the map. This is required for embedding maps for offline use.",
                "This is the sparse point cloud of the map that can be used to visualize the map."
            };
            bool[] enabled = { false, true, true };

            GUILayout.BeginHorizontal();

            // Left column
            GUILayout.BeginVertical();
            for (int i = 0; i < labels.Length; i++)
            {
                GUILayout.BeginVertical();
                GUILayout.Label(labels[i]);
                GUILayout.Label(minis[i], EditorStyles.wordWrappedMiniLabel);
                GUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            GUILayout.EndVertical();
            
            // Right column
            GUILayout.BeginVertical();
            for (int i = 0; i < downloadSelections.Length; i++)
            {
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                GUI.enabled = enabled[i];
                downloadSelections[i] = EditorGUILayout.Toggle(downloadSelections[i]); //, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();
        }

        #region Alignment methods
        
        private IEnumerator MapAlignmentLoad(bool force = false)
        {
            //
            // Loads map metadata, updates XR Map metadata info, extracts the alignment, converts it to Unity's coordinate system and sets the map transform
            //
            
            XRMap obj = (XRMap)target;
            sdk = ImmersalSDK.Instance;

            if (!force)
            {
                // Check if metadata file exists already
                string destinationFolder = MapManager.GetDirectoryPath();
                string existingFilePath = Path.Combine(destinationFolder, $"{obj.mapId}-{obj.mapName}-metadata.json");
                if (File.Exists(existingFilePath))
                {
                    XRMap.MetadataFile metadataFile = JsonUtility.FromJson<XRMap.MetadataFile>(File.ReadAllText(existingFilePath));
                    obj.SetMetadata(metadataFile, true);
                    obj.ApplyAlignment();
                    yield break;
                }
            }
            
            // Download
            EditorCoroutineUtility.StartCoroutine(MapManager.DownloadMapMetadata(obj.mapId, result =>
            {
                obj.SetMetadata(result, false);
                obj.ApplyAlignment();
            }, force), this);
        }
        
        private IEnumerator MapAlignmentSave()
        {
            //
            // Updates map metadata to the Cloud Service and reloads to keep local files in sync
            //

            XRMap obj = (XRMap)target;
            sdk = ImmersalSDK.Instance;

            Vector3 pos = obj.transform.localPosition;
            Quaternion rot = obj.transform.localRotation;
            float scl = (obj.transform.localScale.x + obj.transform.localScale.y + obj.transform.localScale.z) / 3f; // Only uniform scale metadata is supported

            // IMPORTANT
            // Switching coordinate system handedness from Unity's left-handed system to Immersal Cloud Service's default right-handed system
            Matrix4x4 b = Matrix4x4.TRS(pos, rot, obj.transform.localScale);
            Matrix4x4 a = b.SwitchHandedness();
            pos = a.GetColumn(3);
            rot = a.rotation;

            // Update map alignment metadata to Immersal Cloud Service
            SDKMapAlignmentSetRequest r = new SDKMapAlignmentSetRequest();
            r.token = sdk.developerToken;
            r.id = obj.mapId;
            r.tx = pos.x;
            r.ty = pos.y;
            r.tz = pos.z;
            r.qx = rot.x;
            r.qy = rot.y;
            r.qz = rot.z;
            r.qw = rot.w;
            r.scale = scl;

            string jsonString = JsonUtility.ToJson(r);
            UnityWebRequest request = UnityWebRequest.Put(string.Format(ImmersalHttp.URL_FORMAT, ImmersalSDK.Instance.localizationServer, SDKMapAlignmentSetRequest.endpoint), jsonString);
            request.method = UnityWebRequest.kHttpVerbPOST;
            request.useHttpContinue = false;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SendWebRequest();

            while (!request.isDone)
            {
                yield return null;
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                ImmersalLogger.LogError($"Failed to save alignment for map id {obj.mapId}\n{request.error}");
            }
            else
            {
                SDKMapAlignmentSetResult result = JsonUtility.FromJson<SDKMapAlignmentSetResult>(request.downloadHandler.text);
                if (result.error == "none")
                {
                    // Reload the metadata from Immersal Cloud Service to keep local files in sync
                    EditorCoroutineUtility.StartCoroutine(MapAlignmentLoad(true), this);
                }
            }
        }

        private IEnumerator MapAlignmentReset()
        {
            //
            // Reset map alignment to the original captured data and reload metadata from the Immersal Cloud Service to keep local files in sync
            //

            XRMap obj = (XRMap)target;
            sdk = ImmersalSDK.Instance;

            // Reset alignment on Immersal Cloud Service
            SDKMapAlignmentResetRequest r = new SDKMapAlignmentResetRequest();
            r.token = sdk.developerToken;
            r.id = obj.mapId;

            string jsonString = JsonUtility.ToJson(r);
            UnityWebRequest request = UnityWebRequest.Put(string.Format(ImmersalHttp.URL_FORMAT, ImmersalSDK.Instance.localizationServer, SDKMapAlignmentResetRequest.endpoint), jsonString);
            request.method = UnityWebRequest.kHttpVerbPOST;
            request.useHttpContinue = false;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SendWebRequest();

            while (!request.isDone)
            {
                yield return null;
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                ImmersalLogger.LogError($"Failed to reset alignment for map id {obj.mapId}\n{request.error}");
            }
            else
            {
                SDKMapAlignmentResetResult result = JsonUtility.FromJson<SDKMapAlignmentResetResult>(request.downloadHandler.text);
                if (result.error == "none")
                {
                    // Reload the metadata from Immersal Cloud Service to keep local files in sync
                    EditorCoroutineUtility.StartCoroutine(MapAlignmentLoad(true), this);
                }
            }
        }
        #endregion'
    }
}
#endif