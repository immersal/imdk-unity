/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;
using UnityEngine.Events;
using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Immersal.XR;
using Immersal.REST;
using AOT;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Immersal
{
	public class ImmersalSDK : MonoBehaviour
	{
		// SDK properties
		
		public static string sdkVersion = "2.3.0";
		private static readonly string[] ServerList = new[] {"https://api.immersal.com", "https://immersal.hexagon.com.cn"};
		public enum APIServer { DefaultServer = 0, ChinaServer = 1, CustomServer = 2 };
		
		public APIServer ImmersalServer = APIServer.DefaultServer;
		
		[Tooltip("SDK developer token")]
		public string developerToken;

		#region Interfaced components

		[Header("Components")]

		/*
         * ImmersalSDK modular framework works with any implementation of the main components:
		 * IImmersalSession, IPlatformSupport, ILocalizer, ISceneUpdater and ITrackingAnalyzer.
		 *
         * Serialization and inspector support for these implementation references are
         * handled with an Object reference Property with a custom Attribute as well as
         * a custom PropertyDrawer. An additional public Property is used for casting
         * the Object to the relevant interface type.
         */
		
		[SerializeField][InterfaceAttribute(typeof(IImmersalSession))]
		private Object m_Session;
		public IImmersalSession Session => m_Session as IImmersalSession;
        
		[SerializeField][InterfaceAttribute(typeof(IPlatformSupport))]
		private Object m_Platform;
		public IPlatformSupport PlatformSupport => m_Platform as IPlatformSupport;
        
		[SerializeField][InterfaceAttribute(typeof(ILocalizer))]
		private Object m_Localizer;
		public ILocalizer Localizer => m_Localizer as ILocalizer;
        
		[SerializeField][InterfaceAttribute(typeof(ISceneUpdater))]
		private Object m_SceneUpdater;
		public ISceneUpdater SceneUpdater => m_SceneUpdater as ISceneUpdater;
        
		[SerializeField][InterfaceAttribute(typeof(ITrackingAnalyzer))]
		private Object m_TrackingAnalyzer;
		public ITrackingAnalyzer TrackingAnalyzer => m_TrackingAnalyzer as ITrackingAnalyzer;

		#endregion

        // Configuration properties

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Initialize the SDK on Awake")]
        private bool m_InitializeAutomatically = true;
        
        [SerializeField]
        [Tooltip("Application target frame rate")]
        private int m_TargetFrameRate = 60;
        
        [Tooltip("Downsample image to HD resolution")]
        [SerializeField]
        private bool m_Downsample = true;

        [SerializeField]
        private ImmersalLogger.LoggingLevel m_LoggingLevel = ImmersalLogger.LoggingLevel.ErrorsAndWarnings;

        [Header("Editor")]
        [Tooltip("Map data download location in relation to Assets")]
        [SerializeField]
        private string m_DownloadDirectory = "Map Data";
        
        // Public

        public ITrackingStatus TrackingStatus => TrackingAnalyzer.TrackingStatus;
        public bool IsReady => m_IsReady;
        public int LicenseLevel => m_LicenseLevel;
        public bool HasValidated => m_HasValidated;
        public static HttpClient client;

        public int targetFrameRate
        {
	        get { return m_TargetFrameRate; }
	        set
	        {
		        m_TargetFrameRate = value;
		        SetFrameRate();
	        }
        }

        public string localizationServer
        {
	        get => ImmersalServer == APIServer.CustomServer ? m_CustomServerUrl : ServerList[(int)ImmersalServer];
	        set
	        {
		        ImmersalServer = APIServer.CustomServer;
		        m_CustomServerUrl = value;
	        }
        }
        
        public bool downsample
        {
	        get { return m_Downsample; }
	        set
	        {
		        m_Downsample = value;
		        SetDownsample();
	        }
        }

        public string DownloadDirectory
        {
	        get { return m_DownloadDirectory; }
        }
        
        private void SetDownsample()
        {
	        if (downsample)
	        {
		        Core.SetInteger("LocalizationMaxPixels", 960*720);
	        }
	        else
	        {
		        Core.SetInteger("LocalizationMaxPixels", 0);
	        }
        }

        // Events

        [Header("Events")]
        public UnityEvent OnInitializationComplete;
        public UnityEvent OnUserValidationComplete;
        public UnityEvent OnReset;
        
        [SerializeField]
        private string m_CustomServerUrl = "";
        private int m_LicenseLevel = -1;
        
        private bool m_IsReady = false;
        private bool m_HasValidated = false;
        private bool m_PluginCallbackRegistered = false;
        private bool m_PlatformIsConfigured = false;
        private bool m_LocalizerIsConfigured = false;
        private bool m_MapsAreRegisteredAndLoaded = false;
        
        
        #region Singleton pattern

        private static ImmersalSDK instance = null;
        
        public static ImmersalSDK Instance
        {
	        get
	        {
#if UNITY_EDITOR
		        if (instance == null && !Application.isPlaying)
		        {
			        instance = FindObjectOfType<ImmersalSDK>();
		        }
#endif
		        if (instance == null)
		        {
			        ImmersalLogger.LogError("No ImmersalSDK instance found. Ensure one exists in the scene.");
		        }
		        return instance;
	        }
        }

        #endregion
        
        private void Awake()
        {
	        if (instance == null)
	        {
		        instance = this;
	        }
	        
	        if (instance != this)
	        {
		        ImmersalLogger.LogError("There must be only one ImmersalSDK object in a scene.");
		        UnityEngine.Object.DestroyImmediate(this);
		        return;
	        }
	        
	        if (m_InitializeAutomatically)
				_ = Initialize();
        }
        
        public async Task Initialize()
        {
	        ImmersalLogger.Level = m_LoggingLevel;
	        ImmersalLogger.Log("Initializing ImmersalSDK", ImmersalLogger.LoggingLevel.Verbose);   
	        
	        if (PlatformSupport == null || Localizer == null || SceneUpdater == null || TrackingAnalyzer == null)
	        {
		        throw new Exception("ImmersalSDK has null references, please assign all Properties.");
	        }
	        
	        // plugin log callback
	        if (!m_PluginCallbackRegistered)
	        {
		        LogCallback callback_delegate = new LogCallback(Log);
		        IntPtr intptr_delegate = Marshal.GetFunctionPointerForDelegate(callback_delegate);
		        Native.PP_RegisterLogCallback(intptr_delegate);
		        m_PluginCallbackRegistered = true;
	        }
	        
			// http client
	        HttpClientHandler handler = new HttpClientHandler();
	        handler.ClientCertificateOptions = ClientCertificateOption.Automatic;
	        client = new HttpClient(handler);
	        client.DefaultRequestHeaders.ExpectContinue = false;
			client.Timeout = TimeSpan.FromDays(1);
			
	        // validate user
	        if (developerToken != null && developerToken.Length > 0)
	        {
		        PlayerPrefs.SetString("token", developerToken);
		        ValidateUser();
	        }
	        
	        SetFrameRate();
#if !UNITY_EDITOR
			SetDownsample();
#endif
	        MapManager.RefreshLocalizationMethods();
	        
	        await ConfigureComponents();

	        if (m_IsReady)
	        {
		        ImmersalLogger.Log("Immersal SDK ready!", ImmersalLogger.LoggingLevel.Verbose);
		        OnInitializationComplete?.Invoke();
	        }
        }
        
        public async void ValidateUser()
        {
	        if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android)
	        {
		        m_LicenseLevel = Core.ValidateUser(developerToken);
		        string licenseLevel = m_LicenseLevel >= 1 ? "Enterprise" : "Free";
		        ImmersalLogger.Log($"{licenseLevel} License");
	        }
	        else
	        {
		        JobStatusAsync j = new JobStatusAsync();
				SDKStatusResult result = await j.RunJobAsync();
				m_LicenseLevel = result.level;
				string licenseLevel = m_LicenseLevel >= 1 ? "Enterprise" : "Free";
				ImmersalLogger.Log($"{licenseLevel} License");
	        }

	        m_HasValidated = true;
	        OnUserValidationComplete?.Invoke();
        }

        private async Task ConfigureComponents()
        {
	        if (!m_MapsAreRegisteredAndLoaded)
	        {
		        m_MapsAreRegisteredAndLoaded = await RegisterAndLoadMaps();
	        }
	        
	        if (!m_PlatformIsConfigured)
	        {
		        m_PlatformIsConfigured = await ConfigurePlatform();
		        if (m_PlatformIsConfigured)
			        ImmersalLogger.Log("Platform configured.", ImmersalLogger.LoggingLevel.Verbose);
	        }
	        
	        if (!m_LocalizerIsConfigured)
	        {
		        m_LocalizerIsConfigured = await ConfigureLocalizer();
		        if (m_LocalizerIsConfigured)
			        ImmersalLogger.Log("Localizer configured.", ImmersalLogger.LoggingLevel.Verbose);
	        }
	        
	        m_IsReady = m_PlatformIsConfigured && m_LocalizerIsConfigured && m_MapsAreRegisteredAndLoaded;
        }
        
        private async Task<bool> RegisterAndLoadMaps()
        {
	        XRMap[] maps = FindObjectsOfType<XRMap>();
	        ImmersalLogger.Log($"Found {maps.Length} maps in scene.");
	        List<Task> mapRegisterTasks = new List<Task>();

	        foreach (XRMap map in maps)
	        {
		        ISceneUpdateable sceneUpdateable = map.gameObject.transform.GetComponentInParent<ISceneUpdateable>(true);
		        if (sceneUpdateable != null)
		        {
			        ImmersalLogger.Log($"Starting RegisterAndLoad for map {map.mapId}");
			        // map registering might initiate downloads so we should not await here
			        Task t = MapManager.RegisterAndLoadMap(map, sceneUpdateable);
			        mapRegisterTasks.Add(t);
		        }
	        }
	        // wait for all register tasks to finish	        
	        await Task.WhenAll(mapRegisterTasks.ToArray());
	        List<XRMap> registeredMaps = MapManager.GetRegisteredMaps();
	        ImmersalLogger.Log($"Registered maps: {(registeredMaps.Count > 0 ? string.Join(",", registeredMaps) : "none")}", ImmersalLogger.LoggingLevel.Verbose);
	        
	        return true;
        }
        
        private async Task<bool> ConfigurePlatform()
        {
	        try
	        {
		        IPlatformConfigureResult result = await PlatformSupport.ConfigurePlatform();
		        return result.Success;
	        }
	        catch (Exception e)
	        {
		        AbortWithException(e, "PlatformSupport");
	        }

	        return false;
        }

        private async Task<bool> ConfigureLocalizer()
        {
	        // Get all registered maps
            List<XRMap> maps = MapManager.GetRegisteredMaps();

            // Create a mapping of maps and the associated localization methods
            Dictionary<ILocalizationMethod, XRMap[]> mapping = maps
	            .GroupBy(map => map.LocalizationMethod)
	            .ToDictionary(
		            group => group.Key,
		            group => group.ToArray());

            DefaultLocalizerConfiguration config = new DefaultLocalizerConfiguration
            {
	            ConfigurationsToAdd = mapping,
	            StopRunningTasks = true
            };
            
            try
            {
                ILocalizerConfigurationResult result = await Localizer.ConfigureLocalizer(config);
                return result.Success;
            }
            catch (Exception e)
            {
	            AbortWithException(e, "Localizer");
            }

            return false;
        }
        
        private void SetFrameRate()
        {
	        Application.targetFrameRate = targetFrameRate;
        }
        
        // Convenience method
        // Note: no data processing chain here
        public async Task LocalizeOnce()
        {
	        if (m_IsReady)
	        {
		        ILocalizationResults localizerResults;
		        
		        IPlatformUpdateResult platformResult = await PlatformSupport.UpdatePlatform();
		        if (platformResult.Success)
		        {
			        localizerResults = await Localizer.Localize(platformResult.CameraData);

			        foreach (ILocalizationResult result in localizerResults.Results)
			        {
				        if (result.Success && MapManager.TryGetMapEntry(result.MapId, out MapEntry entry))
				        {
					        await SceneUpdater.UpdateScene(entry, platformResult.CameraData, result);
				        }			        
			        }
		        }
		        else
		        {
			        // Localization never ran due to failed platform update
			        // so we need invalidate results.
			        localizerResults = new LocalizationResults
			        {
				        Results = Array.Empty<ILocalizationResult>()
			        };
		        }
		        
		        // Update tracking status
		        TrackingAnalyzer.Analyze(platformResult.Status, localizerResults);
	        }
        }

        private void AbortWithException(Exception e, string logPrefix = "")
        {
	        m_IsReady = false;
	        ImmersalLogger.LogError($"{logPrefix}: {e.Message}");
	        Debug.LogException(e);
        }
        
        private void OnDestroy()
        {
            m_IsReady = false;
            MapManager.RemoveAllMaps(false);
        }

        public async void TriggerSoftReset()
        {
	        await SoftReset();
        }

        public async Task SoftReset()
        {
	        await Session.ResetSession();
	        await ResetScenes();
	        TrackingAnalyzer.Reset();
        }
        
        public async void TriggerResetScenes()
        {
	        await ResetScenes();
        }
        
        public async Task ResetScenes()
        {
	        ImmersalLogger.Log("Resetting SceneUpdateables");
	        List<ISceneUpdateable> sceneUpdateables = MapManager.GetSceneUpdateablesInUse();
	        foreach (ISceneUpdateable sceneUpdateable in sceneUpdateables)
	        {
		        await sceneUpdateable.ResetScene();
	        }
        }

        [ContextMenu("RestartSdk")]
        public async void RestartSdk()
        {
			ImmersalLogger.Log("Restarting ImmersalSDK", ImmersalLogger.LoggingLevel.Verbose);
	        m_IsReady = false;
	        
			// Stop and clean up components
			await Session.StopSession();
			
			// Clear maps
			MapManager.RemoveAllMaps();
			m_MapsAreRegisteredAndLoaded = false;
	        
	        m_LocalizerIsConfigured = false;
	        await Localizer.StopAndCleanUp();
	        
	        m_PlatformIsConfigured = false;
	        await PlatformSupport.StopAndCleanUp();
	        
	        // Reset scenes
	        await ResetScenes();
	        
	        // Invoke event
	        OnReset?.Invoke();
	        
	        // Restart
	        await Initialize();
        }
        
        // Logging callback for plugin
        [MonoPInvokeCallback(typeof(LogCallback))]
        public static void Log(IntPtr ansiString)
        {
	        string msg = Marshal.PtrToStringAnsi(ansiString);
	        ImmersalLogger.Log($"Plugin: {msg}", ImmersalLogger.LoggingLevel.Verbose);
        }
	}

	public class ComponentTaskCriticalException : Exception
	{
		public ComponentTaskCriticalException(string message) : base(message) { }
	}
}