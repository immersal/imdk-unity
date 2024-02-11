/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Immersal.XR
{
    public enum SolverType
    {
        Default = 0,
        Lean = 1
    };
    
    [Serializable]
    public class DeviceLocalization : MonoBehaviour, ILocalizationMethod
    {
        [SerializeField]
        private ConfigurationMode m_ConfigurationMode = ConfigurationMode.Always;
        
        [SerializeField]
        private SolverType m_SolverType = SolverType.Default;

        public ConfigurationMode ConfigurationMode => m_ConfigurationMode;

        public IMapOption[] MapOptions => new IMapOption[]
        {
            new MapLoadingOption()
        };

        public Task<bool> Configure(XRMap[] maps)
        {
            // On device localization does not need configuration
            return Task.FromResult(true);
        }

        public async Task<ILocalizationResult> Localize(ICameraData cameraData, CancellationToken cancellationToken)
        {
            LocalizationResult r = new LocalizationResult();

            if (cameraData.PixelBuffer != IntPtr.Zero)
            {
                float startTime = Time.realtimeSinceStartup;

                Task<LocalizeInfo> t = Task.Run(() =>
                {
                    return Immersal.Core.LocalizeImage(cameraData, (int)m_SolverType);
                });

                await t;

                LocalizeInfo locInfo = t.Result;
                float elapsedTime = Time.realtimeSinceStartup - startTime;
                if (locInfo.mapId > 0)
                {
                    r.Success = true;
                    r.MapId = locInfo.mapId;
                    r.LocalizeInfo = locInfo;
			        
                    ImmersalLogger.Log($"Relocalized in {elapsedTime} seconds");
                }
                else
                {
                    r.Success = false;

                    ImmersalLogger.Log($"Localization attempt failed after {elapsedTime} seconds");
                }
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