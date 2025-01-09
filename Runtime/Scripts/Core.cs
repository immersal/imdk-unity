/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Immersal.XR;
#if UNITY_EDITOR || UNITY_STANDALONE
using System.Diagnostics;
#endif

namespace Immersal
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CaptureInfo
    {
        public int captureSize;
        public int connected;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LocalizeInfo
    {
        public int mapId;
        public Vector3 position;
        public Quaternion rotation;
        public int confidence;
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(IntPtr msg);

    public static class Core
    {
        #region On-Device Mapping
        public static int MapAddImage(IntPtr pixels, int width, int height, int channels, ref Vector4 intrinsics, ref Vector3 pos, ref Quaternion rot) => Native.icvMapAddImage(pixels, width, height, channels, ref intrinsics, ref pos, ref rot);
   
        public static int MapImageGetCount() => Native.icvMapImageGetCount();

        public static int MapPrepare(string path) => Native.icvMapPrepare(path);

        public static int MapGet(byte[] map) => Native.icvMapGet(map);

        public static int MapPointsGetCount() => Native.icvMapPointsGetCount();

        public static int MapPointsGet(Vector3[] points)
        {
            GCHandle pointsHandle = GCHandle.Alloc(points, GCHandleType.Pinned);
            int n = Native.icvMapPointsGet(pointsHandle.AddrOfPinnedObject(), points.Length);
            pointsHandle.Free();
            return n;
        }

        public static int MapResourcesFree() => Native.icvMapResourcesFree();

        #endregion

        /// <summary>
        /// Get a Vector3 point cloud representation of the map data.
        /// </summary>
        /// <param name="mapId">An integer map id</param>
        /// <param name="points">A preallocated Vector3 array for the points</param>
        /// <returns>Returns the number of points if succeeded, 0 otherwise.</returns>
        public static int GetPointCloud(int mapId, Vector3[] points)
        {
            if (MapHandleMapping.TryGetHandle(mapId, out int handle))
            {
                GCHandle vector3ArrayHandle = GCHandle.Alloc(points, GCHandleType.Pinned);
                int n = Native.icvPointsGet(handle, vector3ArrayHandle.AddrOfPinnedObject(), points.Length);
                vector3ArrayHandle.Free();
                return n;
            }

            return -1;
        }

        /// <summary>
        /// Get point count of the map's point cloud.
        /// </summary>
        /// <param name="mapId">An integer map id</param>
        /// <returns>Returns the number of points.</returns>
        public static int GetPointCloudSize(int mapId)
        {
            if (MapHandleMapping.TryGetHandle(mapId, out int handle))
            {
                return Native.icvPointsGetCount(handle);
            }

            return -1;
        }

        /// <summary>
        /// Load map data from a .bytes file.
        /// </summary>
        /// <param name="buffer">Map data as a byte array</param>
        /// <param name="mapId">An integer map id</param>
        /// <returns>An integer map id</returns>
        public static int LoadMap(int mapId, byte[] buffer)
        {
            // if there is already a loaded map with the same id, return the handle pointing to that
            if (MapHandleMapping.TryGetHandle(mapId, out int handle))
            {
                ImmersalLogger.Log($"Trying to load map {mapId}({handle}) into plugin, but it's already mapped.");
                return handle;
            }

            int mapHandle = Native.icvLoadMap(buffer);
            MapHandleMapping.AddMapping(mapId, mapHandle);
            ImmersalLogger.Log($"Loaded map {mapId}({mapHandle}) into plugin");
            return mapHandle;
        }
       
        /// <summary>
        /// Free the map data from memory.
        /// </summary>
        /// <param name="mapId">An integer map handle</param>
        /// <returns>Returns 1 if succeeded, 0 otherwise.</returns>
        public static int FreeMap(int mapId)
        {
            if (MapHandleMapping.TryGetHandle(mapId, out int mapHandle))
            {
                ImmersalLogger.Log($"Freeing map {mapId}({mapHandle}) from plugin");
                MapHandleMapping.RemoveMappingByMapId(mapId);
                return Native.icvFreeMap(mapHandle);
            }

            return 0;
        }
        
        /// <summary>
        /// Capture image into the current map.
        /// </summary>
        /// <param name="capture">A preallocated byte array for the captured PNG image</param>
        /// <param name="captureSizeMax">Int size of the array</param>
        /// <param name="pixels">Raw pixel buffer data from the camera</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="channels">1 or 3, monochromatic or RGB capture</param>
        /// <returns>Int size of the captured PNG bytes</returns>
        public static CaptureInfo CaptureImage(byte[] capture, int captureSizeMax, byte[] pixels, int width,
            int height, int channels, int useMatching = 0)
        {
            GCHandle captureHandle = GCHandle.Alloc(capture, GCHandleType.Pinned);
            GCHandle pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            CaptureInfo info = Native.icvCaptureImage(captureHandle.AddrOfPinnedObject(), captureSizeMax,
                pixelsHandle.AddrOfPinnedObject(), width, height, channels, useMatching);
            captureHandle.Free();
            pixelsHandle.Free();

            return info;
        }

        /// <summary>
        /// Gets the position and orientation of the image within the map.
        /// </summary>
        /// <param name="pos">Output Vector3 for the position</param>
        /// <param name="rot">Output Quaternion for the orientation</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="intrinsics">Camera intrinsics</param>
        /// <param name="pixels">Raw pixel buffer data from the camera</param>
        /// <returns>A LocalizeInfo struct with mapId. mapId will be -1 on failure.</returns>
        public static LocalizeInfo LocalizeImage(int n, int[] mapIds, int width,
            int height, ref Vector4 intrinsics, IntPtr pixels, int channels, int solverType, ref Quaternion cameraRotation)
        {
            if (MapHandleMapping.IdsToHandles(mapIds, out int[] handles))
            {
                GCHandle intHandle = GCHandle.Alloc(handles, GCHandleType.Pinned);
                LocalizeInfo result = Native.icvLocalize(n, intHandle.AddrOfPinnedObject(), width, height,
                    ref intrinsics, pixels, channels, solverType, ref cameraRotation);
                intHandle.Free();
                
                // result.mapId is a handle at this point -> convert
                if (MapHandleMapping.TryGetMapId(result.mapId, out int mapId))
                {
                    result.mapId = mapId;
                    return result;
                }
            }

            return new LocalizeInfo
            {
                mapId = -1
            };
        }

        /// <summary>
        /// Gets the position and orientation of the image within the map.
        /// </summary>
        /// <param name="pos">Output Vector3 for the position</param>
        /// <param name="rot">Output Quaternion for the orientation</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="intrinsics">Camera intrinsics</param>
        /// <param name="pixels">Raw pixel buffer data from the camera</param>
        /// <returns>A LocalizeInfo struct with mapId. mapId will be -1 on failure.</returns>
        public static LocalizeInfo LocalizeImage(int width, int height,
            ref Vector4 intrinsics, IntPtr pixels)
        {
            int n = 0;
            int[] mapIds = new int[1];
            int channels = 1;
            Quaternion cameraRotation = Quaternion.identity;
            return LocalizeImage(n, mapIds, width, height, ref intrinsics, pixels, channels, 0, ref cameraRotation);
        }
        
        public static LocalizeInfo LocalizeImage(int width, int height,
            ref Vector4 intrinsics, IntPtr pixels, int channels, int solverType, ref Quaternion cameraRotation)
        {
            int n = 0;
            int[] mapIds = new int[1];
            return LocalizeImage(n, mapIds, width, height, ref intrinsics, pixels, channels, solverType, ref cameraRotation);
        }
            
        /// <summary>
        /// Gets the position and orientation of the image within the map.
        /// </summary>
        /// <param name="cameraData">ICameraData containing necessary data</param>
        /// <returns>A LocalizeInfo struct with mapId. mapId will be -1 on failure.</returns>
        public static LocalizeInfo LocalizeImage(ICameraData cameraData, IntPtr pixelBuffer, int solverType = 0)
        {
            int channels = cameraData.Channels == 0 ? 1 : cameraData.Channels; // default to 1
            Vector4 intrinsics = cameraData.Intrinsics;
            Quaternion r = cameraData.CameraRotationOnCapture * cameraData.Orientation;
            r.SwitchHandedness();
            return LocalizeImage(cameraData.Width, cameraData.Height, ref intrinsics, pixelBuffer, channels, solverType, ref r);
        }
        
        public static LocalizeInfo icvLocalizeImageWithPrior(ICameraData cameraData, IntPtr pixelBuffer, ref Vector3 priorPos, int priorNNCount, float priorRadius)
        {
            int channels = cameraData.Channels == 0 ? 1 : cameraData.Channels; // default to 1
            Vector4 intrinsics = cameraData.Intrinsics;
            int n = 0;
            int[] mapIds = new int[1];
            if (MapHandleMapping.IdsToHandles(mapIds, out int[] handles))
            {
                GCHandle intHandle = GCHandle.Alloc(handles, GCHandleType.Pinned);
                LocalizeInfo result = Native.icvLocalizePrior(n, intHandle.AddrOfPinnedObject(), cameraData.Width, cameraData.Height,
                    ref intrinsics, pixelBuffer, channels, ref priorPos, priorNNCount, priorRadius);
                intHandle.Free();
                
                // result.mapId is a handle at this point -> convert
                if (MapHandleMapping.TryGetMapId(result.mapId, out int mapId))
                {
                    result.mapId = mapId;
                    return result;
                }
            }

            return new LocalizeInfo
            {
                mapId = -1
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ecef"></param>
        /// <param name="map"></param>
        /// <param name="mapToEcef"></param>
        /// <returns></returns>
        public static int PosMapToEcef(double[] ecef, Vector3 map, double[] mapToEcef)
        {
            GCHandle ecefHandle = GCHandle.Alloc(ecef, GCHandleType.Pinned);
            GCHandle mapToEcefHandle = GCHandle.Alloc(mapToEcef, GCHandleType.Pinned);
            int r = Native.icvPosMapToEcef(ecefHandle.AddrOfPinnedObject(), ref map,
                mapToEcefHandle.AddrOfPinnedObject());
            mapToEcefHandle.Free();
            ecefHandle.Free();
            return r;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wgs84"></param>
        /// <param name="ecef"></param>
        /// <returns></returns>
        public static int PosEcefToWgs84(double[] wgs84, double[] ecef)
        {
            GCHandle wgs84Handle = GCHandle.Alloc(wgs84, GCHandleType.Pinned);
            GCHandle ecefHandle = GCHandle.Alloc(ecef, GCHandleType.Pinned);
            int r = Native.icvPosEcefToWgs84(wgs84Handle.AddrOfPinnedObject(), ecefHandle.AddrOfPinnedObject());
            ecefHandle.Free();
            wgs84Handle.Free();
            return r;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ecef"></param>
        /// <param name="wgs84"></param>
        /// <returns></returns>
        public static int PosWgs84ToEcef(double[] ecef, double[] wgs84)
        {
            GCHandle ecefHandle = GCHandle.Alloc(ecef, GCHandleType.Pinned);
            GCHandle wgs84Handle = GCHandle.Alloc(wgs84, GCHandleType.Pinned);
            int r = Native.icvPosWgs84ToEcef(ecefHandle.AddrOfPinnedObject(), wgs84Handle.AddrOfPinnedObject());
            wgs84Handle.Free();
            ecefHandle.Free();
            return r;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="ecef"></param>
        /// <param name="mapToEcef"></param>
        /// <returns></returns>
        public static int PosEcefToMap(out Vector3 map, double[] ecef, double[] mapToEcef)
        {
            GCHandle ecefHandle = GCHandle.Alloc(ecef, GCHandleType.Pinned);
            GCHandle mapToEcefHandle = GCHandle.Alloc(mapToEcef, GCHandleType.Pinned);
            int r = Native.icvPosEcefToMap(out map, ecefHandle.AddrOfPinnedObject(),
                mapToEcefHandle.AddrOfPinnedObject());
            mapToEcefHandle.Free();
            ecefHandle.Free();
            return r;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wgs84"></param>
        /// <param name="map"></param>
        /// <param name="mapToEcef"></param>
        /// <returns></returns>
        public static int PosMapToWgs84(double[] wgs84, Vector3 map, double[] mapToEcef)
        {
            double[] ecef = new double[3];
            int err = PosMapToEcef(ecef, map, mapToEcef);
            if (err != 0)
                return err;
            return PosEcefToWgs84(wgs84, ecef);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ecef"></param>
        /// <param name="map"></param>
        /// <param name="mapToEcef"></param>
        /// <returns></returns>
        public static int RotMapToEcef(out Quaternion ecef, Quaternion map, double[] mapToEcef)
        {
            GCHandle mapToEcefHandle = GCHandle.Alloc(mapToEcef, GCHandleType.Pinned);
            int r = Native.icvRotMapToEcef(out ecef, ref map, mapToEcefHandle.AddrOfPinnedObject());
            mapToEcefHandle.Free();
            return r;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="ecef"></param>
        /// <param name="mapToEcef"></param>
        /// <returns></returns>
        public static int RotEcefToMap(out Quaternion map, Quaternion ecef, double[] mapToEcef)
        {
            GCHandle mapToEcefHandle = GCHandle.Alloc(mapToEcef, GCHandleType.Pinned);
            int r = Native.icvRotEcefToMap(out map, ref ecef, mapToEcefHandle.AddrOfPinnedObject());
            mapToEcefHandle.Free();
            return r;
        }

        /// <summary>
        /// Get internal plugin parameters.
        /// </summary>
        /// <param name="parameter">Parameter name</param>
        /// <returns>Returns an integer value if set, -1 otherwise.</returns>
        public static int GetInteger(string parameter) => Native.icvGetInteger(parameter);

        /// <summary>
        /// Set internal plugin parameters.
        ///
        /// Available parameters:
        /// "LocalizationMaxPixels" - 0 is no limit (the default), 960*720 or higher.
        /// "NumThreads" - how many CPU cores to use; -1 (system default) or a positive integer.
        /// "ImageCompressionLevel" - 0 (no compression, fastest) to 9 (slowest). Defaults to 4.
        /// </summary>
        /// <param name="parameter">Parameter name</param>
        /// <param name="value">An integer parameter value</param>
        /// <returns>Returns 1 if succeeded, -1 otherwise.</returns>
        public static int SetInteger(string parameter, int value) => Native.icvSetInteger(parameter, value);

        public static int ValidateUser(string token) => Native.icvValidateUser(token);
    }
    
    public static class MapHandleMapping
    {
        private static Dictionary<int, int> mapIdToHandleMapping = new Dictionary<int, int>();
        private static Dictionary<int, int> handleToMapIdMapping = new Dictionary<int, int>();

        public static void AddMapping(int mapId, int pluginHandle)
        {
            if (mapIdToHandleMapping.ContainsKey(mapId) || handleToMapIdMapping.ContainsKey(pluginHandle))
            {
                return;
            }

            mapIdToHandleMapping[mapId] = pluginHandle;
            handleToMapIdMapping[pluginHandle] = mapId;
        }

        public static bool TryGetHandle(int mapId, out int pluginHandle)
        {
            return mapIdToHandleMapping.TryGetValue(mapId, out pluginHandle);
        }

        public static bool TryGetMapId(int pluginHandle, out int mapId)
        {
            mapId = -1;
            return handleToMapIdMapping.TryGetValue(pluginHandle, out mapId);
        }

        public static bool RemoveMappingByMapId(int mapId)
        {
            if (mapIdToHandleMapping.TryGetValue(mapId, out int pluginHandle))
            {
                mapIdToHandleMapping.Remove(mapId);
                handleToMapIdMapping.Remove(pluginHandle);
                return true;
            }

            return false;
        }

        public static bool RemoveMappingByHandle(int pluginHandle)
        {
            if (handleToMapIdMapping.TryGetValue(pluginHandle, out int mapId))
            {
                handleToMapIdMapping.Remove(pluginHandle);
                mapIdToHandleMapping.Remove(mapId);
                return true;
            }

            return false;
        }

        public static bool Clear()
        {
            foreach (KeyValuePair<int,int> keyValuePair in mapIdToHandleMapping)
            {
                if (RemoveMappingByMapId(keyValuePair.Key) == false)
                    return false;
            }

            return true;
        }

        public static bool IdsToHandles(int[] mapIds, out int[] handles)
        {
            handles = new int[mapIds.Length];
            
            // single zero id case
            if (mapIds.Length == 1 && mapIds[0] == 0)
            {
                handles[0] = 0;
                return true;
            }

            for (int i = 0; i < handles.Length; i++)
            {
                // Dictionary lookups are O(1) complexity, no need for caching?
                if (!TryGetHandle(mapIds[i], out int handle))
                    return false;
                
                handles[i] = handle;
            }

            return true;
        }
    }

    public static class Native
    {
        private const string Assembly =
#if UNITY_IOS && !UNITY_EDITOR
		"__Internal";
#else
        "PosePlugin";
#endif
        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PP_RegisterLogCallback(IntPtr callbackDelegate);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvPointsGet(int mapHandle, IntPtr array, int maxCount);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvPointsGetCount(int mapHandle);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvLoadMap(byte[] buffer);
        
        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvFreeMap(int mapHandle);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern CaptureInfo icvCaptureImage(IntPtr capture, int captureSizeMax, IntPtr pixels,
            int width, int height, int channels, int useMatching);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern LocalizeInfo icvLocalize(int n, IntPtr handles, int width,
            int height, ref Vector4 intrinsics, IntPtr pixels, int channels, int solverType, ref Quaternion cameraRotation);
        
        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern LocalizeInfo icvLocalizePrior(int n, IntPtr handles, int width,
            int height, ref Vector4 intrinsics, IntPtr pixels, int channels, ref Vector3 priorPos, int priorNNCount, float priorRadius);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvPosMapToEcef(IntPtr ecef, ref Vector3 map, IntPtr mapToEcef);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvPosEcefToWgs84(IntPtr wgs84, IntPtr ecef);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvPosWgs84ToEcef(IntPtr ecef, IntPtr wgs84);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvPosEcefToMap(out Vector3 map, IntPtr ecef, IntPtr mapToEcef);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvRotMapToEcef(out Quaternion ecef, ref Quaternion map, IntPtr mapToEcef);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvRotEcefToMap(out Quaternion map, ref Quaternion ecef, IntPtr mapToEcef);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvSetInteger([MarshalAs(UnmanagedType.LPStr)] string parameter, int value);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvGetInteger([MarshalAs(UnmanagedType.LPStr)] string parameter);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvValidateUser([MarshalAs(UnmanagedType.LPStr)] string token);
        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]

        public static extern int icvMapAddImage(IntPtr pixels, int width, int height, int channels, ref Vector4 intrinsics, ref Vector3 pos, ref Quaternion rot);
        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvMapImageGetCount();

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvMapPrepare([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvMapGet(byte[] map);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvMapPointsGetCount();

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvMapPointsGet(IntPtr points, int countMax);

        [DllImport(Assembly, CallingConvention = CallingConvention.Cdecl)]
        public static extern int icvMapResourcesFree();
    }
}