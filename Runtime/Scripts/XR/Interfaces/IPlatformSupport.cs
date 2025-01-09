/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Immersal.XR
{
    public interface IPlatformUpdateResult
    {
        bool Success { get; }
        IPlatformStatus Status { get; }
        ICameraData CameraData { get; }
    }
    
    public interface IPlatformStatus
    {
        int TrackingQuality { get; }
    }
    
    public enum CameraDataFormat
    {
        SingleChannel,
        RGB
    }

    public interface IPlatformConfigureResult
    {
        bool Success { get; }
    }
    
    public interface IPlatformSupport
    {
        Task<IPlatformUpdateResult> UpdatePlatform();
        Task<IPlatformUpdateResult> UpdatePlatform(IPlatformConfiguration oneShotConfiguration);
        Task<IPlatformConfigureResult> ConfigurePlatform();
        Task<IPlatformConfigureResult> ConfigurePlatform(IPlatformConfiguration configuration);
        Task StopAndCleanUp();
    }

    public interface IPlatformConfiguration
    {
        CameraDataFormat CameraDataFormat { get; }
    }
    
    #region Simple implementations
    
    public struct SimplePlatformConfigureResult : IPlatformConfigureResult
    {
        public bool Success { get; set; }
    }
    
    public struct SimplePlatformUpdateResult : IPlatformUpdateResult
    {
        public bool Success { get; set; }
        public IPlatformStatus Status { get; set; }
        public ICameraData CameraData { get; set; }
    }

    public struct SimplePlatformStatus : IPlatformStatus
    {
        public int TrackingQuality { get; set; }
    }

    public struct PlatformConfiguration : IPlatformConfiguration
    {
        public CameraDataFormat CameraDataFormat { get; set; }
    }

    #endregion
}