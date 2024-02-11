/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Immersal.REST;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Immersal.XR
{
    public static class MapManager
    {
        private static Dictionary<int, MapEntry> m_MapEntries = new Dictionary<int, MapEntry>();
        
        public static async Task RegisterAndLoadMap(XRMap map, ISceneUpdateable sceneParent)
        {
            if (!map.IsConfigured)
            {
                ImmersalLogger.LogError($"Trying to register and load unconfigured map: {map.name}");
                return;
            }

            if (map.LocalizationMethod == null)
            {
                ImmersalLogger.LogError($"Map {map.mapName} has null localization method.");
                return;
            }
            
            if (!TryGetMapEntry(map.mapId, out MapEntry entry))
            {
                RegisterMap(map, sceneParent);
            }

            await map.LocalizationMethod.OnMapRegistered(map);

            await LoadMap(map);
         
            ImmersalLogger.Log($"RegisterAndLoad for map {map.mapId} complete.");
        }
        
        // The LoadMap method checks if the map has a MapLoadingOption and loads the map
        // into the Immersal plugin based on the configuration.
        // Note: not all maps require loading (ServerLocalization for example)
        public static async Task LoadMap(XRMap map)
        {
            MapLoadingOption mlo = map.MapOptions.FirstOrDefault(option => option.Name == "MapLoading") as MapLoadingOption;
            if (mlo == null)
            {
                ImmersalLogger.Log($"No MapLoadingOption on map {map.mapId}, skipping loading.");
                // No MapLoadingOption -> assume map does not need to be loaded
                return;
            }
            
            ImmersalLogger.Log($"Loading map {map.mapId} according to MapLoadingOption.");
            
            // Download visualization?
            if (mlo.DownloadVisualizationAtRuntime)
            {
                ImmersalLogger.Log($"Downloading visualization for map {map.mapId}");
                JobLoadMapSparseAsync j = new JobLoadMapSparseAsync();
                j.id = map.mapId;
                SDKSparseDownloadResult plyResult = await j.RunJobAsync();
                ImmersalLogger.Log($"Adding visualization for map {map.mapId}");
                map.CreateVisualization(XRMapVisualization.RenderMode.EditorAndRuntime, true);
                map.Visualization.LoadPly(plyResult.data, map.mapName);
            }
            
            // Load raw bytes. This option is not exposed in the inspector, only for runtime configs.
            if (mlo.Bytes is { Length: > 0 }) 
            {
                ImmersalLogger.Log($"Loading provided bytes for: {map.mapId}");
                await TryToLoadMap(mlo.Bytes, map.mapId);
            }
            // Download map data
            else if (mlo.m_SerializedDataSource == (int)MapDataSource.Download)
            {
                ImmersalLogger.Log($"Map {map.mapId} configured to download data.");
                
                ImmersalLogger.Log($"Downloading mapfile for map {map.mapId}");
                JobLoadMapBinaryAsync j = new JobLoadMapBinaryAsync();
                j.id = map.mapId;

                SDKMapResult result = await j.RunJobAsync();
                ImmersalLogger.Log($"Loading downloaded mapfile for map {map.mapId}");
                await TryToLoadMap(result.mapData, map.mapId);
            }
            // Use embedded mapfile
            else if (mlo.m_SerializedDataSource == (int)MapDataSource.Embed)
            {
                if (map.mapFile == null)
                {
                    ImmersalLogger.LogError($"Missing map file for: {map.mapId}.");
                    return;
                }
                ImmersalLogger.Log($"Loading embedded mapfile for: {map.mapId}");
                byte[] mapBytes = map.mapFile.bytes;
                await TryToLoadMap(mapBytes, map.mapId);
            }
            else
            {
                ImmersalLogger.LogError("Unexpected DataSource configuration");
            }
        }

        public static async Task TryToLoadMap(byte[] mapBytes, int mapId)
        {
            // Check if map is registered
            if (TryGetMapEntry(mapId, out MapEntry entry))
            {
                if (mapBytes != null)
                {
                    Task<int> t = Task.Run(() => { return Immersal.Core.LoadMap(mapId, mapBytes); });
                    await t;
                }
            }
            else
            {
                ImmersalLogger.LogError($"Trying to load unregistered map ID: {mapId}");
            }
        }

        public static async Task<MapCreationResult> TryCreateMap(MapCreationParameters parameters, bool unconfigured = false)
        {
            // Default to failure
            MapCreationResult result = new MapCreationResult { Success = false };

            // GameObject
            parameters.Name = string.IsNullOrEmpty(parameters.Name) ? "New map" : parameters.Name;
            GameObject go = new GameObject(parameters.Name);
            
            // XRMap component
            XRMap map = go.AddComponent<XRMap>();
            result.Map = map;
            
            // SceneParent
            if (parameters.SceneParent == null)
            {
                GameObject newParent = new GameObject("New XR Space");
                parameters.SceneParent = newParent.AddComponent<XRSpace>();
            }
            
            go.transform.SetParent(parameters.SceneParent.GetTransform());
            result.SceneParent = parameters.SceneParent;
            
            // Localization method
            if (parameters.LocalizationMethod == null)
            {
                ILocalizationMethod[] availableMethods = AvailableLocalizationMethods;
                ILocalizationMethod method =
                    availableMethods?.FirstOrDefault(m => m.GetType() == parameters.LocalizationMethodType);
                if (method == null)
                    return result; // fail
                parameters.LocalizationMethod = method;
            }
            
            if (!await parameters.LocalizationMethod.Configure(new XRMap[] { map }))
                return result; // fail
            
            map.LocalizationMethod = parameters.LocalizationMethod;
            
            // Map options
            if (parameters.MapOptions != null)
            {
                map.MapOptions = parameters.MapOptions.ToList();
            }

            // Optionally return here and leave out configuration, registering and loading
            if (unconfigured)
            {
                result.Success = true;
                return result;
            }
            
            // Configure
            if (parameters.MapId != null)
            {
                map.SetIdAndName(parameters.MapId.Value, parameters.Name, true);
                map.Configure();
            }
            else
            {
                if (parameters.MetadataGetResult == null)
                    return result; // fail
                map.Configure(parameters.MetadataGetResult.Value);
            }

            // Register and load
            await RegisterAndLoadMap(map, parameters.SceneParent);

            result.Success = true;
            return result;
        }
        
        public static List<XRMap> GetRegisteredMaps()
        {
            List<XRMap> maps = new List<XRMap>();
            foreach (KeyValuePair<int,MapEntry> keyValuePair in m_MapEntries)
            {
                maps.Add(keyValuePair.Value.Map);
            }

            return maps;
        }

        public static int GetRegisteredMapCount()
        {
            return m_MapEntries.Count;
        }

        public static List<ISceneUpdateable> GetSceneUpdateablesInUse()
        {
            List<ISceneUpdateable> updateables = new List<ISceneUpdateable>();
            foreach (KeyValuePair<int,MapEntry> keyValuePair in m_MapEntries)
            {
                ISceneUpdateable sceneUpdateable = keyValuePair.Value.SceneParent;
                if (!updateables.Contains(sceneUpdateable ))
                    updateables.Add(sceneUpdateable);
            }

            return updateables;
        }
        
        #region MapEntries
        
        public static void RegisterMap(XRMap map, ISceneUpdateable sceneParent)
        {
            ImmersalLogger.Log($"Registering map: {map.mapId}");  
            
            if (m_MapEntries.ContainsKey(map.mapId))
            {
                ImmersalLogger.LogWarning("Map is already registered, aborting.");
                return;
            }
            
            Transform tr = map.transform;
            MapToSpaceRelation mtsr = new MapToSpaceRelation
            {
                Position = tr.localPosition,
                Rotation = tr.localRotation,
                Scale = tr.localScale
            };

            MapEntry me = new MapEntry
            {
                Map = map,
                SceneParent = sceneParent,
                Relation = mtsr
            };
            
            m_MapEntries.Add(map.mapId, me);
        }
        
        public static void RemoveMap(int mapId, bool destroyObjects = false)
        {
            if (TryGetMapEntry(mapId, out MapEntry entry))
            {
                // free & remove mapping
                Immersal.Core.FreeMap(mapId);
                
                // Destroy
                if (destroyObjects)
                {
                    XRMap map = entry.Map;
                    
                    if (map.Visualization != null)
                        map.RemoveVisualization();
                    
                    GameObject.Destroy(map.gameObject);
                }
                
                // remove entry
                m_MapEntries.Remove(mapId);
            }
        }

        public static void RemoveAllMaps(bool destroyObjects = false)
        {
            foreach (int id in m_MapEntries.Keys.ToList())
            {
                RemoveMap(id, destroyObjects);
            }
        }

        public static bool TryGetMapEntry(int mapId, out MapEntry mapEntry)
        {
            return m_MapEntries.TryGetValue(mapId, out mapEntry);
        }

        #endregion

        #region Localization methods and map options
        
        /*
         * This section takes care of managing a collection of available localization methods.
         */

        private static ILocalizationMethod[] m_CachedLocalizationMethods;

        public static ILocalizationMethod[] AvailableLocalizationMethods
        {
            get
            {
                if (m_CachedLocalizationMethods == null)
                {
                    RefreshLocalizationMethods();
                }
                return m_CachedLocalizationMethods;
            }
        }

        // To ensure our cached collections are up to date and pointing to the correct instances,
        // we need to react to both assembly reloads and scene changes.
        
#if UNITY_EDITOR
        static MapManager()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            InvalidateCache();
        }

        [InitializeOnLoadMethod]
        public static void ReactToAssemblyReload()
        {
            InvalidateCache();
        }
#endif

        private static void InvalidateCache()
        {
            m_CachedLocalizationMethods = null;
        }

        public static void RefreshLocalizationMethods()
        {
            if (ImmersalSDK.Instance == null || ImmersalSDK.Instance.Localizer == null)
            {
                ImmersalLogger.LogError("Missing ImmersalSDK or Localizer, cant fetch localization methods.");
                return;
            }
            
            ILocalizationMethod[] methods = ImmersalSDK.Instance.Localizer.AvailableLocalizationMethods;

            if (methods == null)
            {
                ImmersalLogger.LogError("No localization methods defined in Localizer");
                return;
            }
            
            // Check for duplicates
            HashSet<ILocalizationMethod> uniqueMethods = new HashSet<ILocalizationMethod>();
            foreach (ILocalizationMethod method in methods)
            {
                if (!uniqueMethods.Add(method))
                {
                    // Duplicate found
                    ImmersalLogger.LogWarning("Localizer has duplicate Localization Method references.");
                    continue;
                }
            }

            m_CachedLocalizationMethods = uniqueMethods.ToArray();
        }
        
        public static bool TryGetMapOptions(ILocalizationMethod localizationMethod, out List<IMapOption> options)
        {
            options = new List<IMapOption>();
            
            if (localizationMethod.IsNullOrDead())
            {
                ImmersalLogger.LogError("Trying to fetch map options for null localization method.");
                return false;
            }

            IMapOption[] mapOptionsFromMethod = localizationMethod.MapOptions;
            if (mapOptionsFromMethod == null)
                return false;
            
            options = mapOptionsFromMethod.ToList();

            return true;
        }

        #endregion
        
        #region Downloading coroutines for edit time use
#if UNITY_EDITOR
        public static string GetDirectoryPath(string inputPath = "", bool assetsRoot = false)
        {
            string result = "";
            string root = assetsRoot ? "Assets/" : Application.dataPath;
            string defaultPath = Path.Combine(root, ImmersalSDK.Instance.DownloadDirectory);
            result = inputPath != "" ? Path.Combine(root, inputPath) : defaultPath;
            return result;
        }

        public static bool CheckDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                ImmersalLogger.LogWarning("Requested directory does not exist, creating it now.");
                Directory.CreateDirectory(path);
            }
            
            if (string.IsNullOrEmpty(path))
            {
                ImmersalLogger.LogError("Requested path is null or empty.");
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
            }
            catch
            {
                ImmersalLogger.LogError($"Requested path is invalid: {path}");
                return false;
            }

            return true;
        }
        

        public static IEnumerator DownloadMapMetadata(int mapId, Action<SDKMapMetadataGetResult> resultCallback, string jsonWritePath = "")
        {
            string targetFullPath = GetDirectoryPath(jsonWritePath);
            if (!CheckDirectory(targetFullPath))
                yield break;
            
            // Load map metadata from Immersal Cloud Service
            SDKMapMetadataGetRequest r = new SDKMapMetadataGetRequest();
            r.token = ImmersalSDK.Instance.developerToken;
            r.id = mapId;
            
            if (r.token == "")
                ImmersalLogger.LogWarning("Trying to download map data without developer token.");

            string jsonString = JsonUtility.ToJson(r);
            UnityWebRequest request =
                UnityWebRequest.Put(
                    string.Format(ImmersalHttp.URL_FORMAT, ImmersalSDK.Instance.localizationServer,
                        SDKMapMetadataGetRequest.endpoint), jsonString);
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
                ImmersalLogger.LogError(request.error);
            }
            else
            {
                SDKMapMetadataGetResult result =
                    JsonUtility.FromJson<SDKMapMetadataGetResult>(request.downloadHandler.text);
                if (result.error == "none")
                {
                    resultCallback(result);

                    string fileName = $"{result.id}-{result.name}-metadata.json";
                    string jsonFilePath = Path.Combine(targetFullPath, fileName);
                    WriteJson(jsonFilePath, request.downloadHandler.text);
                    string assetPath = Path.Combine(GetDirectoryPath(jsonWritePath, true), fileName);
                    AssetDatabase.Refresh();
                    AssetDatabase.ImportAsset(assetPath);
                }
            }
        }
        
        public static IEnumerator DownloadMapFile(int mapId, string mapName, Action<SDKMapDownloadResult, TextAsset> resultCallback, string bytesWritePath = "")
        {
            string targetFullPath = GetDirectoryPath(bytesWritePath);
            string targetAssetPath = GetDirectoryPath(bytesWritePath, true);
            if (!CheckDirectory(targetFullPath))
                yield break;
            
            // Load map file from Immersal Cloud Service
            SDKMapDownloadRequest r = new SDKMapDownloadRequest();
            r.token = ImmersalSDK.Instance.developerToken;
            r.id = mapId;
            
            if (r.token == "")
                ImmersalLogger.LogWarning("Trying to download map data without developer token.");

            string jsonString = JsonUtility.ToJson(r);
            UnityWebRequest request = UnityWebRequest.Put(string.Format(ImmersalHttp.URL_FORMAT, ImmersalSDK.Instance.localizationServer, SDKMapDownloadRequest.endpoint), jsonString);
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
                ImmersalLogger.LogError(request.error);
            }
            else
            {
                SDKMapDownloadResult mapDataResult = JsonUtility.FromJson<SDKMapDownloadResult>(request.downloadHandler.text);
                if (mapDataResult.error == "none")
                {
                    // Save map file on disk, overwrite existing file
                    string fileName = $"{mapId}-{mapName}.bytes";
                    string mapFileFullPath = Path.Combine(targetFullPath, fileName);
                    string mapFileAssetPath = Path.Combine(GetDirectoryPath(bytesWritePath, true), fileName);
                    WriteBytes(mapFileFullPath, mapDataResult.b64);
                    AssetDatabase.Refresh();
                    AssetDatabase.ImportAsset(mapFileAssetPath);
                    TextAsset mapFile =
                        (TextAsset)AssetDatabase.LoadAssetAtPath(mapFileAssetPath, typeof(TextAsset));
                    resultCallback(mapDataResult, mapFile);
                }
            }
        }
        
        public static IEnumerator DownloadSparseFile(int mapId, string mapName, Action<SDKSparseDownloadResult, string> resultCallback, string plyWritePath = "")
        {
            string targetFullPath = GetDirectoryPath(plyWritePath);
            if (!CheckDirectory(targetFullPath))
                yield break;
            
            string uri =
                $"{ImmersalSDK.Instance.localizationServer}/{SDKSparseDownloadRequest.endpoint}?token={ImmersalSDK.Instance.developerToken}&id={mapId}";

            if (ImmersalSDK.Instance.developerToken == "")
                ImmersalLogger.LogWarning("Trying to download map data without developer token.");
            
            using (UnityWebRequest request = UnityWebRequest.Get(uri))
            {
                // Request and wait for completion
                yield return request.SendWebRequest();
                
#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    ImmersalLogger.LogError(request.error);
                }
                else
                {
                    SDKSparseDownloadResult plyDataResult = new SDKSparseDownloadResult();
                    plyDataResult.data = request.downloadHandler.data;
                    plyDataResult.error = request.error;
                    if (plyDataResult.error == null)
                    {
                        // Save map file on disk, overwrite existing file
                        string fileName = $"{mapId}-{mapName}-sparse.ply";
                        string plyFilePath = Path.Combine(targetFullPath, fileName);
                        WritePly(plyFilePath, plyDataResult.data);
                        resultCallback(plyDataResult, plyFilePath);
                    }
                }
            }
        }

        public static void WriteJson(string jsonFilepath, string data, bool overwrite = false)
        {
            if (File.Exists(jsonFilepath))
            {
                if (!overwrite) return;
                File.Delete(jsonFilepath);
            }

            File.WriteAllText(jsonFilepath, data);
        }

        public static void WriteBytes(string mapFilepath, string b64, bool overwrite = false)
        {
            if (File.Exists(mapFilepath))
            {
                if (!overwrite) return;
                File.Delete(mapFilepath);
            }
            byte[] data = Convert.FromBase64String(b64);
            File.WriteAllBytes(mapFilepath, data);
        }

        public static void WritePly(string plyFilepath, byte[] data, bool overwrite = false)
        {
            if (File.Exists(plyFilepath))
            {
                if (!overwrite) return;
                File.Delete(plyFilepath);
            }
            File.WriteAllBytes(plyFilepath, data);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(plyFilepath);
        }

#endif
        #endregion
    }
    
    public enum MapDataSource
    {
        Embed = 0,      // use embedded mapfile
        Download = 1    // let MapManager download mapfile at runtime
    }

    [Serializable]
    public class MapLoadingOption : IMapOption
    {
        public string Name => "MapLoading";

        [SerializeField]
        public int m_SerializedDataSource = 0;

        private MapDataSource m_MapDataSource = MapDataSource.Embed;

        [SerializeField]
        public bool DownloadVisualizationAtRuntime = false;
        
        private TextAsset currentMapFile = null;
        private TextAsset prevMapFile = null;

        public byte[] Bytes;

        public void DrawEditorGUI(XRMap map)
        {
#if UNITY_EDITOR
            m_MapDataSource = (MapDataSource)m_SerializedDataSource;
            EditorGUI.BeginChangeCheck();
            m_MapDataSource = (MapDataSource)EditorGUILayout.EnumPopup("Map data source", m_MapDataSource);
            if (EditorGUI.EndChangeCheck())
            {
                m_SerializedDataSource = (int)m_MapDataSource;
            }
            if (m_SerializedDataSource == 0)
            {
                currentMapFile = prevMapFile = map.mapFile;
                EditorGUI.BeginChangeCheck();
                currentMapFile =
                    (TextAsset)EditorGUILayout.ObjectField("Map file", currentMapFile, typeof(TextAsset), false);

                if (EditorGUI.EndChangeCheck())
                {
                    if (currentMapFile == null)
                        return;
                    
                    if (prevMapFile == null || currentMapFile != prevMapFile)
                    {
                        string bytesPath = AssetDatabase.GetAssetPath(currentMapFile);
                        if (bytesPath.EndsWith(".bytes"))
                        {
                            // Switch mapfile
                            prevMapFile = currentMapFile;
                            map.Uninitialize();
                            map.Configure(currentMapFile);
                            EditorUtility.SetDirty(map);
                            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        }
                        else
                        {
                            ImmersalLogger.LogError($"{AssetDatabase.GetAssetPath(currentMapFile)} is not a valid map file");
                            map.mapFile = prevMapFile;
                        }
                    }
                }
            }
            else
            {
                DownloadVisualizationAtRuntime =
                    EditorGUILayout.Toggle("Download visualization", DownloadVisualizationAtRuntime);
            }
#endif
        }
    }
    
    public class MapEntry
    {
        public XRMap Map;
        public ISceneUpdateable SceneParent;
        public MapToSpaceRelation Relation;
    }
    
    public class MapToSpaceRelation
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    public class MapCreationParameters
    {
        public int? MapId; // Either this
        public SDKMapMetadataGetResult? MetadataGetResult; // or this is necessary
        public String Name; // Optional
        public ISceneUpdateable SceneParent; // Optional
        public ILocalizationMethod LocalizationMethod; // Optional
        public Type LocalizationMethodType = typeof(DeviceLocalization); // Necessary if above is null
        public IMapOption[] MapOptions; // Optional
    }

    public class MapCreationResult
    {
        public bool Success;
        public XRMap Map;
        public ISceneUpdateable SceneParent;
    }
}
