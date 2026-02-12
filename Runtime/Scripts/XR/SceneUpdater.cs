/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
	public class SceneUpdateData
	{
		public Matrix4x4 Pose;
		public Matrix4x4 TrackerSpace;
		public Matrix4x4 MapSpacePose;
		public ICameraData CameraData;
		public LocalizeInfo LocalizeInfo;
		public MapEntry MapEntry;
		public bool Ignore;
	}
	
    public class SceneUpdater : MonoBehaviour, ISceneUpdater
    {
	    public async Task UpdateScene(MapEntry entry, ICameraData cameraData, ILocalizationResult localizationResult)
	    {
			ImmersalLogger.Log("Updating scene", ImmersalLogger.LoggingLevel.Verbose);
	    
		    LocalizeInfo locInfo = localizationResult.LocalizeInfo;
		    
		    // Immersal pose relative to the map
		    Vector3 localizedPos = locInfo.position;
		    Quaternion localizedRot = locInfo.rotation;
		    
		    // Apply device specific orientation and switch handedness to align with Unity
			localizedRot.AdjustForScreenOrientation();
		    localizedRot.SwitchHandedness();
		    localizedPos.SwitchHandedness();
		
		    // Apply map to space relative transform (map pose in the scene)
		    MapToSpaceRelation mo = entry.Relation;
		    Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.Position, mo.Rotation, Vector3.one);
		    Vector3 scaledPos = Vector3.Scale(localizedPos, mo.Scale);
		    Matrix4x4 mapSpace = offsetNoScale * Matrix4x4.TRS(scaledPos, localizedRot, Vector3.one);

		    // Tracker space
		    Vector3 capturePos = cameraData.CameraPositionOnCapture;
		    Quaternion captureRot = cameraData.CameraRotationOnCapture;
		    Matrix4x4 trackerSpace = Matrix4x4.TRS(capturePos, captureRot, Vector3.one);
		    
		    // Tracker relative pose
		    Matrix4x4 m = trackerSpace * (mapSpace.inverse);

		    SceneUpdateData data = new SceneUpdateData
		    {
			    Pose = m,
			    TrackerSpace = trackerSpace,
			    MapSpacePose = mapSpace,
			    CameraData = cameraData,
			    LocalizeInfo = locInfo,
			    MapEntry = entry,
			    Ignore = false
		    };

		    await entry.SceneParent.SceneUpdate(data);
	    }
    }
}