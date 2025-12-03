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
using UnityEngine.Events;

namespace Immersal.XR
{
    public class GeoPoseLocalization : MonoBehaviour, ILocalizationMethod
    {
	    // Note:
	    // Custom editor does not draw default inspector
	    
	    [SerializeField]
	    private ConfigurationMode m_ConfigurationMode = ConfigurationMode.WhenNecessary;

		[SerializeField]
		private float m_SearchRadius = 200f;

		[SerializeField]
		private GPSLocationProvider m_LocationProvider;
		
	    public ConfigurationMode ConfigurationMode => m_ConfigurationMode;

		public UnityEvent<float> OnProgress;

		// No options
		public IMapOption[] MapOptions => null;
		
        private SDKMapId[] m_MapIds;
        
	    public Task<bool> Configure(ILocalizationMethodConfiguration configuration)
	    {
            if (m_LocationProvider == null)
            {
                m_LocationProvider = gameObject.AddComponent<GPSLocationProvider>();
            }
			
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

	        JobOSCPGeoPoseAsync j = new JobOSCPGeoPoseAsync();
	        j.Progress.ProgressChanged += OnCurrentJobProgress;
		        
	        int channels = cameraData.Channels;
	        int width = cameraData.Width;
	        int height = cameraData.Height;
		        
	        byte[] capture = new byte[channels * width * height + 8192];

	        float startTime = Time.realtimeSinceStartup;
	        using (IImageData imageData = cameraData.GetImageData())
	        {
		        int size = width * height * channels;
		        byte[] pixels = new byte[size];
		        Marshal.Copy(imageData.UnmanagedDataPointer, pixels, 0, size);
		        
		        Task<(byte[], CaptureInfo)> t = Task.Run(() =>
		        {
			        CaptureInfo info =
				        Immersal.Core.CaptureImage(capture, capture.Length, pixels, width, height, channels);
			        Array.Resize(ref capture, info.captureSize);
			        return (capture, info);
		        });

		        await t;
	        }

            j.width = width;
            j.height = height;
	        j.intrinsics = cameraData.Intrinsics;
            j.orientation = (int)cameraData.Orientation.eulerAngles.z;
            j.mirrored = false;
            j.latitude = m_LocationProvider.Latitude;
            j.longitude = m_LocationProvider.Longitude;
            j.altitude = m_LocationProvider.Altitude;
            j.accuracy = m_LocationProvider.HorizontalAccuracy;
            j.altitudeAccuracy = m_LocationProvider.VerticalAccuracy;
			j.radius = m_SearchRadius;
			ImmersalLogger.Log($"GeoPose request: {JsonUtility.ToJson(j)}");
	        j.image = capture;

			OSCPGeoPoseResp result = await j.RunJobAsync(cancellationToken);
			ImmersalLogger.Log($"GeoPose response: {JsonUtility.ToJson(result)}");

			float elapsedTime = Time.realtimeSinceStartup - startTime;
			bool success = (result.geopose.position.lat == 0 && result.geopose.position.lon == 0 && result.geopose.position.h == 0) ? false : true;

            if (success)
            {
		        ImmersalLogger.Log($"Relocalized in {elapsedTime} seconds");

                double latitude = result.geopose.position.lat;
                double longitude = result.geopose.position.lon;
				double ellipsoidHeight = result.geopose.position.h;
				double[] doubleEcef = new double[4];	// x,y,z,w
				double[] doubleEnu = new double[4]
				{
					result.geopose.quaternion.x,
					result.geopose.quaternion.y,
					result.geopose.quaternion.z,
					result.geopose.quaternion.w
				};

				Core.RotEnuToEcef(doubleEcef, doubleEnu, latitude, longitude, true);

				Quaternion qEnu = new Quaternion((float)doubleEnu[0], (float)doubleEnu[1], (float)doubleEnu[2], (float)doubleEnu[3]);
				Quaternion qEcef = new Quaternion((float)doubleEcef[0], (float)doubleEcef[1], (float)doubleEcef[2], (float)doubleEcef[3]);
		        ImmersalLogger.Log($"GeoPose returned latitude: {latitude}, longitude: {longitude}, ellipsoidHeight: {ellipsoidHeight}, ENU quaternion: {qEnu}, ECEF quaternion: {qEcef}");

		        double[] ecef = new double[3];
		        double[] wgs84 = new double[3] { latitude, longitude, ellipsoidHeight };
		        Core.PosWgs84ToEcef(ecef, wgs84);

		        ImmersalLogger.Log($"GeoPose ECEF position x: {ecef[0]}, y: {ecef[1]}, z: {ecef[2]}");

				// for testing visually a map ID is required
				int mapId = m_MapIds[0].id;
				if (mapId != -1)
                {
					if (MapManager.TryGetMapEntry(mapId, out MapEntry entry))
					{
						double[] mapToEcef = entry.Map.MapToEcefGet();
						mapToEcef[12] = 1.0; // force scale to 1.0
						Core.PosEcefToMap(out Vector3 mapPos, ecef, mapToEcef);
						Core.RotEcefToMap(out Quaternion mapRot, qEcef, mapToEcef);

						LocalizeInfo locInfo = new LocalizeInfo
						{
							mapId = mapId,
							position = mapPos,
							rotation = mapRot
						};

						r.Success = true;
						r.MapId = mapId;
						r.LocalizeInfo = locInfo;
					}
                }
	        }
	        else
	        {
		        ImmersalLogger.Log($"Localization attempt failed after {elapsedTime} seconds");
	        }

	        j.Progress.ProgressChanged -= OnCurrentJobProgress;
	        return r;
        }
 
        public Task StopAndCleanUp()
        {
	        return Task.CompletedTask;
        }

        public Task OnMapRegistered(XRMap map)
        {
	        return Task.CompletedTask;
        }
        
        private void OnCurrentJobProgress(object sender, float value)
        {
	        OnProgress?.Invoke(value);
        }
    }
}