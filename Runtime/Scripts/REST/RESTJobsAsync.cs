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
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Text;
using UnityEngine;

namespace Immersal.REST
{
    public interface IJobAsync
    {
        public Task RunJob(CancellationToken cancellationToken = default);
    }

    public class ImmersalHttp
    {
        public static readonly string URL_FORMAT = "{0}/{1}";

        public static async Task<U> Request<T, U>(T request, IProgress<float> progress, CancellationToken cancellationToken)
        {
            U result = default(U);
            string jsonString = JsonUtility.ToJson(request);
            HttpRequestMessage r = new HttpRequestMessage(HttpMethod.Post, string.Format(URL_FORMAT, ImmersalSDK.Instance.localizationServer, (string)typeof(T).GetField("endpoint").GetValue(null)));
            r.Content = new StringContent(jsonString);
            
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var response = await ImmersalSDK.client.DownloadAsync(r, stream, progress, cancellationToken))
                    {
                        string responseBody = Encoding.ASCII.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                        result = JsonUtility.FromJson<U>(responseBody);
                        if (!response.IsSuccessStatusCode)
                        {
                            ImmersalLogger.LogWarning($"ImmersalHttp error: {(int)response.StatusCode} ({response.ReasonPhrase}), {response.RequestMessage}\nrequest JSON: {jsonString}\nresponse JSON: {responseBody}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ImmersalLogger.LogError($"ImmersalHttp connection error: {e.Message}");
            }

            return result;
        }

        public static async Task<byte[]> RequestGet(string uri, IProgress<float> progress, CancellationToken cancellationToken)
        {
            byte[] result = null;
            HttpRequestMessage r = new HttpRequestMessage(HttpMethod.Get, uri);
            
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var response = await ImmersalSDK.client.DownloadAsync(r, stream, progress, cancellationToken))
                    {
                        result = stream.GetBuffer();
                        Array.Resize(ref result, (int)stream.Length);

                        if (!response.IsSuccessStatusCode)
                        {
                            ImmersalLogger.LogWarning($"ImmersalHttp error: {(int)response.StatusCode} ({response.ReasonPhrase}), {response.RequestMessage}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ImmersalLogger.LogError($"ImmersalHttp connection error: {e.Message}");
            }

            return result;
        }

        public static async Task<U> RequestUpload<T, U>(T request, byte[] data, IProgress<float> progress, CancellationToken cancellationToken)
        {
            U result = default(U);
            string jsonString = JsonUtility.ToJson(request);
            byte[] jsonBytes = Encoding.ASCII.GetBytes(jsonString);
            byte[] body = new byte[jsonBytes.Length + 1 + data.Length];
            Array.Copy(jsonBytes, 0, body, 0, jsonBytes.Length);
            body[jsonBytes.Length] = 0;
            Array.Copy(data, 0, body, jsonBytes.Length + 1, data.Length);
            HttpRequestMessage r = new HttpRequestMessage(HttpMethod.Post, string.Format(URL_FORMAT, ImmersalSDK.Instance.localizationServer, (string)typeof(T).GetField("endpoint").GetValue(null)));
            var byteStream = new ProgressMemoryStream(body, progress);
            r.Content = new StreamContent(byteStream);

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var response = await ImmersalSDK.client.DownloadAsync(r, stream, null, cancellationToken))
                    {
                        string responseBody = Encoding.ASCII.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                        result = JsonUtility.FromJson<U>(responseBody);
                        if (!response.IsSuccessStatusCode)
                        {
                            ImmersalLogger.LogWarning($"ImmersalHttp error: {(int)response.StatusCode} ({response.ReasonPhrase}), {response.RequestMessage}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ImmersalLogger.LogError($"ImmersalHttp connection error: {e.Message}");
            }

            return result;
        }
    }

    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> DownloadAsync(this HttpClient client, HttpRequestMessage request, Stream destination, IProgress<float> progress = null, CancellationToken cancellationToken = default) {
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                request.Dispose();
                
                var contentLength = response.Content.Headers.ContentLength;

                using (var download = await response.Content.ReadAsStreamAsync())
                {
                    if (progress == null || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination);
                        return response;
                    }

                    var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
                    await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
                }

                return response;
            }
        }
    }

    public class ProgressMemoryStream : MemoryStream
    {
        IProgress<float> progress;
        private int length;

        public ProgressMemoryStream(byte[] buffer, IProgress<float> progress = null)
            : base(buffer, true) {
            
            this.length = buffer.Length;
            this.progress = progress;
        }

        public override int Read([In, Out] byte[] buffer, int offset, int count) {
            int n = base.Read(buffer, offset, count);
            progress?.Report((float)this.Position / this.length);
            return n;
        }
    }

    public static class StreamExtensions
    {
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long> progress = null, CancellationToken cancellationToken = default) {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.CanRead)
                throw new ArgumentException("Has to be readable", nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new ArgumentException("Has to be writable", nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0) {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
    }

    public abstract class JobAsync<TResult>
    {
        public string token = ImmersalSDK.Instance.developerToken;
        public Action OnStart;
        public Action<string> OnError;
        public Progress<float> Progress = new Progress<float>();
        public Action<TResult> OnResult;

        public virtual Task RunJob(CancellationToken cancellationToken = default)
        {
            return RunJobAsync(cancellationToken);
        }

        public virtual async Task<TResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return default(TResult);
        }

        protected void HandleError(string e)
        {
            string error = e ?? "conn";
            ImmersalLogger.LogWarning(error);
            OnError?.Invoke(error);
        }
    }

    public class JobSetMapAccessTokenAsync : JobAsync<SDKMapAccessTokenResult>, IJobAsync
    {
        public int id;

        public Task GetTask()
        {
            Task t = new Task( async () => await RunJobAsync() );
            return t;
        }

        public override async Task<SDKMapAccessTokenResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            ImmersalLogger.Log("*************************** JobSetMapAccessTokenAsync ***************************");
            this.OnStart?.Invoke();

            SDKSetMapAccessTokenRequest r = new SDKSetMapAccessTokenRequest();
            r.token = this.token;
            r.id = this.id;

            SDKMapAccessTokenResult result = await ImmersalHttp.Request<SDKSetMapAccessTokenRequest, SDKMapAccessTokenResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }
    
    public class JobClearMapAccessTokenAsync : JobAsync<SDKMapAccessTokenResult>, IJobAsync
    {
        public int id;

        public override async Task<SDKMapAccessTokenResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            ImmersalLogger.Log("*************************** JobClearMapAccessTokenAsync ***************************");
            this.OnStart?.Invoke();

            SDKClearMapAccessTokenRequest r = new SDKClearMapAccessTokenRequest();
            r.token = this.token;
            r.id = this.id;

            SDKMapAccessTokenResult result = await ImmersalHttp.Request<SDKClearMapAccessTokenRequest, SDKMapAccessTokenResult>(r, this.Progress, cancellationToken);
            
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }
    
    public class JobClearAsync : JobAsync<SDKClearResult>, IJobAsync
    {
        public bool anchor;

        public override async Task<SDKClearResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKClearRequest r = new SDKClearRequest();
            r.token = this.token;
            r.anchor = this.anchor;

            SDKClearResult result = await ImmersalHttp.Request<SDKClearRequest, SDKClearResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobConstructAsync : JobAsync<SDKConstructResult>, IJobAsync
    {
        public string name;
        public int featureCount = 1024;
        public int featureType = 2;
        public int windowSize = 0;
        public bool preservePoses = false;
        public bool mapTrim = false;
        public int featureFilter = 0;
        public int compressionLevel = 0;
        public bool constructDense = true;

        public override async Task<SDKConstructResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKConstructRequest r = new SDKConstructRequest();
            r.token = this.token;
            r.name = this.name;
            r.featureCount = this.featureCount;
            r.featureType = this.featureType;
            r.windowSize = this.windowSize;
            r.preservePoses = this.preservePoses;
            r.mapTrim = this.mapTrim;
            r.featureFilter = this.featureFilter;
            r.compressionLevel = this.compressionLevel;
            r.dense = constructDense ? 1 : 0;

            SDKConstructResult result = await ImmersalHttp.Request<SDKConstructRequest, SDKConstructResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobRestoreMapImagesAsync : JobAsync<SDKRestoreMapImagesResult>, IJobAsync
    {
        public int id;
        public bool clear;

        public override async Task<SDKRestoreMapImagesResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKRestoreMapImagesRequest r = new SDKRestoreMapImagesRequest();
            r.token = this.token;
            r.id = this.id;
            r.clear = this.clear;

            SDKRestoreMapImagesResult result = await ImmersalHttp.Request<SDKRestoreMapImagesRequest, SDKRestoreMapImagesResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobDeleteMapAsync : JobAsync<SDKDeleteMapResult>, IJobAsync
    {
        public int id;

        public override async Task<SDKDeleteMapResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKDeleteMapRequest r = new SDKDeleteMapRequest();
            r.token = this.token;
            r.id = this.id;

            SDKDeleteMapResult result = await ImmersalHttp.Request<SDKDeleteMapRequest, SDKDeleteMapResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobStatusAsync : JobAsync<SDKStatusResult>, IJobAsync
    {
        public override async Task<SDKStatusResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKStatusRequest r = new SDKStatusRequest();
            r.token = this.token;

            SDKStatusResult result = await ImmersalHttp.Request<SDKStatusRequest, SDKStatusResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobMapUploadAsync : JobAsync<SDKMapUploadResult>, IJobAsync
    {
        public string name;
        public double latitude = 0.0;
        public double longitude = 0.0;
        public double altitude = 0.0;
        public byte[] mapData;

        public override async Task<SDKMapUploadResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapUploadRequest r = new SDKMapUploadRequest();
            r.token = this.token;
            r.name = this.name;
            r.latitude = this.latitude;
            r.longitude = this.longitude;
            r.altitude = this.altitude;

            ImmersalLogger.Log($"Uploading map {r.name} with token {r.token}");

            SDKMapUploadResult result = await ImmersalHttp.RequestUpload<SDKMapUploadRequest, SDKMapUploadResult>(r, mapData, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobCaptureAsync : JobAsync<SDKImageResult>, IJobAsync
    {
        public int run;
        public int index;
        public bool anchor;
        public Vector4 intrinsics;
        public Matrix4x4 rotation;
        public Vector3 position;
        public double latitude;
        public double longitude;
        public double altitude;
        public string encodedImage;
        public string imagePath;

        public override async Task<SDKImageResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKImageRequest r = new SDKImageRequest();
            r.token = this.token;
            r.deviceModel = DeviceInfo.DeviceModel;
            r.run = this.run;
            r.index = this.index;
            r.anchor = this.anchor;
            r.px = position.x;
            r.py = position.y;
            r.pz = position.z;
            r.r00 = rotation.m00;
            r.r01 = rotation.m01;
            r.r02 = rotation.m02;
            r.r10 = rotation.m10;
            r.r11 = rotation.m11;
            r.r12 = rotation.m12;
            r.r20 = rotation.m20;
            r.r21 = rotation.m21;
            r.r22 = rotation.m22;
            r.fx = intrinsics.x;
            r.fy = intrinsics.y;
            r.ox = intrinsics.z;
            r.oy = intrinsics.w;
            r.latitude = latitude;
            r.longitude = longitude;
            r.altitude = altitude;

            byte[] image = File.ReadAllBytes(imagePath);

            SDKImageResult result = await ImmersalHttp.RequestUpload<SDKImageRequest, SDKImageResult>(r, image, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobLocalizeServerAsync : JobAsync<SDKLocalizeResult>, IJobAsync
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector4 intrinsics;
        public double latitude = 0.0;
        public double longitude = 0.0;
        public double radius = 0.0;
        public bool useGPS = false;
        public SDKMapId[] mapIds;
        public byte[] image;
        public int solverType = 0;
        public Vector3 priorPos;
        public int priorNNCount;
        public float priorRadius;

        public override async Task<SDKLocalizeResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKLocalizeResult result = default;

            if (this.useGPS)
            {
                SDKGeoLocalizeRequest r = new SDKGeoLocalizeRequest();
                r.token = this.token;
                r.fx = intrinsics.x;
                r.fy = intrinsics.y;
                r.ox = intrinsics.z;
                r.oy = intrinsics.w;
                r.latitude = this.latitude;
                r.longitude = this.longitude;
                r.radius = this.radius;
                r.qx = rotation.x;
                r.qy = rotation.y;
                r.qz = rotation.z;
                r.qw = rotation.w;
                r.solverType = this.solverType;
                result = await ImmersalHttp.RequestUpload<SDKGeoLocalizeRequest, SDKLocalizeResult>(r, this.image, this.Progress, cancellationToken);
            }
            else
            {
                SDKLocalizeRequest r = new SDKLocalizeRequest();
                r.token = this.token;
                r.deviceModel = DeviceInfo.DeviceModel;
                r.fx = intrinsics.x;
                r.fy = intrinsics.y;
                r.ox = intrinsics.z;
                r.oy = intrinsics.w;
                r.mapIds = this.mapIds;
                r.qx = rotation.x;
                r.qy = rotation.y;
                r.qz = rotation.z;
                r.qw = rotation.w;
                r.solverType = this.solverType;
                r.priorX = priorPos.x;
                r.priorY = priorPos.y;
                r.priorZ = priorPos.z;
                r.priorNeighborCountMin = priorNNCount;
                r.priorRadius = priorRadius;
                result = await ImmersalHttp.RequestUpload<SDKLocalizeRequest, SDKLocalizeResult>(r, this.image, this.Progress, cancellationToken);
            }

            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobGeoPoseAsync : JobAsync<SDKGeoPoseResult>, IJobAsync
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector4 intrinsics;
        public SDKMapId[] mapIds;
        public byte[] image;
        public int solverType = 0;
        public Vector3 priorPos;
        public int priorNNCount;
        public float priorRadius;

        public override async Task<SDKGeoPoseResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKGeoPoseRequest r = new SDKGeoPoseRequest();
            r.token = this.token;
            r.fx = intrinsics.x;
            r.fy = intrinsics.y;
            r.ox = intrinsics.z;
            r.oy = intrinsics.w;
            r.mapIds = this.mapIds;
            r.qx = rotation.x;
            r.qy = rotation.y;
            r.qz = rotation.z;
            r.qw = rotation.w;
            r.solverType = this.solverType;
            r.priorX = priorPos.x;
            r.priorY = priorPos.y;
            r.priorZ = priorPos.z;
            r.priorNeighborCountMin = priorNNCount;
            r.priorRadius = priorRadius;

            SDKGeoPoseResult result = await ImmersalHttp.RequestUpload<SDKGeoPoseRequest, SDKGeoPoseResult>(r, this.image, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobEcefAsync : JobAsync<SDKEcefResult>, IJobAsync
    {
        public int id;
        public bool useToken = true;

        public override async Task<SDKEcefResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKEcefRequest r = new SDKEcefRequest();
            r.token = useToken ? this.token : "";
            r.id = this.id;

            SDKEcefResult result = await ImmersalHttp.Request<SDKEcefRequest, SDKEcefResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobListJobsAsync : JobAsync<SDKJobsResult>, IJobAsync
    {
        public double latitude = 0.0;
        public double longitude = 0.0;
        public double radius = 0.0;
        public bool useGPS = false;
        public bool useToken = true;

        public override async Task<SDKJobsResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKJobsResult result = default;

            if (this.useGPS)
            {
                SDKGeoJobsRequest r = new SDKGeoJobsRequest();
                r.token = this.useToken ? this.token : "";
                r.latitude = this.latitude;
                r.longitude = this.longitude;
                r.radius = this.radius;
                result = await ImmersalHttp.Request<SDKGeoJobsRequest, SDKJobsResult>(r, this.Progress, cancellationToken);
            }
            else
            {
                SDKJobsRequest r = new SDKJobsRequest();
                r.token = this.useToken ? this.token : "";
                result = await ImmersalHttp.Request<SDKJobsRequest, SDKJobsResult>(r, this.Progress, cancellationToken);
            }

            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobLoadMapBinaryAsync : JobAsync<SDKMapResult>, IJobAsync
    {
        public int id;
        public bool useToken = true;
        public string sha256_al;

        public override async Task<SDKMapResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapBinaryRequest r = new SDKMapBinaryRequest();
            r.token = this.useToken ?  this.token : "";
            r.id = this.id;

            string uri = string.Format(ImmersalHttp.URL_FORMAT, ImmersalSDK.Instance.localizationServer, SDKMapBinaryRequest.endpoint);
            uri += (r.token != "") ? string.Format("?token={0}&id={1}", r.token, r.id) : string.Format("?id={0}", r.id);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            SDKMapResult result = default;
            byte[] data = await ImmersalHttp.RequestGet(uri, this.Progress, cancellationToken);

            if (data == null || data.Length == 0)
            {
                result.error = "no data";
            }
            else if (data.Length <= 256) // error
            {
                var str = Encoding.Default.GetString(data);
                result = JsonUtility.FromJson<SDKMapResult>(str);
            }
            else
            {
                result.error = "none";
                result.sha256_al = this.sha256_al;
                result.mapData = data;
            }

            if (result.error == "none")
            {
                JobMapMetadataGetAsync j = new JobMapMetadataGetAsync();
                j.id = this.id;
                j.token = r.token;

                SDKMapMetadataGetResult metadata = await j.RunJobAsync();
                if (metadata.error == "none")
                {
                    result.metadata = metadata;
                }

                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobLoadMapAsync : JobAsync<SDKMapResult>, IJobAsync
    {
        public int id;
        public bool useToken = true;

        public override async Task<SDKMapResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapRequest r = new SDKMapRequest();
            r.token = this.useToken ? this.token : "";
            r.id = this.id;

            SDKMapResult result = await ImmersalHttp.Request<SDKMapRequest, SDKMapResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                JobMapMetadataGetAsync j = new JobMapMetadataGetAsync();
                j.id = this.id;
                j.token = r.token;

                SDKMapMetadataGetResult metadata = await j.RunJobAsync();
                if (metadata.error == "none")
                {
                    result.metadata = metadata;
                }

                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobSetPrivacyAsync : JobAsync<SDKMapPrivacyResult>, IJobAsync
    {
        public int id;
        public int privacy;

        public override async Task<SDKMapPrivacyResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapPrivacyRequest r = new SDKMapPrivacyRequest();
            r.token = this.token;
            r.id = this.id;
            r.privacy = this.privacy;

            SDKMapPrivacyResult result = await ImmersalHttp.Request<SDKMapPrivacyRequest, SDKMapPrivacyResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobLoginAsync : JobAsync<SDKLoginResult>, IJobAsync
    {
        public string username;
        public string password;

        public override async Task<SDKLoginResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKLoginRequest r = new SDKLoginRequest();
            r.login = this.username;
            r.password = this.password;

            SDKLoginResult result = await ImmersalHttp.Request<SDKLoginRequest, SDKLoginResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobMapDownloadAsync : JobAsync<SDKMapDownloadResult>, IJobAsync
    {
        public int id;

        public override async Task<SDKMapDownloadResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapDownloadRequest r = new SDKMapDownloadRequest();
            r.token = this.token;
            r.id = this.id;

            SDKMapDownloadResult result = await ImmersalHttp.Request<SDKMapDownloadRequest, SDKMapDownloadResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobMapMetadataGetAsync : JobAsync<SDKMapMetadataGetResult>, IJobAsync
    {
        public int id;

        public override async Task<SDKMapMetadataGetResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapMetadataGetRequest r = new SDKMapMetadataGetRequest();
            r.token = this.token;
            r.id = this.id;

            SDKMapMetadataGetResult result = await ImmersalHttp.Request<SDKMapMetadataGetRequest, SDKMapMetadataGetResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobMapAlignmentSetAsync : JobAsync<SDKMapAlignmentSetResult>, IJobAsync
    {
        public int id;
        public double tx;
        public double ty;
        public double tz;
        public double qw;
        public double qx;
        public double qy;
        public double qz;
        public double scale;

        public override async Task<SDKMapAlignmentSetResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKMapAlignmentSetRequest r = new SDKMapAlignmentSetRequest();
            r.token = this.token;
            r.id = this.id;
            r.tx = this.tx;
            r.ty = this.ty;
            r.tz = this.tz;
            r.qw = this.qw;
            r.qx = this.qx;
            r.qy = this.qy;
            r.qz = this.qz;
            r.scale = this.scale;

            SDKMapAlignmentSetResult result = await ImmersalHttp.Request<SDKMapAlignmentSetRequest, SDKMapAlignmentSetResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobLoadMapSparseAsync : JobAsync<SDKSparseDownloadResult>, IJobAsync
    {
        public int id;
        public bool useToken = true;

        public override async Task<SDKSparseDownloadResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKSparseDownloadRequest r = new SDKSparseDownloadRequest();
            r.token = this.useToken ? this.token : "";
            r.id = this.id;

            string uri = string.Format(ImmersalHttp.URL_FORMAT, ImmersalSDK.Instance.localizationServer,
                SDKSparseDownloadRequest.endpoint);
            uri += (r.token != "") ? string.Format("?token={0}&id={1}", r.token, r.id) : string.Format("?id={0}", r.id);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            SDKSparseDownloadResult result = default;
            byte[] data = await ImmersalHttp.RequestGet(uri, this.Progress, cancellationToken);

            if (data == null || data.Length == 0)
            {
                result.error = "no data";
            }
            else
            {
                result.error = "none";
                result.data = data;
            }

            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }
    
    public class JobCopyMapAsync : JobAsync<SDKCopyMapResult>, IJobAsync
    {
        public string login;
        public int id;

        public override async Task<SDKCopyMapResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKCopyMapRequest r = new SDKCopyMapRequest();
            r.token = this.token;
            r.login = this.login;
            r.id = this.id;

            SDKCopyMapResult result = await ImmersalHttp.Request<SDKCopyMapRequest, SDKCopyMapResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }
    
    public class JobVersionAsync : JobAsync<SDKVersionResult>, IJobAsync
    {
        public override async Task<SDKVersionResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKVersionRequest r = new SDKVersionRequest();
            SDKVersionResult result = await ImmersalHttp.Request<SDKVersionRequest, SDKVersionResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }
    
    public class JobAlignMapsAsync : JobAsync<SDKAlignMapsResult>, IJobAsync
    {
        public string name;
        public SDKMapId[] mapIds;

        public override async Task<SDKAlignMapsResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKAlignMapsRequest r = new SDKAlignMapsRequest();
            r.token = this.token;
            r.name = this.name;
            r.mapIds = this.mapIds;

            SDKAlignMapsResult result = await ImmersalHttp.Request<SDKAlignMapsRequest, SDKAlignMapsResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }

    public class JobStitchMapsAsync : JobAsync<SDKStitchMapsResult>, IJobAsync
    {
        public string name;
        public SDKMapId[] mapIds;

        public override async Task<SDKStitchMapsResult> RunJobAsync(CancellationToken cancellationToken = default)
        {
            this.OnStart?.Invoke();

            SDKStitchMapsRequest r = new SDKStitchMapsRequest();
            r.token = this.token;
            r.name = this.name;
            r.mapIds = this.mapIds;

            SDKStitchMapsResult result = await ImmersalHttp.Request<SDKStitchMapsRequest, SDKStitchMapsResult>(r, this.Progress, cancellationToken);
            if (result.error == "none")
            {
                this.OnResult?.Invoke(result);
            }
            else
            {
                HandleError(result.error);
            }

            return result;
        }
    }
}
