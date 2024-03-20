/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using UnityEngine;

namespace Immersal.REST
{
    public struct SDKJobState
    {
        public const string Done = "done";
        public const string Sparse = "sparse";
        public const string Processing = "processing";
        public const string Failed = "failed";
        public const string Pending = "pending";
    }

    public enum SDKJobType { Map, Stitch, Alignment, Edit };
    public enum SDKJobPrivacy { Private, Public };

    public struct DeviceInfo
    {
        public static string DeviceModel => SystemInfo.deviceModel != SystemInfo.unsupportedIdentifier
            ? SystemInfo.deviceModel
            : "unknown";
    }

    [Serializable]
    public struct SDKJob
    {
        public int id;
        public int type;
        public string version;
        public int creator;
        public int size;
        public string status;
        public int privacy;
        public string name;
        public double latitude;
        public double longitude;
        public double altitude;
        public string created;
        public string modified;
        public string sha256_al;
        public string sha256_sparse;
        public string sha256_dense;
        public string sha256_tex;
    }

    [Serializable]
    public struct SDKMapId
    {
        public int id;
    }

    [Serializable]
    public struct SDKLoginRequest
    {
        public static string endpoint = "login";
        public string login;
        public string password;
    }

    [Serializable]
    public struct SDKLoginResult
    {
        public string error;
        public int userId;
        public string token;
        public int level;
    }

    [Serializable]
    public struct SDKClearRequest
    {
        public static string endpoint = "clear";
        public string token;
        public bool anchor;
    }

    [Serializable]
    public struct SDKClearResult
    {
        public string error;
    }

    [Serializable]
    public struct SDKConstructRequest
    {
        public static string endpoint = "construct";
        public string token;
        public string name;
        public int featureCount;
        public int featureType;
        public bool preservePoses;
        public int windowSize;
        public bool mapTrim;
        public int featureFilter;
        public int compressionLevel;
    }

    [Serializable]
    public struct SDKConstructResult
    {
        public string error;
        public int id;
        public int size;
    }

    [Serializable]
    public struct SDKStatusRequest
    {
        public static string endpoint = "status";
        public string token;
    }

    [Serializable]
    public struct SDKStatusResult
    {
        public string error;
        public int userId;
        public int imageCount;
        public int imageMax;
        public bool eulaAccepted;
        public int level;
    }

    [Serializable]
    public struct SDKJobsRequest
    {
        public static string endpoint = "list";
        public string token;
    }

    [Serializable]
    public struct SDKGeoJobsRequest
    {
        public static string endpoint = "geolist";
        public string token;
        public double latitude;
        public double longitude;
        public double radius;
    }

    [Serializable]
    public struct SDKJobsResult
    {
        public string error;
        public int count;
        public SDKJob[] jobs;
    }

    [Serializable]
    public struct SDKImageRequest
    {
        public static string endpoint = "capture";
        public string deviceModel;
        public string token;
        public int run;
        public int index;
        public bool anchor;
        public double px;
        public double py;
        public double pz;
        public double r00;
        public double r01;
        public double r02;
        public double r10;
        public double r11;
        public double r12;
        public double r20;
        public double r21;
        public double r22;
        public double fx;
        public double fy;
        public double ox;
        public double oy;
        public double latitude;
        public double longitude;
        public double altitude;
    }

    [Serializable]
    public struct SDKImageResult
    {
        public string error;
        public string path;
    }
    
    [Serializable]
    public struct SDKGeoLocalizeRequest
    {
        public static string endpoint = "geolocalize";
        public string token;
        public double fx;
        public double fy;
        public double ox;
        public double oy;
        public double latitude;
        public double longitude;
        public double radius;
        public double qx;
        public double qy;
        public double qz;
        public double qw;
        public int solverType;
    }

    [Serializable]
    public struct SDKLocalizeRequest
    {
        public static string endpoint = "localize";
        public string deviceModel;
        public string token;
        public double fx;
        public double fy;
        public double ox;
        public double oy;
        public SDKMapId[] mapIds;
        public double qx;
        public double qy;
        public double qz;
        public double qw;
        public int solverType;
    }

    [Serializable]
    public struct SDKGeoPoseRequest
    {
        public static string endpoint = "geopose";
        public string token;
        public double fx;
        public double fy;
        public double ox;
        public double oy;
        public SDKMapId[] mapIds;
        public double qx;
        public double qy;
        public double qz;
        public double qw;
        public int solverType;
    }

    [Serializable]
    public struct SDKLocalizeResult
    {
        public string error;
        public bool success;
        public int map;
        public float px;
        public float py;
        public float pz;
        public float r00;
        public float r01;
        public float r02;
        public float r10;
        public float r11;
        public float r12;
        public float r20;
        public float r21;
        public float r22;
        public int confidence;
        public float time;
    }

    [Serializable]
    public struct SDKGeoPoseResult
    {
        public string error;
        public bool success;
        public int map;
        public double latitude;
        public double longitude;
        public double ellipsoidHeight;
        public float[] quaternion;
    }

    [Serializable]
    public struct SDKEcefRequest
    {
        public static string endpoint = "ecef";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKEcefResult
    {
        public string error;
        public double[] ecef;
    }

    [Serializable]
    public struct SDKSetMapAccessTokenRequest
    {
        public static string endpoint = "setmaptoken";
        public string token;
        public int id;
    }
    
    [Serializable]
    public struct SDKClearMapAccessTokenRequest
    {
        public static string endpoint = "clearmaptoken";
        public string token;
        public int id;
    }
    
    [Serializable]
    public struct SDKMapAccessTokenResult
    {
        public string error;
        public int mapId;
        public string accessToken;
    }
    
    [Serializable]
    public struct SDKMapBinaryRequest
    {
        public static string endpoint = "map";
        public string token;
        public int id;
    }
    
    [Serializable]
    public struct SDKMapRequest
    {
        public static string endpoint = "mapb64";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKMapResult
    {
        public string error;
        public string sha256_al;
        public string b64;
        public byte[] mapData;
        public SDKMapMetadataGetResult metadata;
    }

    [Serializable]
    public struct SDKDeleteMapRequest
    {
        public static string endpoint = "delete";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKDeleteMapResult
    {
        public string error;
    }

    [Serializable]
    public struct SDKRestoreMapImagesRequest
    {
        public static string endpoint = "restore";
        public string token;
        public int id;
        public bool clear;
    }

    [Serializable]
    public struct SDKRestoreMapImagesResult
    {
        public string error;
    }

    [Serializable]
    public struct SDKMapPrivacyRequest
    {
        public static string endpoint = "privacy";
        public string token;
        public int id;
        public int privacy;
    }

    [Serializable]
    public struct SDKMapPrivacyResult
    {
        public string error;
    }

    [Serializable]
    public struct SDKMapDownloadRequest
    {
        public static string endpoint = "mapb64";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKMapDownloadResult
    {
        public string error;
        public string sha256_al;
        public string b64;
    }

    [Serializable]
    public struct SDKSparseDownloadRequest
    {
        public static string endpoint = "sparse";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKSparseDownloadResult
    {
        public string error;
        public byte[] data;
    }

    [Serializable]
    public struct SDKMapUploadRequest
    {
        public static string endpoint = "uploadmap";
        public string token;
        public string name;
        public double latitude;
        public double longitude;
        public double altitude;
    }

    [Serializable]
    public struct SDKMapUploadResult
    {
        public string error;
        public int id;
    }

    [Serializable]
    public struct SDKMapMetadataGetRequest
    {
        public static string endpoint = "metadataget";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKMapMetadataGetResult
    {
        public string error;
        public int id;
        public int type;
        public string created;
        public string version;
        public int user;
        public int creator;
        public string name;
        public int size;
        public string status;
        public int privacy;
        public double latitude;
        public double longitude;
        public double altitude;
        public double tx;
        public double ty;
        public double tz;
        public double qw;
        public double qx;
        public double qy;
        public double qz;
        public double scale;
        public string sha256_al;
        public string sha256_sparse;
        public string sha256_dense;
        public string sha256_tex;
    }

    [Serializable]
    public struct SDKMapAlignmentSetRequest
    {
        public static string endpoint = "metadataset";
        public string token;
        public int id;
        public double tx;
        public double ty;
        public double tz;
        public double qw;
        public double qx;
        public double qy;
        public double qz;
        public double scale;
    }

    [Serializable]
    public struct SDKMapAlignmentSetResult
    {
        public string error;
    }

    [Serializable]
    public struct SDKMapAlignmentResetRequest
    {
        public static string endpoint = "reset";
        public string token;
        public int id;
    }

    [Serializable]
    public struct SDKMapAlignmentResetResult
    {
        public string error;
    }
    
    [Serializable]
    public struct SDKCopyMapRequest
    {
        public static string endpoint = "copy";
        public string token;
        public string login;
        public int id;
    }
    
    [Serializable]
    public struct SDKCopyMapResult
    {
        public string error;
    }
    
    [Serializable]
    public struct SDKVersionRequest
    {
        public static string endpoint = "version";
    }
    
    [Serializable]
    public struct SDKVersionResult
    {
        public string error;
        public string version;
    }
    
    [Serializable]
    public struct SDKAlignMapsRequest
    {
        public static string endpoint = "align";
        public string token;
        public string name;
        public SDKMapId[] mapIds;
    }
    
    [Serializable]
    public struct SDKAlignMapsResult
    {
        public string error;
        public int id;
        public int size;
    }
    
    [Serializable]
    public struct SDKStitchMapsRequest
    {
        public static string endpoint = "fuse";
        public string token;
        public string name;
        public SDKMapId[] mapIds;
    }
    
    [Serializable]
    public struct SDKStitchMapsResult
    {
        public string error;
        public int id;
        public int size;
    }
}