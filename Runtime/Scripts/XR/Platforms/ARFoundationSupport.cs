/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
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
        private bool m_ConfigDone = false;
        
        public ARCameraManager cameraManager
        {
            get
            {
                if (m_CameraManager == null)
                {
                    m_CameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();
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
                    m_ARSession = UnityEngine.Object.FindObjectOfType<ARSession>();
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

        private Task<(bool, CameraData)> m_CurrentCameraDataTask;
        private IntPtr m_PixelBuffer = IntPtr.Zero;
        private bool m_isTracking = false;
        
        private void Awake()
        {
            m_CameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();

            if (!m_CameraManager)
            {
                throw new ComponentTaskCriticalException("Could not find ARCameraManager.");
            }
            
            m_ARSession = UnityEngine.Object.FindObjectOfType<ARSession>();
            if (!m_ARSession)
            {
                throw new ComponentTaskCriticalException("Could not find ARSession.");
            }

            if (Camera.main != null) m_CameraTransform = Camera.main.transform;
        }

        public async Task<IPlatformConfigureResult> ConfigurePlatform()
        {
            ImmersalLogger.Log("Configuring ARF Platform");
            
#if UNITY_EDITOR
            ImmersalLogger.LogWarning("Running AR Foundation Platform in Unity Editor will result in failed updates.");
#endif

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
			var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp);
			if (!configurations.IsCreated || (configurations.Length <= 0))
				return false;
			int bestError = int.MaxValue;
			var currentConfig = cameraSubsystem.currentConfiguration;
			int dw = (int)currentConfig?.width;
			int dh = (int)currentConfig?.height;
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

        public async Task<IPlatformUpdateResult> UpdatePlatform()
        {
            ImmersalLogger.Log("Updating ARF Platform");
            
            if (!m_ConfigDone)
                throw new ComponentTaskCriticalException("Trying to update platform before configuration.");
            
            // Status
            SimplePlatformStatus platformStatus = new SimplePlatformStatus
            {
                TrackingQuality = m_isTracking ? 1 : 0
            };

            m_CurrentCameraDataTask = GetCameraData();
            (bool success, CameraData data) = await m_CurrentCameraDataTask;

            // UpdateResult
            SimplePlatformUpdateResult r = new SimplePlatformUpdateResult
            {
                Success = success,
                Status = platformStatus,
                CameraData = data
            };
       
            return r;
        }

        private async Task<(bool, CameraData)> GetCameraData()
        {
            // CameraData
            CameraData data = new CameraData();

            bool imageAcquired = false;
            await Task.Run(() =>
            {
                imageAcquired = m_CameraManager.TryAcquireLatestCpuImage(out XRCpuImage image);
                if (!imageAcquired) return;
                using (image)
                {
                    GetPlaneDataFast(ref m_PixelBuffer, image);
                    data.PixelBuffer = m_PixelBuffer;

                    data.Width = image.width;
                    data.Height = image.height;
                }
            });

            if (!imageAcquired)
            {
                ImmersalLogger.LogError("Could not acquire camera image.");
                return (false, data);
            }
            
            if (!GetIntrinsics(out Vector4 intrinsics))
            {
                ImmersalLogger.LogError("Could not acquire camera intrinsics.");
                return (false, data);
            }

            if (m_CameraTransform == null)
            {
                ImmersalLogger.LogError("Could not acquire camera pose.");
                return (false, data);
            }
            
            data.Intrinsics = intrinsics;
            data.CameraPositionOnCapture = m_CameraTransform.position;
            data.CameraRotationOnCapture = m_CameraTransform.rotation;

            // distortion..

            data.Orientation = GetOrientation();

            return (true, data);
        }

        private void GetPlaneDataFast(ref IntPtr pixels, XRCpuImage image)
        {
            XRCpuImage.Plane plane = image.GetPlane(0);	// use the Y plane
            int width = image.width, height = image.height;

            if (width == plane.rowStride)
            {
                unsafe
                {
                    pixels = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(plane.data);
                }
            }
            else
            {
                byte[] data = new byte[width * height];

                unsafe
                {
                    fixed (byte* dstPtr = data)
                    {
                        byte* srcPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(plane.data);
                        if (width > 0 && height > 0) {
                            UnsafeUtility.MemCpyStride(dstPtr, width, srcPtr, plane.rowStride, width, height);
                        }
                        pixels = (IntPtr)dstPtr;
                    }
                }
            }
        }

        private bool GetIntrinsics(out Vector4 intrinsics)
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
        
        private Quaternion GetOrientation()
        {
            float angle = 0f;
            switch (Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                    angle = 90f;
                    break;
                case ScreenOrientation.LandscapeLeft:
                    angle = 180f;
                    break;
                case ScreenOrientation.LandscapeRight:
                    angle = 0f;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    angle = -90f;
                    break;
                default:
                    angle = 0f;
                    break;
            }

            return Quaternion.Euler(0f, 0f, angle);
        }

        private void OnEnable()
        {
#if !UNITY_EDITOR
			m_isTracking = ARSession.state == ARSessionState.SessionTracking;
			ARSession.stateChanged += ARSessionStateChanged;
#endif
        }
        
        private void OnDisable()
        {
#if !UNITY_EDITOR
			ARSession.stateChanged -= ARSessionStateChanged;
#endif
            m_isTracking = false;
        }

        private void ARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            m_isTracking = args.state == ARSessionState.SessionTracking;
        }
        
        public async Task StopAndCleanUp()
        {
	         // there is no cancellation token for the update procedure here, just wait
             await m_CurrentCameraDataTask;
             m_ConfigDone = false;
             m_PixelBuffer = IntPtr.Zero;
             m_isTracking = false;
        }

        private void OnDestroy()
        {
            m_PixelBuffer = IntPtr.Zero;
        }
    }
}