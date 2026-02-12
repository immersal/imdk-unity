/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Immersal.REST;
using UnityEngine;

namespace Immersal.XR
{
    [Serializable]
    public class DeviceLocalization : MonoBehaviour, ILocalizationMethod
    {
        // Note:
        // Custom editor does not draw default inspector
        
        [SerializeField]
        private ConfigurationMode m_ConfigurationMode = ConfigurationMode.Always;
        
        [SerializeField]
        private SolverType m_SolverType = SolverType.Default;

        [SerializeField]
        private int m_PriorNNCountMin = 60;

        [SerializeField]
        private int m_PriorNNCountMax = 720;

	    [SerializeField]
	    private Vector3 m_PriorScale = Vector3.one;
        
        [SerializeField]
        private float m_PriorRadius = 6.0f;

        [SerializeField]
        private float m_FilterRadius = 0f;
        
        public ConfigurationMode ConfigurationMode => m_ConfigurationMode;

        public IMapOption[] MapOptions => new IMapOption[]
        {
            new MapLoadingOption()
        };
        
        private SDKMapId[] m_MapIds;

        private int m_previouslyLocalizedMapId = 0;

        public Task<bool> Configure(ILocalizationMethodConfiguration configuration)
        {
            m_SolverType = configuration.SolverType ?? m_SolverType;
            m_PriorNNCountMin = configuration.PriorNNCountMin ?? m_PriorNNCountMin;
            m_PriorNNCountMax = configuration.PriorNNCountMax ?? m_PriorNNCountMax;
            m_PriorScale = configuration.PriorScale ?? m_PriorScale;
            m_PriorRadius = configuration.PriorRadius ?? m_PriorRadius;
            m_FilterRadius = configuration.FilterRadius ?? m_FilterRadius;
            
            List<SDKMapId> mapList = m_MapIds != null ? m_MapIds.ToList() : new List<SDKMapId>();
            
            // Add maps
            if (configuration.MapsToAdd != null)
            {
                foreach (XRMap map in configuration.MapsToAdd)
                {
                    mapList.Add(new SDKMapId {id = map.mapId});
                }
            }
	        
            // Remove maps
            if (configuration.MapsToRemove != null)
            {
                foreach (XRMap map in configuration.MapsToRemove)
                {
                    mapList.Remove(new SDKMapId {id = map.mapId});
                }
		        
                // Check if there are no configured maps left
                if (mapList.Count == 0)
                {
                    m_MapIds = Array.Empty<SDKMapId>();
                    return Task.FromResult(false);
                }
            }
	        
            m_MapIds = mapList.ToArray();
            return Task.FromResult(true);
        }

        public async Task<ILocalizationResult> Localize(ICameraData cameraData, CancellationToken cancellationToken)
        {
            LocalizationResult r = new LocalizationResult();

            using IImageData imageData = cameraData.GetImageData();
            
            float startTime = Time.realtimeSinceStartup;

            LocalizeInfo locInfo;
            
            if (m_SolverType == SolverType.Prior &&
                m_previouslyLocalizedMapId != 0 &&
                MapManager.TryGetMapEntry(m_previouslyLocalizedMapId, out MapEntry entry))
            {
                    Vector3 pos = cameraData.CameraPositionOnCapture;
                    Matrix4x4 mapPoseWithRelation = entry.SceneParent.ToMapSpace(pos, Quaternion.identity);
                    Vector3 priorPos = entry.Relation.ApplyInverseRelation(mapPoseWithRelation).GetPosition();
                    priorPos.SwitchHandedness();
                    locInfo = await Task.Run(() => Immersal.Core.LocalizeImageWithPrior(cameraData, imageData.UnmanagedDataPointer, ref priorPos, ref m_PriorScale, m_PriorNNCountMin, m_PriorNNCountMax, m_PriorRadius, m_FilterRadius), cancellationToken);
            }
            else
            {
                // previously localized map not found, reset
                m_previouslyLocalizedMapId = 0;
                locInfo = await Task.Run(() => Immersal.Core.LocalizeImage(cameraData, imageData.UnmanagedDataPointer, (int)m_SolverType), cancellationToken);
            }
            
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            if (locInfo.mapId > 0)
            {
                r.Success = true;
                r.MapId = locInfo.mapId;
                r.LocalizeInfo = locInfo;
                m_previouslyLocalizedMapId = locInfo.mapId;
			        
                ImmersalLogger.Log($"Relocalized in {elapsedTime} seconds");
            }
            else
            {
                r.Success = false;

                ImmersalLogger.Log($"Localization attempt failed after {elapsedTime} seconds");
            }

            return r;
        }
 
        public Task StopAndCleanUp()
        {
            // This implementation has nothing to clean up
            return Task.CompletedTask;
        }

        public Task OnMapRegistered(XRMap map)
        {
            // Ensure there is some map loading configuration
            MapLoadingOption mlo = map.MapOptions.FirstOrDefault(option => option.Name == "MapLoading") as MapLoadingOption;
            if (mlo == null)
            {
                ImmersalLogger.LogWarning($"Map {map.mapName} is missing DataSource option, attempting to deduce intended configuration.");
                mlo = new MapLoadingOption
                {
                    m_SerializedDataSource = map.mapFile == null ? 1 : 0, // Download mapfile if not found
                    DownloadVisualizationAtRuntime = map.Visualization == null // Download vis if not found
                };
                map.MapOptions.Add(mlo);
            }

            return Task.CompletedTask;
        }
    }
}