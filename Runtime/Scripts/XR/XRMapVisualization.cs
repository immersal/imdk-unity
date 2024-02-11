/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Immersal.XR
{
    [ExecuteAlways]
    public class XRMapVisualization : MonoBehaviour
    {
        private const int MAX_VERTICES = 65535;

        [SerializeField, HideInInspector] public bool IsVisualized = false;

        public static readonly Color[] pointCloudColors = new Color[]
        {
            new Color(0.22f, 1f, 0.46f),
            new Color(0.96f, 0.14f, 0.14f),
            new Color(0.16f, 0.69f, 0.95f),
            new Color(0.93f, 0.84f, 0.12f),
            new Color(0.57f, 0.93f, 0.12f),
            new Color(1f, 0.38f, 0.78f),
            new Color(0.4f, 0f, 0.9f),
            new Color(0.89f, 0.4f, 0f)
        };

        public enum RenderMode
        {
            DoNotRender,
            EditorOnly,
            EditorAndRuntime
        }

        public static bool pointCloudVisible = true;

        [SerializeField, HideInInspector] public RenderMode renderMode = RenderMode.EditorOnly;

        [SerializeField, HideInInspector] public Color m_PointColor = new Color(0.57f, 0.93f, 0.12f);

        public Color pointColor
        {
            get { return m_PointColor; }
            set { m_PointColor = value; }
        }

        public static float pointSize = 0.33f;

        // public static bool isRenderable = true;
        public static bool renderAs3dPoints = true;

        [SerializeField, HideInInspector]
        public XRMap Map;

        [SerializeField, HideInInspector]
        private Shader m_Shader;
        
        [SerializeField, HideInInspector]
        private Material m_Material;
        
        [SerializeField, HideInInspector]
        private Mesh m_Mesh;

        [SerializeField, HideInInspector]
        private MeshFilter m_MeshFilter;
        
        [SerializeField, HideInInspector]
        private MeshRenderer m_MeshRenderer;

        public Mesh Mesh => m_Mesh;
        
        public void Initialize(XRMap map, RenderMode renderMode, Color pointColor)
        {
            Map = map;
            this.renderMode = renderMode;
            this.pointColor = pointColor;
        }

        public void LoadPly(string filePath)
        {
            Mesh plyMesh = PlyImporter.PlyToMesh(filePath);
            SetMesh(plyMesh);
        }
        
        public void LoadPly(byte[] bytes, string plyName)
        {
            Mesh plyMesh = PlyImporter.PlyToMesh(bytes, plyName);
            SetMesh(plyMesh);
        }

        public void LoadFromPlugin()
        {
            if (Map == null)
            {
                ImmersalLogger.LogError("Cant load point cloud from plugin, map is null");
                return;
            }
            
            int numPoints = Immersal.Core.GetPointCloudSize(Map.mapId);
            Vector3[] points = new Vector3[numPoints];
            Immersal.Core.GetPointCloud(Map.mapId, points);
            for (int i = 0; i < numPoints; i++)
                points[i] = points[i].SwitchHandedness();

            Mesh mesh = PointsToMesh(points, numPoints, Matrix4x4.identity);
            SetMesh(mesh);
        }
        
        private Mesh PointsToMesh(Vector3[] points, int totalPoints, Matrix4x4 offset)
        {
            Mesh m = new Mesh();
            
            int numPoints = totalPoints >= MAX_VERTICES ? MAX_VERTICES : totalPoints;
            int[] indices = new int[numPoints];
            Vector3[] pts = new Vector3[numPoints];
            Color32[] col = new Color32[numPoints];
            for (int i = 0; i < numPoints; ++i)
            {
                indices[i] = i;
                pts[i] = offset.MultiplyPoint3x4(points[i]);
            }

            m.Clear();
            m.vertices = pts;
            m.colors32 = col;
            m.SetIndices(indices, MeshTopology.Points, 0);
            m.bounds = new Bounds(transform.position, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue));

            return m;
        }

        public void SetMesh(Mesh mesh)
        {
            if (this == null) return;

            ClearVisualization();

            if (m_Shader == null)
            {
                m_Shader = Shader.Find("Immersal/Point Cloud");
            }

            if (m_Material == null)
            {
                m_Material = new Material(m_Shader);
                //m_Material.hideFlags = HideFlags.DontSave;
            }
            
            m_Mesh = mesh;
            
            if (m_MeshFilter == null)
            {
                m_MeshFilter = gameObject.GetComponent<MeshFilter>();
                if (m_MeshFilter == null)
                {
                    m_MeshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (m_MeshRenderer == null)
                {
                    m_MeshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            m_MeshFilter.mesh = m_Mesh;
            m_MeshRenderer.material = m_Material;

            m_MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            m_MeshRenderer.lightProbeUsage = LightProbeUsage.Off;
            m_MeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            IsVisualized = true;
            
            UpdateMaterial();
        }

        public void ClearVisualization()
        {
            if (m_MeshFilter != null)
            {
                DestroyImmediate(m_MeshFilter);
            }

            if (m_MeshRenderer != null)
            {
                DestroyImmediate(m_MeshRenderer);
            }

            if (m_Material != null)
            {
                DestroyImmediate(m_Material);
            }

            m_Material = null;
            m_Mesh = null;

            IsVisualized = false;
        }

        private void ClearMesh()
        {
            if (m_Mesh != null)
            {
                m_Mesh.Clear();
            }
        }

        private bool IsRenderable()
        {
            if (pointCloudVisible)
            {
                switch (renderMode)
                {
                    case RenderMode.DoNotRender:
                        return false;
                    case RenderMode.EditorOnly:
                        if (Application.isEditor)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    case RenderMode.EditorAndRuntime:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }

        public void Start()
        {
            UpdateMaterial();
        }

        public void OnRenderObject()
        {
            UpdateMaterial();
        }

        public void UpdateMaterial()
        {
            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
                if (m_MeshRenderer == null) return;
            }

            if (m_Material == null)
            {
                m_Material = m_MeshRenderer.sharedMaterial;
                if (m_Material == null) return;
            }

            if (!IsRenderable())
            {
                m_MeshRenderer.enabled = false;
                return;
            }

            m_MeshRenderer.enabled = true;

            if (renderAs3dPoints)
            {
                m_Material.SetFloat("_PerspectiveEnabled", 1f);
                m_Material.SetFloat("_PointSize", Mathf.Lerp(0.002f, 0.14f, Mathf.Max(0, Mathf.Pow(pointSize, 3f))));
            }
            else
            {
                m_Material.SetFloat("_PerspectiveEnabled", 0f);
                m_Material.SetFloat("_PointSize", Mathf.Lerp(1.5f, 40f, Mathf.Max(0, pointSize)));
            }

            m_Material.SetColor("_PointColor", m_PointColor);
        }
        
        private void OnDestroy()
        {
            if (m_Material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(m_Mesh);
                    Destroy(m_Material);
                }
                else
                {
                    DestroyImmediate(m_Mesh);
                    DestroyImmediate(m_Material);
                }
            }
        }
    }
}