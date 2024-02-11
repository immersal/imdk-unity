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
    
    public interface ICameraData
    {
        IntPtr PixelBuffer { get; }
        int Width { get; }
        int Height { get; }
        Vector4 Intrinsics { get; } 
        Vector3 CameraPositionOnCapture { get; }
        Quaternion CameraRotationOnCapture { get; }
        double[] Distortion { get; }
        Quaternion Orientation { get; }
    }

    public interface IPlatformConfigureResult
    {
        bool Success { get; }
    }
    
    public interface IPlatformSupport
    {
        Task<IPlatformUpdateResult> UpdatePlatform();
        Task<IPlatformConfigureResult> ConfigurePlatform();
        Task StopAndCleanUp();
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
    
    public struct CameraData : ICameraData
    {
        public IntPtr PixelBuffer { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Vector4 Intrinsics { get; set; }  // x = principal point x, y = principal point y, z = focal length x, w = focal length y
        public Vector3 CameraPositionOnCapture { get; set; }
        public Quaternion CameraRotationOnCapture { get; set; }
        public double[] Distortion { get; set; } // not yet used
        public Quaternion Orientation { get; set; }
    }

    #endregion
}