/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
	public class ImmersalSession : MonoBehaviour, IImmersalSession
	{
		[Tooltip("Start Session at app startup")] [SerializeField]
		private bool m_AutoStart = true;

		[Tooltip("Seconds between continuous updates")] [SerializeField]
		private float m_SessionUpdateInterval = 2.0f;

		[Tooltip("Try to localize at maximum speed at app startup / resume")] [SerializeField]
		private bool m_BurstMode = true;
		
		[Tooltip("Number of successful localizations to turn off burst mode")] [SerializeField]
		private int m_BurstSuccessCount = 10;
		
		[Tooltip("Time limit for burst mode in seconds")] [SerializeField]
		private float m_BurstTimeLimit = 15f;

		[Tooltip("Reset stats and ARSpace filters on application pause")] [SerializeField]
		private bool m_ResetOnPause = true;

		[Tooltip("Restart session automatically after a reset")] [SerializeField]
		private bool m_RestartOnReset = true;
		
		[Tooltip("Process component results in a data processing chain")] [SerializeField]
		private bool m_ProcessData = false;
		
		[SerializeField]
		[Interface(typeof(IDataProcessor<SessionData>))]
		private Object[] m_SessionDataProcessors;

		public IDataProcessor<SessionData>[] SessionDataProcessors =>
			m_SessionDataProcessors.OfType<IDataProcessor<SessionData>>().ToArray();

		public bool AutoStart
		{
			get => m_AutoStart;
			set => m_AutoStart = value;
		}

		public bool BurstMode
		{
			get { return m_BurstMode; }
			set { SetBurstMode(value); }
		}

		public bool ProcessData
		{
			get => m_ProcessData;
			set => m_ProcessData = value;
		}

		public float SessionUpdateInterval
		{
			get => m_SessionUpdateInterval;
			set => m_SessionUpdateInterval = value;
		}

		public int BurstSuccessCount
		{
			get => m_BurstSuccessCount;
			set => m_BurstSuccessCount = value;
		}

		public float BurstTimeLimit
		{
			get => m_BurstTimeLimit;
			set => m_BurstTimeLimit = value;
		}

		public bool RestartOnReset
		{
			get => m_RestartOnReset;
			set => m_RestartOnReset = value;
		}

		private void SetBurstMode(bool on)
		{
			m_BurstStartTime = Time.unscaledTime;
			m_BurstModeActive = on;
		}

		#region Events

		public UnityEvent OnPause;
		public UnityEvent OnResume;
		public UnityEvent OnReset;

		#endregion

		private ImmersalSDK sdk;

		private float m_BurstStartTime = 0.0f;
		private bool m_BurstModeActive = false;

		private float m_LastUpdateTime;
		private bool m_SessionIsRunning = false;
		private bool m_RunningTasks = false;
		private bool m_Paused = false;

		private Task m_CurrentRunningTask;
		private CancellationTokenSource m_CTS;
		
		private IPlatformUpdateResult m_LatestPlatformUpdateResult = null;
		private ILocalizationResults m_LatestLocalizationResults = null;
		
		private DataProcessingChain<SessionData> m_SessionDataProcessingChain;

		private void Start()
		{
			sdk = ImmersalSDK.Instance;

			m_SessionDataProcessingChain = new DataProcessingChain<SessionData>(SessionDataProcessors);

			if (!m_AutoStart)
				return;
			
			if (sdk.IsReady)
			{
				StartSession();
			}
			else
			{
				sdk.OnInitializationComplete.AddListener(StartSession);
			}
		}

		public void StartSession()
		{
			if (m_SessionIsRunning)
			{
				if (m_Paused)
					ResumeSession(); // Resume if paused
				return;
			}
				
			if (!sdk.IsReady)
				return;
			
			ImmersalLogger.Log("Session starting", ImmersalLogger.LoggingLevel.Verbose);
			m_SessionIsRunning = true;
			SetBurstMode(BurstMode);
		}

		private async void Update()
		{
			// update data processing chain
			if (m_ProcessData)
				await m_SessionDataProcessingChain.UpdateChain();

			// bail out conditionals
			if (!m_SessionIsRunning || !sdk.IsReady || !MapManager.HasRegisteredMaps)
				return;

			float curTime = Time.unscaledTime;

			// deactivate burst after enough success or certain time
			if (sdk.TrackingStatus?.LocalizationSuccessCount >= m_BurstSuccessCount || curTime - m_BurstStartTime >= m_BurstTimeLimit)
			{
				SetBurstMode(false);
			}

			// check if update interval has passed or if we are bursting
			bool updateIntervalOrBurst = curTime - m_LastUpdateTime >= m_SessionUpdateInterval
			                             || m_BurstModeActive;

			// update conditional
			bool doUpdate = updateIntervalOrBurst
			                && !m_RunningTasks
			                && !m_Paused;

			if (doUpdate)
			{
				m_LastUpdateTime = curTime;
				m_RunningTasks = true;
				m_CTS = new CancellationTokenSource();
				m_CurrentRunningTask = RunTasksAsync(m_ProcessData, m_CTS.Token);
			}
		}

		private async Task RunTasksAsync(bool processData, CancellationToken cancellationToken)
		{
			try
			{
				// Update platform
				m_LatestPlatformUpdateResult = await sdk.PlatformSupport.UpdatePlatform();
				if (cancellationToken.IsCancellationRequested) { m_RunningTasks = false; return; }
				if (m_LatestPlatformUpdateResult.Success)
				{
					// Localize
					m_LatestLocalizationResults = await sdk.Localizer.Localize(m_LatestPlatformUpdateResult.CameraData);
					if (cancellationToken.IsCancellationRequested) { m_RunningTasks = false; return; }
					
					foreach (ILocalizationResult result in m_LatestLocalizationResults.Results)
					{
						if (result.Success)
						{
							// Check if map entry exists
							if (MapManager.TryGetMapEntry(result.MapId, out MapEntry entry))
							{
								ImmersalLogger.Log($"Localized to map {entry.Map.mapId}-{entry.Map.mapName}", ImmersalLogger.LoggingLevel.Verbose);
								ILocalizationResult r = result;
								IPlatformUpdateResult p = m_LatestPlatformUpdateResult;
								
								if (processData)
								{
									// Push new data to data processing chain
									await m_SessionDataProcessingChain.ProcessNewData(new SessionData()
									{
										Entry = entry,
										PlatformResult = p,
										LocalizationResult = r
									});

									SessionData processed = m_SessionDataProcessingChain.GetCurrentData();
									entry = processed.Entry;
									r = processed.LocalizationResult;
									p = processed.PlatformResult;
								}

								// Update SceneUpdater
								if (cancellationToken.IsCancellationRequested) { m_RunningTasks = false; return; }
								await sdk.SceneUpdater.UpdateScene(entry, p.CameraData, r);
							}
							else
							{
								ImmersalLogger.LogError("Localization result does not match with any registered map.");
							}
						}
					}
				}
				else
				{
					// Localization never ran due to failed platform update
					// so we need to invalidate previous results.
					m_LatestLocalizationResults = new LocalizationResults
					{
						Results = Array.Empty<ILocalizationResult>()
					};
				}

				// Update tracking status
				sdk.TrackingAnalyzer.Analyze(m_LatestPlatformUpdateResult.Status, m_LatestLocalizationResults);
			}
			catch (Exception e)
			{
				ImmersalLogger.LogError($"Session task error: {e.Message}. Stopping session.");
				ImmersalLogger.LogError($" {e.StackTrace} ");
				m_SessionIsRunning = false;
			}
			finally
			{
				m_RunningTasks = false;
			}
		}
		
		// Convenience method
		public async Task LocalizeOnce()
		{
			if (m_CurrentRunningTask != null)
				await m_CurrentRunningTask;
			
			m_LastUpdateTime = Time.unscaledTime;
			m_RunningTasks = true;
			m_CTS = new CancellationTokenSource();

			try
			{
				m_CurrentRunningTask = RunTasksAsync(m_ProcessData, m_CTS.Token);
			}
			catch (Exception e)
			{
				ImmersalLogger.LogError(e.Message);
				m_SessionIsRunning = false;
			}
		}

		// Note: MonoBehaviour.OnApplicationPause is called as a GameObject starts. The call is made after Awake.
		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				PauseSession();
			}
			else
			{
				ResumeSession();
			}
		}

		public async void PauseSession()
		{
			if (!m_SessionIsRunning)
				return;
			if (m_ResetOnPause)
				await ResetSession();
			m_Paused = true;
			OnPause?.Invoke();
		}

		public void ResumeSession()
		{
			if (!m_SessionIsRunning)
				return;
			m_Paused = false;
			SetBurstMode(BurstMode);
			OnResume?.Invoke();
		}

		public async void TriggerResetSession()
		{
			await ResetSession();
		}

		public async Task ResetSession()
		{
			ImmersalLogger.Log("Resetting session");
			await StopSession();
			await m_SessionDataProcessingChain.ResetProcessors();
			OnReset?.Invoke();
			if (m_RestartOnReset)
				StartSession();
		}

		public async Task StopSession(bool cancelRunningTask = true)
		{
			// Send cancellation token to abort running task
			if (cancelRunningTask)
				m_CTS?.Cancel();
			
			// Wait for running task to finish
			if (m_CurrentRunningTask != null)
				await m_CurrentRunningTask;
			
			m_SessionIsRunning = false;
			m_RunningTasks = false;
			m_LatestLocalizationResults = null;
			m_LatestPlatformUpdateResult = null;
			m_BurstModeActive = false;
			m_BurstStartTime = 0.0f;
			m_LastUpdateTime = 0f;
			m_Paused = false;
			
			ImmersalLogger.Log("Session stopped");
		}
	}
	
	public class SessionData
	{
		public MapEntry Entry;
		public IPlatformUpdateResult PlatformResult;
		public ILocalizationResult LocalizationResult;
	}
}
