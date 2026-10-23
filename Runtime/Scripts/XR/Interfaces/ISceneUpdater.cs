/*===============================================================================
Copyright (C) 2026 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Threading.Tasks;

namespace Immersal.XR
{
    public interface ISceneUpdater
    {
        Task UpdateScene(MapEntry entry, ICameraData cameraData, ILocalizationResult localizationResult);
    }
}