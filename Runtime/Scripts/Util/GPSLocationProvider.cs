/*===============================================================================
Copyright (C) 2026 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;
using System.Collections;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

namespace Immersal
{
    public class GPSLocationProvider : MonoBehaviour
    {
        [SerializeField] private bool m_EnableGPS = true;

        public double Latitude { get; private set; } = 0.0;
        public double Longitude { get; private set; } = 0.0;
        public double Altitude { get; private set; } = 0.0;
        public double HorizontalAccuracy { get; private set; } = 0.0;
        public double VerticalAccuracy { get; private set; } = 0.0;
        public bool IsInitialized { get; private set; } = false;
        public bool HasValidLocation { get; private set; } = false;
        public bool GpsOn
        {
#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
            get { return NativeBindings.LocationServicesEnabled(); }
#else
            get
            {
                return Input.location.status == LocationServiceStatus.Running;
            }
#endif
        }

        // Events
        public static event System.Action OnGPSInitialized;
        public static event System.Action<double, double, double> OnLocationReceived;
        public static event System.Action<string> OnGPSError;

        public static GPSLocationProvider Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null && !Application.isPlaying)
                {
                    instance = UnityEngine.Object.FindObjectOfType<GPSLocationProvider>();
                }
#endif
                if (instance == null)
                {
                    ImmersalLogger.LogError("No GPSLocationProvider instance found. Ensure one exists in the scene.");
                }
                return instance;
            }
        }

        private static GPSLocationProvider instance = null;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            if (instance != this)
            {
                ImmersalLogger.LogError("There must be only one GPSLocationProvider object in a scene.");
                UnityEngine.Object.DestroyImmediate(this);
                return;
            }
        }

        private void OnEnable()
        {
            if (m_EnableGPS)
            {
                Invoke("StartGPS", 0.1f);
            }
        }

        public void StartGPS()
        {
#if UNITY_IOS
            StartCoroutine(EnableLocationServices());
#elif PLATFORM_ANDROID
            if (Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                ImmersalLogger.Log("Location permission OK");
                StartCoroutine(EnableLocationServices());
            }
            else
            {
                ImmersalLogger.Log("Requesting location permission");
                Permission.RequestUserPermission(Permission.FineLocation);
                StartCoroutine(WaitForLocationPermission());
            }
#endif
        }

        public void StopGPS()
        {
#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
            NativeBindings.StopLocation();
#else
            Input.location.Stop();
#endif
            m_EnableGPS = false;
            IsInitialized = false;
            HasValidLocation = false;
            ImmersalLogger.Log("GPS tracking stopped");
        }

        private void Update()
        {
            UpdateLocation();
        }

        private bool IsValidCoordinate(double latitude, double longitude)
        {
            // Basic validation - coordinates shouldn't be exactly 0,0 and should be within valid ranges
            return !(latitude == 0.0 && longitude == 0.0) && 
                   latitude >= -90.0 && latitude <= 90.0 && 
                   longitude >= -180.0 && longitude <= 180.0;
        }

        private void UpdateLocation()
        {
            if (GpsOn)
            {
                double newLatitude, newLongitude;

#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
                newLatitude = NativeBindings.GetLatitude();
                newLongitude = NativeBindings.GetLongitude();
                Altitude = NativeBindings.GetAltitude();
                HorizontalAccuracy = NativeBindings.GetHorizontalAccuracy();
                VerticalAccuracy = NativeBindings.GetVerticalAccuracy();
#else
                newLatitude = Input.location.lastData.latitude;
                newLongitude = Input.location.lastData.longitude;
                Altitude = Input.location.lastData.altitude;
                HorizontalAccuracy = Input.location.lastData.horizontalAccuracy;
                VerticalAccuracy = Input.location.lastData.verticalAccuracy;
#endif
                
                // Check if we have valid coordinates (not 0,0 which is often default/invalid)
                if (IsValidCoordinate(newLatitude, newLongitude))
                {
                    Latitude = newLatitude;
                    Longitude = newLongitude;
                    
                    if (!HasValidLocation)
                    {
                        HasValidLocation = true;
                        ImmersalLogger.Log($"First valid location received: {Latitude}, {Longitude}, {Altitude}");
                        OnLocationReceived?.Invoke(Latitude, Longitude, Altitude);
                    }
                }
            }
        }

#if PLATFORM_ANDROID
        private IEnumerator WaitForLocationPermission()
        {
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                yield return null;
            }

            ImmersalLogger.Log("Location permission OK");
            StartCoroutine(EnableLocationServices());
            yield return null;
        }
#endif

        private IEnumerator EnableLocationServices()
        {
            // First, check if user has location service enabled
            if (!Input.location.isEnabledByUser)
            {
                m_EnableGPS = false;
                ImmersalLogger.LogWarning("Location services not enabled by user");
                OnGPSError?.Invoke("Location services not enabled by user");
                yield break;
            }

            // Start service before querying location
#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
            NativeBindings.StartLocation();
#else
            Input.location.Start(0.001f, 0.001f);
#endif

            // Wait until service initializes
            int maxWait = 20;
#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
            while (!NativeBindings.LocationServicesEnabled() && maxWait > 0)
#else
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
#endif
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            // Service didn't initialize in 20 seconds
            if (maxWait < 1)
            {
                m_EnableGPS = false;
                ImmersalLogger.LogWarning("Location services timed out");
                OnGPSError?.Invoke("Location services initialization timed out");
                yield break;
            }

            // Connection has failed
#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
            if (!NativeBindings.LocationServicesEnabled())
#else
            if (Input.location.status == LocationServiceStatus.Failed)
#endif
            {
                m_EnableGPS = false;
                ImmersalLogger.LogWarning("Unable to determine device location");
                OnGPSError?.Invoke("Unable to determine device location");
                yield break;
            }

#if (UNITY_IOS || PLATFORM_ANDROID) && !UNITY_EDITOR
            if (NativeBindings.LocationServicesEnabled())
#else
            if (Input.location.status == LocationServiceStatus.Running)
#endif
            {
                m_EnableGPS = true;
                IsInitialized = true;
                ImmersalLogger.Log("GPS initialized successfully");
                OnGPSInitialized?.Invoke();
            }
        }
    }
}