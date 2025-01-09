/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using System.Threading.Tasks;
using UnityEngine;

namespace Immersal.XR
{
    public interface ISceneUpdateable
    {
        Task SceneUpdate(SceneUpdateData data);
        Transform GetTransform();
        Task ResetScene();
    }

    public static class SceneUpdateableExtensions
    {
        public static Matrix4x4 ToMapSpace(this ISceneUpdateable sceneUpdateable, Vector3 pos, Quaternion rot)
        {
            Transform spaceTransform = sceneUpdateable.GetTransform();
            Matrix4x4 pose = Matrix4x4.TRS(pos, rot, Vector3.one);
            Matrix4x4 spacePose = Matrix4x4.TRS(spaceTransform.position, spaceTransform.rotation, Vector3.one);
            return spacePose.inverse * pose;
        }
    }
}