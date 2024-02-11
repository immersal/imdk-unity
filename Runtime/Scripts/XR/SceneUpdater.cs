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
    public class SceneUpdater : MonoBehaviour, ISceneUpdater
    {
		public async Task UpdateScene(MapEntry entry, ICameraData cameraData, ILocalizationResult localizationResult)
        {
	        ImmersalLogger.Log("Updating scene", ImmersalLogger.LoggingLevel.Verbose);
	        
	        Vector3 capturePos = cameraData.CameraPositionOnCapture;
	        Quaternion captureRot = cameraData.CameraRotationOnCapture;
            LocalizeInfo locInfo = localizationResult.LocalizeInfo;

			Vector3 localizedPos = locInfo.position;
			Quaternion localizedRot = locInfo.rotation;

		    // apply device specific orientation and switch handedness
		    localizedRot *= cameraData.Orientation;
		    localizedPos.SwitchHandedness();
			localizedRot.SwitchHandedness();

            // apply map to space relative transform
            MapToSpaceRelation mo = entry.Relation;
			Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.Position, mo.Rotation, Vector3.one);
			Vector3 scaledPos = Vector3.Scale(localizedPos, mo.Scale);
			Matrix4x4 cloudSpace = offsetNoScale * Matrix4x4.TRS(scaledPos, localizedRot, Vector3.one);
			
			// transform to tracker space
			Matrix4x4 trackerSpace = Matrix4x4.TRS(capturePos, captureRot, Vector3.one);
			Matrix4x4 m = trackerSpace * (cloudSpace.inverse);

            await entry.SceneParent.SceneUpdate(m);
        }
    }
}