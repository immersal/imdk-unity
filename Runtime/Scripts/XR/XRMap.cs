/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Immersal.REST;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
    public class XRMap : MonoBehaviour, ISerializationCallbackReceiver
    {
        // Note: All properties are configured via the custom editor
        // The editor does not render the default inspector view,
        // so none of these will show up without explicitly adding to the editor
        
        [SerializeField]
        public TextAsset mapFile;
        
        [SerializeField]
        private int m_MapId = -1;
        
        [SerializeField]
        private string m_MapName = null;

        public int privacy;
        public MapAlignment mapAlignment;
        public WGS84 wgs84;

        [System.Serializable]
        public struct MetadataFile
        {
            public string error;
            public int id;
            public int type;
            public string created;
            public string version;
            public int user;
            public int creator;
            public string name;
            public int size;
            public string status;
            public int privacy;
            public double latitude;
            public double longitude;
            public double altitude;
            public double tx;
            public double ty;
            public double tz;
            public double qw;
            public double qx;
            public double qy;
            public double qz;
            public double scale;
            public string sha256_al;
            public string sha256_sparse;
            public string sha256_dense;
            public string sha256_tex;
        }
        
        [System.Serializable]
        public struct MapAlignment
        {
            public double tx;
            public double ty;
            public double tz;
            public double qx;
            public double qy;
            public double qz;
            public double qw;
            public double scale;
        }

        [System.Serializable]
        public struct WGS84
        {
            public double latitude;
            public double longitude;
            public double altitude;
        }

        [SerializeField]
        public bool IsConfigured = false;

        [SerializeField]
        public XRMapVisualization Visualization;

        public int mapId
        {
            get => m_MapId;
            private set => m_MapId = value;
        }

        public string mapName
        {
            get => m_MapName;
            set => m_MapName = value;
        }
        
        // Localization method
        [SerializeField]
        private Object m_LocalizationMethodObject;
        public ILocalizationMethod LocalizationMethod
        {
            get => m_LocalizationMethodObject as ILocalizationMethod;
            set => m_LocalizationMethodObject = value as Object;
        }

        #region Map options
    
        /*
         * Map options are custom configurations specific to individual ILocalizationMethods.
         * They are defined in the ILocalizationMethod implementation and registered in MapManager.
         * They require custom serialization via the SerializableMapOption class.
         */
        
        [SerializeField]
        private List<SerializableMapOption> m_SerializedMapOptions = new List<SerializableMapOption>();

        public List<IMapOption> MapOptions = new List<IMapOption>();

#if UNITY_EDITOR
        public void UpdateMapOptions()
        {
            MapOptions = new List<IMapOption>();
            
            if (MapManager.TryGetMapOptions(LocalizationMethod, out List<IMapOption> options))
            {
                MapOptions = options;
            }
            
            // force serialization
            SerializeMapOptions();
        }
#endif

        public void SerializeMapOptions()
        {
            m_SerializedMapOptions.Clear();
            foreach (IMapOption mapOption in MapOptions)
            {
                m_SerializedMapOptions.Add(SerializableMapOption.Serialize(mapOption));
            }
        }
        public void DeserializeMapOptions()
        {
            MapOptions.Clear();
            foreach (SerializableMapOption serializedOption in m_SerializedMapOptions)
            {
                IMapOption option = serializedOption.Deserialize();
                if (option != null)
                {
                    MapOptions.Add(option);
                }
            }
        }
        
        // Unity serialization callbacks
        
        public void OnBeforeSerialize()
        {
            SerializeMapOptions();
        }

        public void OnAfterDeserialize()
        {
            DeserializeMapOptions();
        }

        private void Awake()
        {
            // Ensure we have deserialized options available at runtime
            DeserializeMapOptions();
        }

        #endregion 

        public double[] MapToEcefGet()
        {
            double[] m = mapAlignment.QuaternionsToDoubleMatrix3x3();

            double[] mapToEcef = new double[] {this.mapAlignment.tx, this.mapAlignment.ty, this.mapAlignment.tz, m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], this.mapAlignment.scale};

            return mapToEcef;
        }

        public void Uninitialize()
        {
            ClearBytesReference();
            if (Visualization != null)
                RemoveVisualization();
            IsConfigured = false;
        }

        public void Configure(SDKMapMetadataGetResult result, bool setIdAndName = true)
        {
            SetMetadata(result, setIdAndName);
            Configure();
        }

        public void Configure(TextAsset mapFile = null)
        {
            if (mapFile != null)
            {
                this.mapFile = mapFile;
                ParseMapFiles();
            }
            IsConfigured = true;
        }

        public void CreateVisualization(XRMapVisualization.RenderMode renderMode = XRMapVisualization.RenderMode.EditorOnly, bool randomColor = false)
        {
            Color c = randomColor ? XRMapVisualization.pointCloudColors[UnityEngine.Random.Range(0, XRMapVisualization.pointCloudColors.Length)]
                : new Color(0.57f, 0.93f, 0.12f);
            CreateVisualization(c, renderMode);
        }

        public void CreateVisualization(Color pointColor, XRMapVisualization.RenderMode renderMode)
        {
            if (Visualization != null)
            {
                RemoveVisualization();
            }

            GameObject go = new GameObject($"{mapId}-{mapName}-vis");
            go.transform.SetParent(transform, false);

            Visualization = go.AddComponent<XRMapVisualization>();
            Visualization.Initialize(this, renderMode, pointColor);
        }

        public void RemoveVisualization()
        {
            Visualization.ClearVisualization();
            DestroyImmediate(Visualization.gameObject);
            Visualization = null;
        }

        private void ClearBytesReference()
        {
            mapFile = null;
        }
        
        public void ApplyAlignment()
        {
            Vector3 posMetadata = new Vector3((float)mapAlignment.tx, (float)mapAlignment.ty, (float)mapAlignment.tz);
            Quaternion rotMetadata = new Quaternion((float)mapAlignment.qx, (float)mapAlignment.qy, (float)mapAlignment.qz, (float)mapAlignment.qw);
            float scaleMetadata = (float)mapAlignment.scale; // Only uniform scale metadata is supported

            // IMPORTANT
            // Switch coordinate system handedness back from Immersal Cloud Service's default right-handed system to Unity's left-handed system
            Matrix4x4 b = Matrix4x4.TRS(posMetadata, rotMetadata, new Vector3(scaleMetadata, scaleMetadata, scaleMetadata));
            Matrix4x4 a = b.SwitchHandedness();
            Vector3 pos = a.GetColumn(3);
            Quaternion rot = a.rotation;
            Vector3 scl = new Vector3(scaleMetadata, scaleMetadata, scaleMetadata); // Only uniform scale metadata is supported

            // Set XR Map local transform from the converted metadata
            transform.localPosition = pos;
            transform.localRotation = rot;
            transform.localScale = scl;
        }
        

        public void ParseMapFiles()
        {
            int id = -1;
            if (GetMapId(out id))
            {
                SetIdAndName(id, mapFile.name.Substring(id.ToString().Length + 1), true);
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (mapFile != null)
                {
                    try
                    {
                        string mapFilePath = AssetDatabase.GetAssetPath(mapFile);
                        string mapFileDir = Path.GetDirectoryName(mapFilePath);
                        string jsonFilePath = Path.Combine(mapFileDir, string.Format("{0}-metadata.json", mapFile.name));
                        MetadataFile metadataFile = JsonUtility.FromJson<MetadataFile>(File.ReadAllText(jsonFilePath));
                        SetMetadata(metadataFile);
                    }
                    catch (FileNotFoundException e)
                    {
                        ImmersalLogger.LogWarning($"{e.Message}\nCould not find {mapFile.name}-metadata.json");
                        
                        // set default values in case metadata is not available

                        mapAlignment.tx = 0.0;
                        mapAlignment.ty = 0.0;
                        mapAlignment.tz = 0.0;

                        mapAlignment.qx = 0.0;
                        mapAlignment.qy = 0.0;
                        mapAlignment.qz = 0.0;
                        mapAlignment.qw = 1.0;

                        mapAlignment.scale = 1.0;

                        wgs84.latitude = 0.0;
                        wgs84.longitude = 0.0;
                        wgs84.altitude = 0.0;

                        privacy = 0;
                    }
                }
            }
#endif
        }

        public void SetMetadata(SDKMapMetadataGetResult result, bool setIdAndName = false)
        {
            mapAlignment.tx = result.tx;
            mapAlignment.ty = result.ty;
            mapAlignment.tz = result.tz;
            mapAlignment.qx = result.qx;
            mapAlignment.qy = result.qy;
            mapAlignment.qz = result.qz;
            mapAlignment.qw = result.qw;
            mapAlignment.scale = result.scale;

            wgs84.latitude = result.latitude;
            wgs84.longitude = result.longitude;
            wgs84.altitude = result.altitude;

            privacy = result.privacy;
            
            if (setIdAndName)
            {
                SetIdAndName(result.id, result.name, true);
            }
        }

        public void SetMetadata(MetadataFile metadataFile, bool setIdAndName = false)
        {
            mapAlignment.tx = metadataFile.tx;
            mapAlignment.ty = metadataFile.ty;
            mapAlignment.tz = metadataFile.tz;

            mapAlignment.qx = metadataFile.qx;
            mapAlignment.qy = metadataFile.qy;
            mapAlignment.qz = metadataFile.qz;
            mapAlignment.qw = metadataFile.qw;

            mapAlignment.scale = metadataFile.scale;
                        
            wgs84.latitude = metadataFile.latitude;
            wgs84.longitude = metadataFile.longitude;
            wgs84.altitude = metadataFile.altitude;
                        
            privacy = metadataFile.privacy;
            
            if (setIdAndName)
            {
                SetIdAndName(metadataFile.id, metadataFile.name, true);
            }
        }
        
        public void SetIdAndName(int mapId, string mapName, bool applyToGameObject = false)
        {
            this.mapId = mapId;
            this.mapName = mapName;
            if (applyToGameObject)
            {
                gameObject.name = string.Format("XR Map {0}-{1}", mapId, mapName);
            }
        }

        private bool GetMapId(out int parsedMapId)
        {
            if (mapFile == null)
            {
                parsedMapId = -1;
                return false;
            }

            string mapFileName = mapFile.name;
            Regex rx = new Regex(@"^\d+");
            Match match = rx.Match(mapFileName);
            if (match.Success)
            {
                parsedMapId = Int32.Parse(match.Value);
                return true;
            }
            else
            {
                parsedMapId = -1;
                return false;
            }
        }

        public bool PreBuildCheck(out string message)
        {
            message = "";
            
            if (!gameObject.activeInHierarchy || !enabled)
                return false;
            
            string logName = mapName;
            if (!IsConfigured)
            {
                ImmersalLogger.LogWarning($"XRMap on object {name} is unconfigured.");
                logName = name;
            }

            if (LocalizationMethod == null)
            {
                message = $"XRMap {logName} has null LocalizationMethod.";
                return false;
            }

            return true;
        }
    }
}