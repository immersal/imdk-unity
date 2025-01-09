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
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Immersal.XR;
using Immersal.REST;
using Immersal.Samples.Util;
using TMPro;
using UnityEngine.Serialization;

namespace Immersal.Samples.Mapping
{
    public class RealtimeCaptureManager : MonoBehaviour
    {
        public const int MAX_VERTICES = 65535;

        [SerializeField] private bool m_SaveMapOnServer;
        [SerializeField] private float m_CaptureInterval = 1.0f;
        [SerializeField] private float m_PointSize = 20f;
        [SerializeField] private Color m_PointColor = new Color(0.57f, 0.93f, 0.12f);
        [SerializeField] private XRSpace m_XRSpace;
        [SerializeField] private Button m_CaptureButton = null;
        [SerializeField] private Image m_CaptureButtonIcon = null;
        [SerializeField] private Sprite m_StartCaptureSprite = null;
        [SerializeField] private Sprite m_StopCaptureSprite = null;
        [SerializeField] private TextMeshProUGUI m_StatusText = null;

        private ImmersalSDK m_Sdk;
        private bool m_IsTracking = false;
        private bool m_IsMapping = false;
        private Camera m_MainCamera = null;
        private GameObject m_PointCloud = null;
        private Mesh m_Mesh = null;
        private MeshFilter m_MeshFilter = null;
        private MeshRenderer m_MeshRenderer = null;
        private XRMap m_XRMap = null;

        private bool m_SdkInitComplete = false;
        private bool m_UserValidationComplete = false;
        
        private void Start()
        {
            m_MainCamera = Camera.main;
            m_CaptureButtonIcon.sprite = m_StartCaptureSprite;
            m_CaptureButton.interactable = false;
            
            InitMesh();
            
            LogStatus("Initializing...");
            
            m_Sdk = ImmersalSDK.Instance;
            
            // Check if SDK is ready and user is authorized
            // If not, setup callbacks to act when the processes complete
            m_SdkInitComplete = m_Sdk.IsReady;
            if (!m_SdkInitComplete) m_Sdk.OnInitializationComplete.AddListener(InitializationComplete);
            m_UserValidationComplete = m_Sdk.HasValidated;
            if (!m_UserValidationComplete) m_Sdk.OnUserValidationComplete.AddListener(ValidationComplete);
            CheckState();
        }

        private void InitializationComplete()
        {
            m_Sdk.OnInitializationComplete.RemoveListener(InitializationComplete);
            m_SdkInitComplete = true;
            CheckState();
        }

        private void ValidationComplete()
        {
            m_Sdk.OnUserValidationComplete.RemoveListener(ValidationComplete);
            m_UserValidationComplete = true;
            CheckState();
        }

        private void CheckState()
        {
            if (!m_SdkInitComplete || !m_UserValidationComplete) return;

            // Stop / disable mapping if not tracking
            if (!m_IsTracking)
            {
                if (m_IsMapping)
                {
                    StopRealtimeCapture();
                }
                m_CaptureButton.interactable = false;
                LogStatus("Not tracking");
                return;
            }
            
            // Check license level
            if (m_Sdk.LicenseLevel >= 1)
            {
                m_CaptureButton.interactable = true;
                LogStatus("Ready");
            }
            else
            {
                LogStatus("Enterprise license required");
            }
        }

		void OnEnable()
		{
			ARSession.stateChanged += ARSessionStateChanged;
		}

		void OnDisable()
		{
			ARSession.stateChanged -= ARSessionStateChanged;
		}

		private void ARSessionStateChanged(ARSessionStateChangedEventArgs args)
		{
            m_IsTracking = args.state == ARSessionState.SessionTracking;
            CheckState();
		}

        private void LogStatus(string s)
        {
            if (m_StatusText != null)
            {
                m_StatusText.text = s;
            }
        }

        private void InitMesh()
        {
            m_PointCloud = new GameObject("Realtime Capture Point Cloud", typeof(MeshFilter), typeof(MeshRenderer));
            
            m_MeshFilter = m_PointCloud.GetComponent<MeshFilter>();
            m_MeshRenderer = m_PointCloud.GetComponent<MeshRenderer>();
            m_Mesh = new Mesh();
            m_MeshFilter.mesh = m_Mesh;

            Material material = new Material(Shader.Find("Immersal/Point Cloud"));
            m_MeshRenderer.material = material;
            m_MeshRenderer.material.SetFloat("_PointSize", m_PointSize);
            m_MeshRenderer.material.SetFloat("_PerspectiveEnabled", 0f);
            m_MeshRenderer.material.SetColor("_PointColor", m_PointColor);
        }

        public void CreateCloud(Vector3[] points, int totalPoints, Matrix4x4 offset)
        {
            int numPoints = totalPoints >= MAX_VERTICES ? MAX_VERTICES : totalPoints;
            int[] indices = new int[numPoints];
            Vector3[] pts = new Vector3[numPoints];
            Color32[] col = new Color32[numPoints];
            for (int i = 0; i < numPoints; ++i)
            {
                indices[i] = i;
                pts[i] = offset.MultiplyPoint3x4(points[i]);
            }

            m_Mesh.Clear();
            m_Mesh.vertices = pts;
            m_Mesh.colors32 = col;
            m_Mesh.SetIndices(indices, MeshTopology.Points, 0);
            m_Mesh.bounds = new Bounds(transform.position, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue));
        }

        public void CreateCloud(Vector3[] points, int totalPoints)
        {
            CreateCloud(points, totalPoints, Matrix4x4.identity);
        }

        private void ResetPoints()
        {
            Vector3[] points = new Vector3[0];
            CreateCloud(points, points.Length);
        }

        public void ToggleCapture()
        {
            if (!m_IsMapping)
            {
                StartRealtimeCapture();
            }
            else
            {
                StopRealtimeCapture();
            }
        }

        public async void StartRealtimeCapture()
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android)
            {
                ResetPoints();

                if (m_XRMap != null)
                {
                    if (m_XRMap.Visualization != null)
                        m_XRMap.RemoveVisualization();
                    
                    MapManager.RemoveMap(m_XRMap.mapId);
                    await m_Sdk.ResetScenes();
                    m_Sdk.TrackingAnalyzer.Reset();
                }

                m_IsMapping = true;
                m_CaptureButtonIcon.sprite = m_StopCaptureSprite;
                LogStatus("Capturing");

                NotificationManager.Instance.GenerateNotification("Realtime mapping started");
                
                while (m_IsMapping)
                {
                    await RealtimeCapture();
                    await Task.Delay((int)(m_CaptureInterval * 1000));
                }
            }
            else
            {
                NotificationManager.Instance.GenerateWarning("Realtime mapping not enabled on this platform");
            }
        }

        public async void StopRealtimeCapture()
        {
            m_IsMapping = false;
            m_CaptureButtonIcon.sprite = m_StartCaptureSprite;

            NotificationManager.Instance.GenerateNotification("Realtime mapping stopped");

            await Task.Delay(2000);

            ResetPoints();

            int numImages = Immersal.Core.MapImageGetCount();
            ImmersalLogger.Log($"Captured {numImages} images");

            int size = Immersal.Core.MapPrepare(Application.persistentDataPath);
            ImmersalLogger.Log($"Map size: {size} bytes");

            if (size > 0)
            {
                byte[] map = new byte[size];
                int r = Immersal.Core.MapGet(map);

                if (r == 1)
                {
                    if (m_SaveMapOnServer)
                    {
                        JobMapUploadAsync j = new JobMapUploadAsync();
                        j.name = "RealtimeMap";
                        j.mapData = map;
                        j.OnError += (e) =>
                        {
                            Debug.LogError(e);
                            NotificationManager.Instance.GenerateError("Map upload failed");
                        };

                        SDKMapUploadResult result = await j.RunJobAsync();

                        NotificationManager.Instance.GenerateSuccess("Map uploaded!");

                        SetupMap(map, result);
                    }
                    else
                    {
                        SetupMap(map, default);
                    }
                }
            }

            Immersal.Core.MapResourcesFree();
        }

        private async void SetupMap(byte[] mapData, SDKMapUploadResult result)
        {
            Transform root = m_XRSpace.transform;
            SDKJob job = default;
            job.type = (int)Immersal.REST.SDKJobType.Map;
            job.id = (result.id == 0) ? UnityEngine.Random.Range(1, 10000) : result.id;
            job.name = this.name;
            job.privacy = (int)SDKJobPrivacy.Private;

            MapCreationParameters parameters = new MapCreationParameters
            {
                MapId = job.id,
                SceneParent = m_XRSpace,
                LocalizationMethodType = typeof(DeviceLocalization),
                MapOptions = new IMapOption[]
                {
                    new MapLoadingOption
                    {
                        Bytes = mapData
                    }
                }
            };
            
            MapCreationResult r = await MapManager.TryCreateMap(parameters);

            if (r.Success)
            {
                m_XRMap = r.Map;
                m_XRMap.CreateVisualization(XRMapVisualization.RenderMode.EditorAndRuntime, true);
                m_XRMap.Visualization.LoadFromPlugin();
            }

            LogStatus("Localizing");
        }

        private async Task RealtimeCapture()
        {
            IPlatformUpdateResult platformUpdateResult = await m_Sdk.PlatformSupport.UpdatePlatform();

            if (!platformUpdateResult.Success)
                return;

            ICameraData cameraData = platformUpdateResult.CameraData;
            using IImageData imageData = cameraData.GetImageData();

            Vector3 pos = cameraData.CameraPositionOnCapture;
            Quaternion r = cameraData.CameraRotationOnCapture;
            r *= cameraData.Orientation;
            pos.SwitchHandedness();
            r.SwitchHandedness();
            Vector4 intrinsics = cameraData.Intrinsics;

            int result = await Task.Run(() => Immersal.Core.MapAddImage(imageData.UnmanagedDataPointer, cameraData.Width,
                cameraData.Height, cameraData.Channels, ref intrinsics, ref pos, ref r));

            if (result == 0)
            {
                int numPoints = Immersal.Core.MapPointsGetCount();
                Vector3[] points = new Vector3[numPoints];
                Immersal.Core.MapPointsGet(points);
                for (int i = 0; i < numPoints; i++)
                    points[i] = points[i].SwitchHandedness();
                
                CreateCloud(points, numPoints);

                int numImages = Immersal.Core.MapImageGetCount();
                LogStatus(string.Format("Capturing, images: {0}", numImages));
            }
        }
    }
}