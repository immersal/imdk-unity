/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Immersal.XR
{
    public struct VisualGeoPose
    {
        public double Latitude;
        public double Longitude;
        public double Altitude;
        public float Bearing;
        public Pose Pose;
    }
    
    public class GeoPoseProcessor : MonoBehaviour, IDataProcessor<SessionData>
    {
        [SerializeField]
        private TextMeshProUGUI m_GeoPoseText;
        
        public VisualGeoPose LatestGeoPose { get; private set; }

        private Camera m_MainCamera = null;
        private Matrix4x4 m_LatestTrackerSpacePose;
        private double[] m_LatestMapEcef; 
        
        public Camera MainCamera
        {
            get
            {
                if (m_MainCamera == null)
                {
                    m_MainCamera = Camera.main;
                    if (m_MainCamera == null)
                        ImmersalLogger.LogError("No Camera found");
                }

                return m_MainCamera;
            }
        }

        public virtual void Update()
        {
            if (m_LatestMapEcef != null)
                UpdateLocation();
        }

        private void UpdateLocation()
        {
            VisualGeoPose newPose = new VisualGeoPose();
            
            Vector2 cd = CompassDir(MainCamera, m_LatestTrackerSpacePose.inverse, m_LatestMapEcef);
            float bearing = Mathf.Atan2(-cd.x, cd.y) * (180f / (float)Math.PI);
            if(bearing >= 0f)
            {
                newPose.Bearing = bearing;
            }
            else
            {
                newPose.Bearing = 360f - Mathf.Abs(bearing);
            }

            Vector3 pos = m_LatestTrackerSpacePose.GetColumn(3);

            double[] wgs84 = new double[3];
            int r = Immersal.Core.PosMapToWgs84(wgs84, pos.SwitchHandedness(), m_LatestMapEcef);
            newPose.Latitude = wgs84[0];
            newPose.Longitude = wgs84[1];
            newPose.Altitude = wgs84[2];
            newPose.Pose = new Pose(pos, m_LatestTrackerSpacePose.rotation);
            LatestGeoPose = newPose;
            
            string vgpsString = string.Format("VLat: {0}, VLon: {1}, VAlt: {2}, VBRG: {3}", 
                newPose.Latitude.ToString("0.000000"),
                newPose.Longitude.ToString("0.000000"),
                newPose.Altitude.ToString("0.0"),
                newPose.Bearing.ToString("0.0"));

            if (m_GeoPoseText != null)
                m_GeoPoseText.text = vgpsString;
        }
        
        public Task<SessionData> ProcessData(SessionData data, DataProcessorTrigger trigger)
        {
            if (trigger == DataProcessorTrigger.NewData)
            {
                Vector3 capturePos = data.PlatformResult.CameraData.CameraPositionOnCapture;
                Quaternion captureRot = data.PlatformResult.CameraData.CameraRotationOnCapture;
                
                Vector3 pos = data.LocalizationResult.LocalizeInfo.position;
                Quaternion rot = data.LocalizationResult.LocalizeInfo.rotation;
                rot *= data.PlatformResult.CameraData.Orientation;
                pos.SwitchHandedness();
                rot.SwitchHandedness();
                
                // Bring pose to tracker space
                MapToSpaceRelation mo = data.Entry.Relation;
                Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.Position, mo.Rotation, Vector3.one);
                Vector3 scaledPos = Vector3.Scale(pos, mo.Scale);
                Matrix4x4 cloudSpace = offsetNoScale * Matrix4x4.TRS(scaledPos, rot, Vector3.one);
                Matrix4x4 trackerSpace = Matrix4x4.TRS(capturePos, captureRot, Vector3.one);
                m_LatestTrackerSpacePose = trackerSpace * (cloudSpace.inverse);
                
                // Cache ecef as well
                m_LatestMapEcef = data.Entry.Map.MapToEcefGet();
            }

            return Task.FromResult(data);
        }

        public Task ResetProcessor()
        {
            LatestGeoPose = new VisualGeoPose();
            m_LatestTrackerSpacePose = Matrix4x4.identity;
            m_LatestMapEcef = Array.Empty<double>();
            return Task.CompletedTask;
        }

        Matrix4x4 RotX(double angle)
        {
            float c = (float)System.Math.Cos(angle * System.Math.PI / 180.0);
            float s = (float)System.Math.Sin(angle * System.Math.PI / 180.0);

            Matrix4x4 r = Matrix4x4.identity;

            r.m11 = c;
            r.m22 = c;
            r.m12 = s;
            r.m21 = -s;

            return r;
        }

        Matrix4x4 RotZ(double angle)
        {
            float c = (float)System.Math.Cos(angle * System.Math.PI / 180.0);
            float s = (float)System.Math.Sin(angle * System.Math.PI / 180.0);

            Matrix4x4 r = Matrix4x4.identity;

            r.m00 = c;
            r.m11 = c;
            r.m10 = -s;
            r.m01 = s;

            return r;
        }

        Matrix4x4 Rot3d(double lat, double lon)
        {
            Matrix4x4 rz = RotZ(90 + lon);
            Matrix4x4 rx = RotX(90 - lat);
            return rx * rz;
        }

        Vector2 CompassDir(Camera cam, Matrix4x4 trackerToMap, double[] mapToEcef)
        {
            Vector3 a = trackerToMap.MultiplyPoint(cam.transform.position);
            Vector3 b = trackerToMap.MultiplyPoint(cam.transform.position + cam.transform.forward);

            double[] aEcef = new double[3];
            int ra = Immersal.Core.PosMapToEcef(aEcef, a.SwitchHandedness(), mapToEcef);
            double[] bEcef = new double[3];
            int rb = Immersal.Core.PosMapToEcef(bEcef, b.SwitchHandedness(), mapToEcef);

            double[] wgs84 = new double[3];
            int rw = Immersal.Core.PosMapToWgs84(wgs84, a.SwitchHandedness(), mapToEcef);
            Matrix4x4 R = Rot3d(wgs84[0], wgs84[1]);

            Vector3 v = new Vector3((float)(bEcef[0] - aEcef[0]), (float)(bEcef[1] - aEcef[1]), (float)(bEcef[2] - aEcef[2]));
            Vector3 vt = R.MultiplyVector(v.normalized);

            Vector2 d = new Vector2(vt.x, vt.y);
            return d.normalized;
        }
    }
}