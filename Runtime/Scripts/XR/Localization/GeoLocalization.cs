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
    public class GeoLocalization : MonoBehaviour, ILocalizationMethod
    {
	    // Note:
	    // Custom editor does not draw default inspector
	    
	    [SerializeField]
	    private ConfigurationMode m_ConfigurationMode = ConfigurationMode.WhenNecessary;
        
	    [SerializeField]
	    private SolverType m_SolverType = SolverType.Default;
	    
	    [SerializeField]
	    private int m_PriorNNCountMin = 60;
        
	    [SerializeField]
	    private int m_PriorNNCountMax = 720;
	    
	    [SerializeField]
	    private Vector3 m_PriorScale = Vector3.one;
        
	    [SerializeField]
	    private float m_PriorRadius = 0f;

	    public ConfigurationMode ConfigurationMode => m_ConfigurationMode;
	    
	    public UnityEvent<float> OnProgress;
	    
	    private int m_PreviouslyLocalizedMapId = 0;
	    
	    // No options
	    public IMapOption[] MapOptions => null;

	    private SDKMapId[] m_MapIds;
        
	    public Task<bool> Configure(ILocalizationMethodConfiguration configuration)
	    {
		    m_SolverType = configuration.SolverType ?? m_SolverType;
		    m_PriorNNCountMin = configuration.PriorNNCountMin ?? m_PriorNNCountMin;
		    m_PriorNNCountMax = configuration.PriorNNCountMax ?? m_PriorNNCountMax;
		    m_PriorScale = configuration.PriorScale ?? m_PriorScale;
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
	        LocalizationResult r = new LocalizationResult
	        {
		        Success = false
	        };

	        if (m_MapIds == null || m_MapIds.Length == 0)
		        return r;
	        
	        JobGeoPoseAsync j = new JobGeoPoseAsync();
	        j.Progress.ProgressChanged += OnCurrentJobProgress;
		        
	        Vector4 intrinsics = cameraData.Intrinsics;
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

	        j.image = capture; //t.Result.Item1;
	        j.intrinsics = intrinsics;
	        j.mapIds = m_MapIds;			
	        j.solverType = m_SolverType == SolverType.Prior ? 4 : (int)m_SolverType;
	        
	        if (m_SolverType == SolverType.Prior &&
	            m_PreviouslyLocalizedMapId != 0 &&
	            MapManager.TryGetMapEntry(m_PreviouslyLocalizedMapId, out MapEntry previousMapEntry))
	        {
		        Vector3 pos = cameraData.CameraPositionOnCapture;
		        Matrix4x4 mapPoseWithRelation = previousMapEntry.SceneParent.ToMapSpace(pos, Quaternion.identity);
		        Vector3 priorPos = previousMapEntry.Relation.ApplyInverseRelation(mapPoseWithRelation).GetPosition();
		        priorPos.SwitchHandedness();
		        j.priorPos = priorPos;
		        j.priorNNCountMin = m_PriorNNCountMin;
		        j.priorNNCountMax = m_PriorNNCountMax;
		        j.priorScale = m_PriorScale;
		        j.priorRadius = m_PriorRadius;
	        }
	        else
	        {
		        // previously localized map not found, reset
		        m_PreviouslyLocalizedMapId = 0;
	        }
	        
	        Quaternion rot = cameraData.CameraRotationOnCapture * cameraData.Orientation;
	        rot.SwitchHandedness();
	        j.rotation = rot;

			SDKGeoPoseResult result = await j.RunJobAsync(cancellationToken);
			string resultJson = JsonUtility.ToJson(result);

	        float elapsedTime = Time.realtimeSinceStartup - startTime;

	        if (result.success)
	        {
		        ImmersalLogger.Log($"Relocalized in {elapsedTime} seconds");

		        int mapId = result.map;
		        double latitude = result.latitude;
		        double longitude = result.longitude;
		        double ellipsoidHeight = result.ellipsoidHeight;
				double[] quatEcef = new double[4];	// w,x,y,z
				Core.RotEnuToEcef(quatEcef, result.quaternion, latitude, longitude, false);

		        double[] ecef = new double[3];
		        double[] wgs84 = new double[3] { latitude, longitude, ellipsoidHeight };
		        Core.PosWgs84ToEcef(ecef, wgs84);

		        if (MapManager.TryGetMapEntry(mapId, out MapEntry entry))
		        {
			        double[] mapToEcef = entry.Map.MapToEcefGet();
					mapToEcef[12] = 1.0; // force scale to 1.0
					Quaternion qEcef = new Quaternion((float)quatEcef[1], (float)quatEcef[2], (float)quatEcef[3], (float)quatEcef[0]);
			        Core.PosEcefToMap(out Vector3 mapPos, ecef, mapToEcef);
			        Core.RotEcefToMap(out Quaternion mapRot, qEcef, mapToEcef);
			        
			        LocalizeInfo locInfo = new LocalizeInfo
			        {
				        mapId = mapId,
				        position = mapPos,
				        rotation = mapRot,
				        confidence = result.confidence
			        };

			        r.Success = true;
			        r.MapId = mapId;
			        r.LocalizeInfo = locInfo;

			        m_PreviouslyLocalizedMapId = mapId;
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
	        m_MapIds = Array.Empty<SDKMapId>();
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