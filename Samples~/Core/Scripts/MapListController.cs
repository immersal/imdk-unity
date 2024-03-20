/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Immersal.XR;
using Immersal.REST;

namespace Immersal.Samples
{
    public enum LocalizationMethodChoice
    {
        OnDevice,
        OnServer
    }
    
    [RequireComponent(typeof(TMP_Dropdown))]
    public class MapListController : MonoBehaviour
    {
        [SerializeField]
        private LocalizationMethodChoice m_LocMethodChoice = LocalizationMethodChoice.OnDevice; 
        
        private List<SDKJob> m_Maps;
        private TMP_Dropdown m_Dropdown;
        private List<IJobAsync> m_Jobs = new List<IJobAsync>();
        private int m_JobLock = 0;

        private MapLoadingOption m_MLO = new MapLoadingOption { DownloadVisualizationAtRuntime = true, m_SerializedDataSource = (int)MapDataSource.Download};

        void Start()
        {
            m_Dropdown = GetComponent<TMP_Dropdown>();
            m_Dropdown.ClearOptions();
            m_Dropdown.AddOptions( new List<string>() { "Load map..." });
            
            m_MLO.m_SerializedDataSource = m_LocMethodChoice == LocalizationMethodChoice.OnDevice
                ? (int)MapDataSource.Download
                : -1;

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
            if (value >= 0)
            {
                SDKJob map = m_Maps[value];
                LoadMap(map);
            }
        }

        public void GetMaps()
        {
            JobListJobsAsync j = new JobListJobsAsync();
            j.token = ImmersalSDK.Instance.developerToken;
            j.OnResult += (SDKJobsResult result) =>
            {
                if (result.count <= 0) return;
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
            };

            m_Jobs.Add(j);
        }

        public void ClearMaps()
        {
            MapManager.RemoveAllMaps(true, true);
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
                    LocalizationMethodType = m_LocMethodChoice == LocalizationMethodChoice.OnDevice
                        ? typeof(DeviceLocalization)
                        : typeof(ServerLocalization),
                    MapOptions = new IMapOption[] { m_MLO }
                };

                MapCreationResult r = await MapManager.TryCreateMap(parameters);
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
