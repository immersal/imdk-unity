/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using Immersal.XR;
using Immersal.REST;
using UnityEngine.Serialization;

namespace Immersal.Samples
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class MapListController : MonoBehaviour
    {
        [SerializeField]
        private XRMap m_EmbeddedMap;
        
        private ISceneUpdateable m_EmbeddedSceneParent;
        private List<SDKJob> m_Maps;
        private TMP_Dropdown m_Dropdown;
        private List<IJobAsync> m_Jobs = new List<IJobAsync>();
        private int m_JobLock = 0;
        private TextAsset m_EmbeddedMapFile;
        private Mesh m_EmbeddedMesh;

        private MapLoadingOption m_MLO = new MapLoadingOption { DownloadVisualizationAtRuntime = true, m_SerializedDataSource = (int)MapDataSource.Download};

        void Start()
        {
            m_Dropdown = GetComponent<TMP_Dropdown>();
            m_Dropdown.ClearOptions();
            bool validEmbed = false;
            
            // check for embedded map
            if (m_EmbeddedMap != null && m_EmbeddedMap.IsConfigured)
            {
                // fetch scene parent
                if (MapManager.TryGetMapEntry(m_EmbeddedMap.mapId, out MapEntry entry))
                {
                    m_EmbeddedSceneParent = entry.SceneParent;
                }
                else
                {
                    Debug.LogError("Could not find map entry for embedded map");
                    enabled = false;
                    return;
                }

                if (m_EmbeddedMap.mapFile != null)
                {
                    m_Dropdown.AddOptions( new List<string>() { string.Format("<{0}>", m_EmbeddedMap.mapFile.name) });
                    m_EmbeddedMapFile = m_EmbeddedMap.mapFile;
                    m_MLO.m_SerializedDataSource = (int)MapDataSource.Embed;
                    if (m_EmbeddedMap.Visualization.Mesh != null)
                    {
                        m_EmbeddedMesh = m_EmbeddedMap.Visualization.Mesh;
                        m_MLO.DownloadVisualizationAtRuntime = false;
                    }
                    validEmbed = true;
                }
            }
            
            if (!validEmbed)
            {
                m_Dropdown.AddOptions( new List<string>() { "Load map..." });
            }

            m_Maps = new List<SDKJob>();
            
            Invoke("GetMaps", 0.5f);
        }

        void Update()
        {
            if (m_JobLock == 1)
                return;
            
            if (m_Jobs.Count > 0)
            {
                m_JobLock = 1;
                RunJob(m_Jobs[0]);
            }
        }

        public async void OnValueChanged(TMP_Dropdown dropdown)
        {
            int value = dropdown.value - 1;
            
            // Embedded map
            if (m_EmbeddedMapFile != null && value == -1)
            {
                // Remove current
                if (m_EmbeddedMap != null && m_EmbeddedMap.IsConfigured)
                {
                    MapManager.RemoveMap(m_EmbeddedMap.mapId);
                    if (m_EmbeddedMap.Visualization != null)
                        m_EmbeddedMap.RemoveVisualization();
                }
                
                m_EmbeddedMap.Configure(m_EmbeddedMapFile);
                
                // Vis
                if (m_EmbeddedMesh != null)
                {
                    m_EmbeddedMap.CreateVisualization();
                    m_EmbeddedMap.Visualization.SetMesh(m_EmbeddedMesh);
                }
                else
                {
                    m_MLO.DownloadVisualizationAtRuntime = true;
                }
                
                await MapManager.RegisterAndLoadMap(m_EmbeddedMap, m_EmbeddedSceneParent);
            }
            else
            {
                if (value >= 0)
                {
                    SDKJob map = m_Maps[value];
                    LoadMap(map);
                }
            }
        }

        public void GetMaps()
        {
            JobListJobsAsync j = new JobListJobsAsync();
            j.token = ImmersalSDK.Instance.developerToken;
            j.OnResult += (SDKJobsResult result) =>
            {
                if (result.count > 0)
                {
                    List<string> names = new List<string>();

                    foreach (SDKJob job in result.jobs)
                    {
                        if (job.type != (int)SDKJobType.Alignment && (job.status == SDKJobState.Sparse || job.status == SDKJobState.Done))
                        {
                            this.m_Maps.Add(job);
                            names.Add(job.name);
                        }
                    }

                    this.m_Dropdown.AddOptions(names);
                }
            };

            m_Jobs.Add(j);
        }

        public void ClearMaps()
        {
            MapManager.RemoveAllMaps(true);
            m_Dropdown.SetValueWithoutNotify(0);
        }

        private void LoadMap(SDKJob job)
        {
            JobMapMetadataGetAsync j = new JobMapMetadataGetAsync();
            j.id = job.id;
            j.OnResult += async (SDKMapMetadataGetResult result) =>
            {
                MapCreationParameters parameters = new MapCreationParameters
                {
                    MetadataGetResult = result,
                    LocalizationMethodType = typeof(DeviceLocalization),
                    MapOptions = new IMapOption[] { m_MLO }
                };

                MapCreationResult r = await MapManager.TryCreateMap(parameters);

                if (r.Success)
                {
                    m_EmbeddedMap = r.Map;
                    m_EmbeddedSceneParent = r.SceneParent;
                }
            };
            m_Jobs.Add(j);
        }

        private async void RunJob(IJobAsync j)
        {
            await j.RunJob();
            if (m_Jobs.Count > 0)
                m_Jobs.RemoveAt(0);
            m_JobLock = 0;
        }
    }
}
