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
        Task SceneUpdate(Matrix4x4 poseMatrix);
        Transform GetTransform();
        Task ResetScene();
    }
}