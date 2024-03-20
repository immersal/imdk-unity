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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Immersal.REST;
using UnityEngine;

namespace Immersal.XR
{
    public class ServerLocalization : MonoBehaviour, ILocalizationMethod
    {
	    [SerializeField]
	    private ConfigurationMode m_ConfigurationMode = ConfigurationMode.WhenNecessary;
        
	    [SerializeField]
	    private SolverType m_SolverType = SolverType.Default;

	    public ConfigurationMode ConfigurationMode => m_ConfigurationMode;
	    
	    // No options
	    public IMapOption[] MapOptions => null;

	    private SDKMapId[] m_MapIds;
        
		public Task<bool> Configure(ILocalizationMethodConfiguration configuration)
		{
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
	        LocalizationResult r = new LocalizationResult
	        {
		        Success = false
	        };

	        if (m_MapIds == null || m_MapIds.Length == 0)
		        return r;
	        
            if (cameraData.PixelBuffer == IntPtr.Zero)
	            return r;
            
            JobLocalizeServerAsync j = new JobLocalizeServerAsync();
		        
	        Vector4 intrinsics = cameraData.Intrinsics;
	        int channels = 1;
	        int width = cameraData.Width;
	        int height = cameraData.Height;
		        
	        int size = width * height;
	        byte[] pixels = new byte[size];
	        Marshal.Copy(cameraData.PixelBuffer, pixels, 0, size);

	        LocalizeInfo locInfo = default;

	        float startTime = Time.realtimeSinceStartup;
		        
	        Task<(byte[], CaptureInfo)> t = Task.Run(() =>
	        {
		        byte[] capture = new byte[channels * width * height + 8192];
		        CaptureInfo info = Immersal.Core.CaptureImage(capture, capture.Length, pixels, width, height, channels);
		        Array.Resize(ref capture, info.captureSize);
		        return (capture, info);
	        });

	        await t;

	        j.image = t.Result.Item1;
	        j.intrinsics = intrinsics;
	        j.mapIds = m_MapIds;
			j.solverType = (int)m_SolverType;

	        Quaternion rot = cameraData.CameraRotationOnCapture * cameraData.Orientation;
	        rot.SwitchHandedness();
	        j.rotation = rot;

	        SDKLocalizeResult result = await j.RunJobAsync(cancellationToken);

	        float elapsedTime = Time.realtimeSinceStartup - startTime;

	        if (result.success)
	        {
		        ImmersalLogger.Log($"Relocalized to mapId {result.map} in {elapsedTime} seconds");

		        int mapId = result.map;

		        if (mapId > 0)
		        {
			        Matrix4x4 m = Matrix4x4.identity;
			        m.m00 = result.r00; m.m01 = result.r01; m.m02 = result.r02;
			        m.m10 = result.r10; m.m11 = result.r11; m.m12 = result.r12;
			        m.m20 = result.r20; m.m21 = result.r21; m.m22 = result.r22;

			        locInfo = new LocalizeInfo
			        {
				        mapId = mapId,
				        position = new Vector3(result.px, result.py, result.pz),
				        rotation = m.rotation,
				        confidence = result.confidence
			        };

			        r.Success = true;
			        r.MapId = mapId;
			        r.LocalizeInfo = locInfo;
		        }
	        }
	        else
	        {
		        ImmersalLogger.Log($"Localization attempt failed after {elapsedTime} seconds");
	        }

	        return r;
        }
        
        public Task StopAndCleanUp()
        {
	        m_MapIds = Array.Empty<SDKMapId>();
	        return Task.CompletedTask;
        }

        public Task OnMapRegistered(XRMap map)
        {
	        return Task.CompletedTask;
        }
    }
}