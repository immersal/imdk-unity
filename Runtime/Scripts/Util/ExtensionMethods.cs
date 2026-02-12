/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using Immersal.XR;
using UnityEngine;

namespace Immersal.XR
{
    public static class ExtensionMethods
    {
        public static Matrix4x4 SwitchHandedness(this ref Matrix4x4 b)
        {
            Matrix4x4 D = Matrix4x4.identity;
            D.m00 = -1;
            b = D * b * D;
            return b;
        }

        public static Quaternion SwitchHandedness(this ref Quaternion b)
        {
            Matrix4x4 m = Matrix4x4.Rotate(b);
            m.SwitchHandedness();
            b = m.rotation;
            return b;
        }

        public static float[] ToFloats(this ref Quaternion q)
        {
            float[] result = new float[4] { q.x, q.y, q.z, q.w };
            return result;
        }

        public static Vector3 SwitchHandedness(this ref Vector3 b)
        {
            Matrix4x4 m = Matrix4x4.TRS(b, Quaternion.identity, Vector3.one);
            m.SwitchHandedness();
            b = m.GetColumn(3);
            return b;
        }

        public static Quaternion AdjustForScreenOrientation(this ref Quaternion q, ScreenOrientation? overrideOrientation = null)
        {
            ScreenOrientation so = overrideOrientation ?? Screen.orientation;

            float angle = so switch
            {
                ScreenOrientation.Portrait => 90f,
                ScreenOrientation.LandscapeLeft => 180f,
                ScreenOrientation.LandscapeRight => 0f,
                ScreenOrientation.PortraitUpsideDown => -90f,
                _ => 0f
            };

            q *= Quaternion.Euler(0f, 0f, angle);
            return q;
        }
        
        public static double[] QuaternionsToDoubleMatrix3x3(this XRMap.MapAlignment ma)
        {
            double[] q = new double[] {ma.qw, ma.qx, ma.qy, ma.qz};
            double[] m = new double [] {1, 0, 0, 0, 1, 0, 0, 0, 1}; //identity matrix
            
            // input quaternion should be in WXYZ order
            double w = q[0];
            double x = q[1];
            double y = q[2];
            double z = q[3];
			
            double ww = w * w;
            double xx = x * x;
            double yy = y * y;
            double zz = z * z;
            
            double xy = x * y;
            double zw = z * w;
            double xz = x * z;
            double yw = y * w;
            double yz = y * z;
            double xw = x * w;

            double inv = 1.0 / (xx + yy + zz + ww);

            m[0] = ( xx - yy - zz + ww) * inv;
            m[1] = 2.0 * (xy - zw) * inv;
            m[2] = 2.0 * (xz + yw) * inv;
            m[3] = 2.0 * (xy + zw) * inv;
            m[4] = (-xx + yy - zz + ww) * inv;
            m[5] = 2.0 * (yz - xw) * inv;
            m[6] = 2.0 * (xz - yw) * inv;
            m[7] = 2.0 * (yz + xw) * inv;
            m[8] = (-xx - yy + zz + ww) * inv;

            return m;
        }
    }
}