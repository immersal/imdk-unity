/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Immersal.XR
{
    public interface ICameraData
    {
        IImageData GetImageData();
        byte[] GetBytes();
        CameraData Copy(IImageData imageData);
        public void ReleaseReference();
        int Width { get; }
        int Height { get; }
        int Channels { get; }
        CameraDataFormat Format { get; }
        Vector4 Intrinsics { get; } 
        Vector3 CameraPositionOnCapture { get; }
        Quaternion CameraRotationOnCapture { get; }
        double[] Distortion { get; }
        Quaternion Orientation { get; }
    }
    
    public class CameraData : ICameraData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
        public CameraDataFormat Format { get; set; }
        public Vector4 Intrinsics { get; set; }  // x = principal point x, y = principal point y, z = focal length x, w = focal length y
        public Vector3 CameraPositionOnCapture { get; set; }
        public Quaternion CameraRotationOnCapture { get; set; }
        public double[] Distortion { get; set; } // not yet used
        public Quaternion Orientation { get; set; }
        
        private readonly IImageData m_ImageData;
        private int m_ReferenceCount;
        private bool m_IsDisposed;

        public CameraData(IImageData imageData)
        {
            m_ImageData = imageData;
            m_ImageData.SetCameraDataReference(this);
        }
        
        public IImageData GetImageData()
        {
            if (m_IsDisposed) throw new ObjectDisposedException("Immersal.XR.CameraData");
            Interlocked.Increment(ref m_ReferenceCount);
            return m_ImageData;
        }

        public byte[] GetBytes()
        {
            if (m_IsDisposed) throw new ObjectDisposedException("Immersal.XR.CameraData");
            return m_ImageData.ManagedBytes;
        }
        
        public void ReleaseReference()
        {
            Interlocked.Decrement(ref m_ReferenceCount);
            if (m_ReferenceCount <= 0)
            {
                Dispose();
            }
        }

        private void Dispose()
        {
            if (m_IsDisposed)
            {
                ImmersalLogger.LogWarning("Attempting to dispose already disposed CameraData");
                return;
            }

            if (m_ImageData != null)
            {
                m_ImageData.DisposeData();
            }
            else
            {
                ImmersalLogger.LogWarning("Attempting to dispose null ImageData");
            }
   
            m_IsDisposed = true;
        }

        public CameraData Copy(IImageData imageData)
        {
            CameraData data = new CameraData(imageData)
            {
                Width = this.Width,
                Height = this.Height,
                Intrinsics = this.Intrinsics,
                Format = this.Format,
                Channels = this.Channels,
                CameraPositionOnCapture = this.CameraPositionOnCapture,
                CameraRotationOnCapture = this.CameraRotationOnCapture,
                Orientation = this.Orientation,
                Distortion = this.Distortion
            };
            return data;
        }
    }
    
    public interface IImageData : IDisposable
    {
        public IntPtr UnmanagedDataPointer { get; }
        public byte[] ManagedBytes { get; }
        
        void SetCameraDataReference(ICameraData cameraData); 
        void DisposeData();
    }
    
    public abstract class ImageData: IImageData
    {
        public abstract IntPtr UnmanagedDataPointer { get; }
        public abstract byte[] ManagedBytes { get; }
        
        public abstract void DisposeData();
        
        private ICameraData m_CameraData;
        private bool m_CameraDataReferenceSet = false;

        public void Dispose()
        {
            if (m_CameraData == null)
            {
                ImmersalLogger.LogWarning("Disposing ImageData with no CameraData reference.");
                DisposeData();
                return;
            }
            m_CameraData.ReleaseReference();
        }

        public void SetCameraDataReference(ICameraData cameraData)
        {
            if (m_CameraDataReferenceSet)
            {
                ImmersalLogger.LogError("CameraData reference already set.");
                return;
            }
            m_CameraData = cameraData;
            m_CameraDataReferenceSet = true;
        }
    }

    public sealed class SimpleImageData : ImageData
    {
        public override IntPtr UnmanagedDataPointer => m_UnmanagedDataPointer;
        public override byte[] ManagedBytes { get; }

        private IntPtr m_UnmanagedDataPointer;
        private GCHandle m_managedDataHandle;

        public SimpleImageData(byte[] bytes)
        {
            ManagedBytes = bytes;
            m_managedDataHandle = GCHandle.Alloc(ManagedBytes, GCHandleType.Pinned);
            m_UnmanagedDataPointer = m_managedDataHandle.AddrOfPinnedObject();
        }

        public override void DisposeData()
        {
            m_managedDataHandle.Free();
            m_UnmanagedDataPointer = IntPtr.Zero;
        }
    }

}