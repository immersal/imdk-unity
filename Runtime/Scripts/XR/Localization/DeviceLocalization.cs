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
using UnityEditor;
using UnityEngine;

namespace Immersal.XR
{
    public enum SolverType
    {
        Default = 0,
        Lean = 1,
        Prior = 2
    };
    
    [Serializable]
    public class DeviceLocalization : MonoBehaviour, ILocalizationMethod
    {
        [SerializeField]
        private ConfigurationMode m_ConfigurationMode = ConfigurationMode.Always;
        
        [SerializeField]
        private SolverType m_SolverType = SolverType.Default;

        [SerializeField]
        private int m_PriorNNCount = 0;
        
        [SerializeField]
        private float m_PriorRadius = 0f;
        
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
            m_PriorNNCount = configuration.PriorNNCount ?? m_PriorNNCount;
            m_PriorRadius = configuration.PriorRadius ?? m_PriorRadius;
            
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
                    Vector3 priorPos = entry.SceneParent.ToMapSpace(pos, Quaternion.identity).GetPosition();
                    locInfo = await Task.Run(() => Immersal.Core.icvLocalizeImageWithPrior(cameraData, imageData.UnmanagedDataPointer, ref priorPos, m_PriorNNCount, m_PriorRadius), cancellationToken);
            }
            else
            {
                // previously localized map not found, reset
                m_previouslyLocalizedMapId = 0;
                locInfo = await Task.Run(() => Immersal.Core.LocalizeImage(cameraData, imageData.UnmanagedDataPointer,(int)m_SolverType), cancellationToken);
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

        public void SetSolverType(SolverType newSolverType)
        {
            m_SolverType = newSolverType;
        }
    }
}