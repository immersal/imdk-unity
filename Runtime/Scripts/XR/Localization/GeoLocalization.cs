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
    public class GeoLocalization : MonoBehaviour, ILocalizationMethod
    {
	    [SerializeField]
	    private ConfigurationMode m_ConfigurationMode = ConfigurationMode.WhenNecessary;
        
	    [SerializeField]
	    private SolverType m_SolverType = SolverType.Default;

	    public ConfigurationMode ConfigurationMode => m_ConfigurationMode;
	    
	    // No options
	    public IMapOption[] MapOptions => null;

	    private SDKMapId[] m_MapIds;
        
	    public Task<bool> Configure(XRMap[] maps)
	    {
		    List<SDKMapId> mapList = m_MapIds != null ? m_MapIds.ToList() : new List<SDKMapId>();
		    foreach (XRMap map in maps)
		    {
			    mapList.Add(new SDKMapId {id = map.mapId});
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
	        
	        JobGeoPoseAsync j = new JobGeoPoseAsync();
		        
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
	        if (m_SolverType == SolverType.Lean)
	        {
		        Quaternion rot = cameraData.CameraRotationOnCapture * cameraData.Orientation;
		        rot.SwitchHandedness();
		        j.rotation = rot;
	        }

	        SDKGeoPoseResult result = await j.RunJobAsync(cancellationToken);

	        float elapsedTime = Time.realtimeSinceStartup - startTime;

	        if (result.success)
	        {
		        ImmersalLogger.Log($"Relocalized in {elapsedTime} seconds");

		        int mapId = result.map;
		        double latitude = result.latitude;
		        double longitude = result.longitude;
		        double ellipsoidHeight = result.ellipsoidHeight;
		        Quaternion rot = new Quaternion(result.quaternion[1], result.quaternion[2], result.quaternion[3], result.quaternion[0]);
		        ImmersalLogger.Log($"GeoPose returned latitude: {latitude}, longitude: {longitude}, ellipsoidHeight: {ellipsoidHeight}, quaternion: {rot}");

		        double[] ecef = new double[3];
		        double[] wgs84 = new double[3] { latitude, longitude, ellipsoidHeight };
		        Core.PosWgs84ToEcef(ecef, wgs84);

		        if (MapManager.TryGetMapEntry(mapId, out MapEntry entry))
		        {
			        double[] mapToEcef = entry.Map.MapToEcefGet();
			        Core.PosEcefToMap(out Vector3 mapPos, ecef, mapToEcef);
			        Core.RotEcefToMap(out Quaternion mapRot, rot, mapToEcef);
			        
			        locInfo = new LocalizeInfo
			        {
				        position = mapPos,
				        rotation = mapRot,
				        confidence = 0
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
	        // This implementation has nothing to clean up
	        return Task.CompletedTask;
        }

        public Task OnMapRegistered(XRMap map)
        {
	        return Task.CompletedTask;
        }
    }
}