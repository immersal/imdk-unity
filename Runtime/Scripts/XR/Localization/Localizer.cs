/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Immersal.REST;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
	public struct LocalizationResults : ILocalizationResults
    {
	    public ILocalizationResult[] Results { get; set;  }
    }

    public struct LocalizationResult : ILocalizationResult
    {
	    public bool Success { get; set; }
	    public int MapId { get; set; }
	    public LocalizeInfo LocalizeInfo { get; set; }
    }

    public class LocalizationTask
    {
	    public Task<ILocalizationResult> LocalizationMethodTask { get; private set; }
	    public CancellationTokenSource CancellationTokenSource { get; private set; }

	    public LocalizationTask(Task<ILocalizationResult> task, CancellationTokenSource cancellationTokenSource)
	    {
		    LocalizationMethodTask = task;
		    CancellationTokenSource = cancellationTokenSource;
	    }
    }
    
    public class Localizer : MonoBehaviour, ILocalizer 
    {
	    // Localization Methods
	    // Serialized as Objects like the components in ImmersalSDK
        [SerializeField, Interface(typeof(ILocalizationMethod))]
        private Object[] m_LocalizationMethodObjects;
        private ILocalizationMethod[] m_CachedLocalizationMethods; // cache deserialized methods

        public ILocalizationMethod[] AvailableLocalizationMethods
        {
	        get
	        {
		        // Return if already cached
		        if (m_CachedLocalizationMethods != null) return m_CachedLocalizationMethods;
		        if (m_LocalizationMethodObjects == null) return Array.Empty<ILocalizationMethod>();
		        // Deserialize and cache the localization methods
		        m_CachedLocalizationMethods = m_LocalizationMethodObjects.OfType<ILocalizationMethod>().ToArray();
		        return m_CachedLocalizationMethods;
	        }
        }

        // Keep references to running LocalizationTasks in a dictionary
        // Each type of ILocalizationMethod has it's own LocalizationTask
        private Dictionary<ILocalizationMethod, LocalizationTask> m_RunningLocalizationTasks;

        // Events
	    [Header("Events"), Space]
	    
        // Invoked when localization is successful for the first time (since start/reset)
        public UnityEvent OnFirstSuccessfulLocalization;
	    
        // Invoked once per Localize call if any localization was successful
        // int[] includes mapIds of successful localizations
        public UnityEvent<int[]> OnSuccessfulLocalizations;
	    
        // Invoked once per Localize call if all localization attempts failed
        public UnityEvent OnFailedLocalizations;
        
        // Other
        private bool m_IsLocalizing = false;
        private bool m_HasLocalizedSuccessfully = false;
 
        // Configuration
        public async Task<ILocalizerConfigurationResult> ConfigureLocalizer(ILocalizerConfiguration configuration)
        {
	        ImmersalLogger.Log("Configuring Localizer");
	        
	        // Check if tasks are running (if being reconfigured after initial config) and stop
	        if (m_RunningLocalizationTasks is { Count: > 0 })
		        await StopRunningLocalizationTasks();
	        
	        // Default to failure
	        ILocalizerConfigurationResult r = new LocalizerConfigurationResult { Success = false };
	        
	        // Configure localization methods
	        List<ILocalizationMethod> configuredMethods = new List<ILocalizationMethod>();
	        foreach (ILocalizationMethod localizationMethod in AvailableLocalizationMethods)
	        {
		        XRMap[] maps = Array.Empty<XRMap>();

		        bool configureMethod = localizationMethod.ConfigurationMode switch
		        {
			        ConfigurationMode.WhenNecessary => configuration.LocalizationMethodXRMapMapping.TryGetValue(
				        localizationMethod, out maps),
			        ConfigurationMode.Always => true,
			        _ => false
		        };

		        if (configureMethod)
		        {
			        // Try to configure method with associated mapIds
			        if (!await ConfigureLocalizationMethod(localizationMethod, maps))
			        {
				        // Configure failed, bail out
				        return r;
			        }
			        configuredMethods.Add(localizationMethod);
		        }
	        }
	        
	        // Refresh our localization method cache to only include configured methods
	        m_CachedLocalizationMethods = configuredMethods.ToArray();
	        
	        // Initialize running tasks Dictionary
	        m_RunningLocalizationTasks = new Dictionary<ILocalizationMethod, LocalizationTask>();

	        r.Success = true;
	        return r;
        }

        private async Task<bool> ConfigureLocalizationMethod(ILocalizationMethod localizationMethod, XRMap[] maps)
        {
	        ImmersalLogger.Log($"Configuring localization method: {localizationMethod.GetType().Name}");
	        
	        // Ensure we have the requested localization method available
	        if (!AvailableLocalizationMethods.Contains(localizationMethod))
	        {
		        ImmersalLogger.LogError("Trying to configure unavailable localization method.");
		        return false;
	        }

	        if (await localizationMethod.Configure(maps))
	        {
		        return true;
	        }
	        
	        ImmersalLogger.LogError($"Could not configure localization method: {localizationMethod.GetType().Name}.");
	        return false;
        }
        
        // This ILocalizer implementation can run multiple asynchronous localization tasks (one per method)
        // The Localize task itself always awaits for one of the internal localization tasks to finish
        // before providing results. Remaining localization tasks will propagate to the next cycle.
        public async Task<ILocalizationResults> Localize(ICameraData cameraData)
        {
	        // Localization is already running -> bail out
	        if (m_IsLocalizing)
		        return new LocalizationResults { Results = Array.Empty<ILocalizationResult>() };

		    m_IsLocalizing = true;
	        List<ILocalizationResult> results = new List<ILocalizationResult>();

	        // Make sure all LocalizationTasks are running
	        foreach (ILocalizationMethod localizationMethod in AvailableLocalizationMethods)
	        {
		        // If a task is already running, we check if has completed since last localization cycle
		        if (m_RunningLocalizationTasks.TryGetValue(localizationMethod, out LocalizationTask task))
		        {
			        if (task.LocalizationMethodTask.IsCompleted)
			        {
				        // Add results and remove so we can start again
				        results.Add(task.LocalizationMethodTask.Result);
				        m_RunningLocalizationTasks.Remove(localizationMethod);
				        task.LocalizationMethodTask.Dispose();
			        }
			        else
			        {
				        // Skip unfinished tasks
				        continue;
			        }
		        }
		        
		        // Start new localization task
		        StartNewLocalizationTask(localizationMethod, cameraData);
	        }
	        
	        // Wait for any of the currently running localization tasks to finish
	        Task anyTask = Task.WhenAny(
			        m_RunningLocalizationTasks.Values.Select(runningTask => runningTask.LocalizationMethodTask));
	        
	        try
	        {
		        await anyTask;
	        }
	        catch (OperationCanceledException)
	        {
		        CleanUpLocalizationTasks();
		        return new LocalizationResults { Results = Array.Empty<ILocalizationResult>() };
	        }
	        
	        ImmersalLogger.Log("Localization task completed");

	        // Collect results for this cycle and combine with previous
	        results.AddRange(CollectLocalizationResults());
	        
	        CheckForEvents(results);
	        
	        LocalizationResults localizationResults = new LocalizationResults
	        {
		        Results = results.ToArray()
	        };

	        m_IsLocalizing = false;
	        return localizationResults;
        }

        private void StartNewLocalizationTask(ILocalizationMethod localizationMethod, ICameraData cameraData)
        {
	        ImmersalLogger.Log($"Starting new localization task: {localizationMethod.GetType().Name}");
	        CancellationTokenSource cts = new CancellationTokenSource();
	        Task<ILocalizationResult> localizationMethodTask = localizationMethod.Localize(cameraData, cts.Token);
	        LocalizationTask task = new LocalizationTask(localizationMethodTask, cts);
	        m_RunningLocalizationTasks.Add(localizationMethod, task);
        }

        private ILocalizationResult[] CollectLocalizationResults()
        {
	        // Combine all currently finished results
	        List<ILocalizationResult> resultList = new List<ILocalizationResult>();
	        
	        foreach (ILocalizationMethod localizationMethod in AvailableLocalizationMethods)
	        {
		        if (m_RunningLocalizationTasks.TryGetValue(localizationMethod, out LocalizationTask task))
		        {
			        if (task.LocalizationMethodTask.IsCompleted)
			        {
				        resultList.Add(task.LocalizationMethodTask.Result);
				        m_RunningLocalizationTasks.Remove(localizationMethod);
				        task.LocalizationMethodTask.Dispose();
			        }
		        }
	        }

	        return resultList.ToArray();
        }

        private void CleanUpLocalizationTasks()
        {
	        // remove cancelled tasks
	        foreach (ILocalizationMethod localizationMethod in AvailableLocalizationMethods)
	        {
		        if (m_RunningLocalizationTasks.TryGetValue(localizationMethod, out LocalizationTask task))
		        {
			        if (task.LocalizationMethodTask.IsCanceled)
			        {
				        m_RunningLocalizationTasks.Remove(localizationMethod);
			        }
		        }
	        }
        }

        // Event firing logic
        private void CheckForEvents(List<ILocalizationResult> results)
        {
	        int[] ids = results.Where(r => r.Success).Select(r => r.MapId).ToArray();
	        if (ids.Length > 0)
	        {
		        if (!m_HasLocalizedSuccessfully)
		        {
			        OnFirstSuccessfulLocalization?.Invoke();
		        }
		        OnSuccessfulLocalizations?.Invoke(ids);
		        m_HasLocalizedSuccessfully = true;
	        }
	        else
	        {
		        OnFailedLocalizations?.Invoke();
	        }
        }

        private async Task StopRunningLocalizationTasks()
        {
	        List<Task<ILocalizationResult>> tasks = new List<Task<ILocalizationResult>>();
	         
	        foreach (LocalizationTask localizationTask in m_RunningLocalizationTasks.Values)
	        {
		        localizationTask.CancellationTokenSource.Cancel();
		        tasks.Add(localizationTask.LocalizationMethodTask);
	        }

	        // Wait for task to finish
	        await Task.WhenAll(tasks);
	         
	        // Clean up tasks
	        m_RunningLocalizationTasks.Clear();
        }

         public async Task StopAndCleanUp()
         {
	         // Cancel all running tasks
	         await StopRunningLocalizationTasks();
	         
	         // Clean up methods
	         await Task.WhenAll(AvailableLocalizationMethods.Select(method => method.StopAndCleanUp()));
	         //m_LocalizationMethods.Clear();

	         m_HasLocalizedSuccessfully = false;
         }
    }

    public struct LocalizerConfigurationResult : ILocalizerConfigurationResult
    {
	    public bool Success { get; set; }
    }

    public struct DefaultLocalizerConfiguration : ILocalizerConfiguration
    {
	    public Dictionary<ILocalizationMethod, XRMap[]> LocalizationMethodXRMapMapping { get; set; }
    }
}