/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Immersal.XR
{
    public class ARFoundationSupport : MonoBehaviour, IPlatformSupport
    {
        [SerializeField, Tooltip("Maximum configuration attempts")]
        private int m_MaxConfigurationAttempts = 10;
        
        [SerializeField, Tooltip("Milliseconds to wait between configuration attempts")]
        private int m_MsBetweenConfigurationAttempts = 100;
        
        private ARCameraManager m_CameraManager;
        private ARSession m_ARSession;
        private Transform m_CameraTransform;

        private XRCameraConfiguration? m_InitialConfig;
        private IPlatformConfiguration m_Configuration;
        private bool m_ConfigDone = false;

        private bool m_OverrideScreenOrientation = false;
        private ScreenOrientation m_ScreenOrientationOverride = ScreenOrientation.Portrait;

        public ARCameraManager cameraManager
        {
            get
            {
                if (m_CameraManager == null)
                {
                    m_CameraManager = UnityEngine.Object.FindFirstObjectByType<ARCameraManager>();
                }
                return m_CameraManager;
            }
        }

        public ARSession arSession
        {
            get
            {
                if (m_ARSession == null)
                {
                    m_ARSession = UnityEngine.Object.FindFirstObjectByType<ARSession>();
                }
                return m_ARSession;
            }
        }

        public enum CameraResolution { Default, HD, FullHD, Max };	// With Huawei AR Engine SDK, only Default (640x480) and Max (1440x1080) are supported.
        
        [SerializeField]
        [Tooltip("Android resolution")]
        private CameraResolution m_AndroidResolution = CameraResolution.FullHD;
        
        [SerializeField]
        [Tooltip("iOS resolution")]
        private CameraResolution m_iOSResolution = CameraResolution.Default;

        [SerializeField]
        private CameraDataFormat m_CameraDataFormat = CameraDataFormat.SingleChannel;
        
        public CameraResolution androidResolution
        {
            get { return m_AndroidResolution; }
            set
            {
                m_AndroidResolution = value;
                ConfigureCamera();
            }
        }

        public CameraResolution iOSResolution
        {
            get { return m_iOSResolution; }
            set
            {
                m_iOSResolution = value;
                ConfigureCamera();
            }
        }

        private bool m_IsTracking = false;
        private bool m_FrameReceived = false;

        public async Task<IPlatformConfigureResult> ConfigurePlatform()
        {
            PlatformConfiguration config = new PlatformConfiguration
            {
                CameraDataFormat = m_CameraDataFormat
            };
            return await ConfigurePlatform(config);
        }

        public async Task<IPlatformConfigureResult> ConfigurePlatform(IPlatformConfiguration configuration)
        {
            ImmersalLogger.Log("Configuring ARF Platform");
            
#if UNITY_EDITOR
            ImmersalLogger.LogWarning("Running AR Foundation Platform in Unity Editor will result in failed updates.");
#endif
            if (!cameraManager)
            {
                throw new ComponentTaskCriticalException("Could not find ARCameraManager.");
            }
            
            if (!arSession)
            {
                throw new ComponentTaskCriticalException("Could not find ARSession.");
            }

            m_FrameReceived = false;
            cameraManager.frameReceived -= OnCameraFrameReceived;
            cameraManager.frameReceived += OnCameraFrameReceived;

            m_Configuration = configuration;
            m_CameraTransform = m_CameraManager.transform;

            for (int i = 0; i < m_MaxConfigurationAttempts; i++)
            {
                m_ConfigDone = ConfigureCamera();

                if (m_ConfigDone)
                    break;

                await Task.Delay(m_MsBetweenConfigurationAttempts);
            }

            IPlatformConfigureResult r = new SimplePlatformConfigureResult
            { 
                Success = m_ConfigDone
            };
            
            return r;
        }

        private bool ConfigureCamera()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
			var cameraSubsystem = cameraManager.subsystem;
			if (cameraSubsystem == null || !cameraSubsystem.running)
				return false;
			using var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp);
			if (!configurations.IsCreated || configurations.Length == 0)
				return false;
			int bestError = int.MaxValue;
			var currentConfig = cameraSubsystem.currentConfiguration;
            if (!currentConfig.HasValue)
                return false;
			int dw = (int)currentConfig.Value.width;
			int dh = (int)currentConfig.Value.height;
			if (dw == 0 && dh == 0)
				return false;
#if UNITY_ANDROID
			CameraResolution reso = androidResolution;
#else
			CameraResolution reso = iOSResolution;
#endif

			if (!m_ConfigDone)
			{
				m_InitialConfig = currentConfig;
			}

			switch (reso)
			{
				case CameraResolution.Default:
					dw = (int)currentConfig?.width;
					dh = (int)currentConfig?.height;
					break;
				case CameraResolution.HD:
					dw = 1280;
					dh = 720;
					break;
				case CameraResolution.FullHD:
					dw = 1920;
					dh = 1080;
					break;
				case CameraResolution.Max:
					dw = 80000;
					dh = 80000;
					break;
			}

			foreach (var config in configurations)
			{
				int perror = config.width * config.height - dw * dh;
				if (Math.Abs(perror) < bestError)
				{
					bestError = Math.Abs(perror);
					currentConfig = config;
				}
			}

			if (reso != CameraResolution.Default) {
				ImmersalLogger.Log($"resolution = {(int)currentConfig?.width}x{(int)currentConfig?.height}");
				cameraSubsystem.currentConfiguration = currentConfig;
			}
			else
			{
				cameraSubsystem.currentConfiguration = m_InitialConfig;
			}
#endif
            return true;
        }
        
        public Task<IPlatformUpdateResult> UpdatePlatform()
        {
            return UpdateWithConfiguration(m_Configuration);
        }
        
        public Task<IPlatformUpdateResult> UpdatePlatform(IPlatformConfiguration oneShotConfiguration)
        {
            return UpdateWithConfiguration(oneShotConfiguration);
        }
        
        private Task<IPlatformUpdateResult> UpdateWithConfiguration(IPlatformConfiguration configuration)
        {
            ImmersalLogger.Log("Updating AR Foundation Platform");
            
            if (!m_ConfigDone)
                throw new ComponentTaskCriticalException("Trying to update platform before configuration.");
            
            (bool success, CameraData data) = GetCameraData(configuration.CameraDataFormat);

            // Status
            SimplePlatformStatus platformStatus = new SimplePlatformStatus
            {
                TrackingQuality = m_IsTracking && success ? 1 : 0
            };

            // UpdateResult
            SimplePlatformUpdateResult r = new SimplePlatformUpdateResult
            {
                Success = success,
                Status = platformStatus,
                CameraData = (ICameraData)data
            };

            return Task.FromResult<IPlatformUpdateResult>(r);
        }

        private (bool, CameraData) GetCameraData(CameraDataFormat cameraDataFormat)
        {
            if (!m_FrameReceived)
                return (false, null);

            if (!m_CameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                ImmersalLogger.LogError("Could not acquire camera image.");
                return (false, null);
            }

            if (!GetIntrinsics(out Vector4 intrinsics))
            {
                image.Dispose();
                ImmersalLogger.LogError("Could not acquire camera intrinsics.");
                return (false, null);
            }

            Vector3 position = m_CameraTransform.position;
            Quaternion rotation = m_CameraTransform.rotation;
            Quaternion orientation = GetScreenOrientation();
            uint imageOrientation = GetImageOrientation();

            ARFImageData imageData = new ARFImageData(image, cameraDataFormat);
            CameraData data = new CameraData(imageData)
            {
                Width = image.width,
                Height = image.height,
                Intrinsics = intrinsics,
                Format = cameraDataFormat,
                Channels = cameraDataFormat == CameraDataFormat.SingleChannel ? 1 : 3,
                CameraPositionOnCapture = position,
                CameraRotationOnCapture = rotation,
                ScreenOrientation = orientation,
                ImageOrientation = imageOrientation
            };

            return (true, data);
        }

        public uint GetImageOrientation()
        {
            uint angle = Input.deviceOrientation switch
            {
                DeviceOrientation.Portrait => 90,
                DeviceOrientation.LandscapeRight => 180,
                DeviceOrientation.LandscapeLeft => 0,
                DeviceOrientation.PortraitUpsideDown => 270,
                _ => 0
            };
            return angle;
        }

        public Quaternion GetScreenOrientation()
        {
            ScreenOrientation orientation =
                m_OverrideScreenOrientation ? m_ScreenOrientationOverride : Screen.orientation;
            float angle = orientation switch
            {
                ScreenOrientation.Portrait => 90f,
                ScreenOrientation.LandscapeLeft => 180f,
                ScreenOrientation.LandscapeRight => 0f,
                ScreenOrientation.PortraitUpsideDown => -90f,
                _ => 0f
            };
            return Quaternion.Euler(0f, 0f, angle);
        }

        public void SetOrientationOverride(ScreenOrientation newOrientation)
        {
            m_OverrideScreenOrientation = true;
            m_ScreenOrientationOverride = newOrientation;
        }

        public void DisableOrientationOverride()
        {
            m_OverrideScreenOrientation = false;
        }

        public bool GetIntrinsics(out Vector4 intrinsics)
        {
            intrinsics = Vector4.zero;
            XRCameraIntrinsics intr = default;

            bool success = m_CameraManager != null && m_CameraManager.TryGetIntrinsics(out intr);

            if (success)
            {
                intrinsics.x = intr.focalLength.x;
                intrinsics.y = intr.focalLength.y;
                intrinsics.z = intr.principalPoint.x;
                intrinsics.w = intr.principalPoint.y;
            }

            return success;
        }

        private void OnEnable()
        {
#if !UNITY_EDITOR
			m_IsTracking = ARSession.state == ARSessionState.SessionTracking;
			ARSession.stateChanged += ARSessionStateChanged;
#endif
        }
        
        private void OnDisable()
        {
#if !UNITY_EDITOR
			ARSession.stateChanged -= ARSessionStateChanged;
#endif
            m_IsTracking = false;
        }

        private void ARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            m_IsTracking = args.state == ARSessionState.SessionTracking;
        }

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            m_FrameReceived = true;
        }
        
        public Task StopAndCleanUp()
        {
            m_ConfigDone = false;
            m_IsTracking = false;
            cameraManager.frameReceived -= OnCameraFrameReceived;
            m_FrameReceived = false;
            return Task.CompletedTask;
        }
    }
    
    public class ARFImageData : ImageData
    {
        public XRCpuImage Image;
        private IntPtr m_UnmanagedDataPointer;
        private byte[] m_ManagedBytes;
        private GCHandle m_ManagedDataHandle;

        public override IntPtr UnmanagedDataPointer => m_UnmanagedDataPointer;

        public override byte[] ManagedBytes
        {
            get
            {
                if (m_ManagedBytes == null || m_ManagedBytes.Length == 0)
                {
                    m_ManagedBytes = CopyBytes();
                }

                return m_ManagedBytes;
            }
        }

        private CameraDataFormat m_Format;

        public ARFImageData(XRCpuImage image, CameraDataFormat format)
        {
            Image = image;
            m_Format = format;
            switch (format)
            {
                case CameraDataFormat.RGB:
                    GetPointerToRGB(Image);
                    break;
                default:
                case CameraDataFormat.SingleChannel:
                    GetPointerFast(Image);
                    break;
            }
        }

        public override void DisposeData()
        {
            Image.Dispose();
            if (m_ManagedDataHandle.IsAllocated)
                m_ManagedDataHandle.Free();
            m_UnmanagedDataPointer = IntPtr.Zero;
        }

        private void GetPointerFast(XRCpuImage image)
        {
            XRCpuImage.Plane plane = image.GetPlane(0); // use the Y plane
            int width = image.width, height = image.height;

            if (width == plane.rowStride)
            {
                unsafe
                {
                    m_UnmanagedDataPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(plane.data);
                }
            }
            else
            {
                m_ManagedBytes = new byte[width * height];
                m_ManagedDataHandle = GCHandle.Alloc(m_ManagedBytes, GCHandleType.Pinned);
                m_UnmanagedDataPointer = m_ManagedDataHandle.AddrOfPinnedObject();

                unsafe
                {
                    byte* dstPtr = (byte*)m_UnmanagedDataPointer;
                    byte* srcPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(plane.data);
                    UnsafeUtility.MemCpyStride(dstPtr, width, srcPtr, plane.rowStride, width, height);
                }
            }
        }

        private void GetPointerToRGB(XRCpuImage image)
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height),
                outputFormat = TextureFormat.RGB24,
                transformation = XRCpuImage.Transformation.None
            };

            int size = image.GetConvertedDataSize(conversionParams);
            m_ManagedBytes = new byte[size];
            m_ManagedDataHandle = GCHandle.Alloc(m_ManagedBytes, GCHandleType.Pinned);
            m_UnmanagedDataPointer = m_ManagedDataHandle.AddrOfPinnedObject();
            image.Convert(conversionParams, m_UnmanagedDataPointer, m_ManagedBytes.Length);
        }

        private byte[] CopyBytes()
        {
            int pixelSize = m_Format == CameraDataFormat.SingleChannel ? 1 : 3;
            int size = Image.width * Image.height * pixelSize;
            byte[] bytes = new byte[size];
            Marshal.Copy(m_UnmanagedDataPointer, bytes, 0, size);
            return bytes;
        }
    }
}