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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
    public enum SolverType
    {
        Default = 0,
        Lean = 1,
        Prior = 2
    }
	
	public struct LocalizationResults : ILocalizationResults
	{
		public ILocalizationResult[] Results { get; set; }
	}

    public struct LocalizationResult : ILocalizationResult
    {
	    public bool Success { get; set; }
	    public int MapId { get; set; }
	    public LocalizeInfo LocalizeInfo { get; set; }
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

        private List<ILocalizationMethod> m_ConfiguredLocalizationMethods = new List<ILocalizationMethod>();

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
        
        public UnityEvent<ILocalizationResults> OnLocalizationResult;
	    
        // Invoked once per Localize call if all localization attempts failed
        public UnityEvent OnFailedLocalizations;
        
        // Other
        private bool m_IsLocalizing = false;
        private bool m_HasLocalizedSuccessfully = false;
 
        // Configuration 
        // Note: this can get called again after the initial configuration if new LocalizationMethods need to be added
        public async Task<ILocalizerConfigurationResult> ConfigureLocalizer(ILocalizerConfiguration configuration)
        {
	        ImmersalLogger.Log("Configuring Localizer");
	        
	        // Check if tasks are running and stop if requested
	        if (configuration.StopRunningTasks && m_RunningLocalizationTasks is { Count: > 0 })
		        await StopRunningLocalizationTasks();
	        
	        // Default to failure
	        ILocalizerConfigurationResult r = new LocalizerConfigurationResult { Success = false };
	        
	        List<ILocalizationMethod> configuredMethods = new List<ILocalizationMethod>();
	        
	        // Add new configurations if requested
	        if (configuration.ConfigurationsToAdd != null)
	        {
		        foreach (ILocalizationMethod localizationMethod in AvailableLocalizationMethods)
		        {
			        bool isNecessary = configuration.ConfigurationsToAdd.TryGetValue(localizationMethod, out XRMap[] maps);

			        bool configureMethod = localizationMethod.ConfigurationMode switch
			        {
				        ConfigurationMode.WhenNecessary => isNecessary,
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
	        }
	        
	        // Refresh our localization method cache to only include configured methods
	        m_ConfiguredLocalizationMethods.AddRange(configuredMethods);
	        
	        // Remove configurations if requested
	        if (configuration.ConfigurationsToRemove != null)
	        {
		        foreach (KeyValuePair<ILocalizationMethod,XRMap[]> keyValuePair in configuration.ConfigurationsToRemove)
		        {
			        await RemoveLocalizationMethodConfiguration(keyValuePair.Key, keyValuePair.Value);
		        }
	        }
	        
	        // Initialize running tasks Dictionary
	        if (m_RunningLocalizationTasks == null)
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

	        DefaultLocalizationMethodConfiguration config = new DefaultLocalizationMethodConfiguration
	        {
		        MapsToAdd = maps
	        };

	        if (await localizationMethod.Configure(config))
	        {
		        return true;
	        }
	        
	        ImmersalLogger.LogError($"Could not configure localization method: {localizationMethod.GetType().Name}.");
	        return false;
        }

        private async Task<bool> RemoveLocalizationMethodConfiguration(ILocalizationMethod localizationMethod, XRMap[] maps)
        {
	        // Check if configured
	        if (!m_ConfiguredLocalizationMethods.Contains(localizationMethod))
	        {
		        ImmersalLogger.LogError("Trying to remove configurations from a non-configured localization method.");
		        return false;
	        }
	        
	        DefaultLocalizationMethodConfiguration config = new DefaultLocalizationMethodConfiguration
	        {
		        MapsToRemove = maps
	        };
	        
	        ImmersalLogger.Log($"Removing {maps.Length} maps from {localizationMethod.GetType().Name} configuration");

	        // Configure will return false if the method does not have any maps configured after removal
	        // => should remove the configuration entirely if set to WhenNecessary
	        if (!await localizationMethod.Configure(config) && localizationMethod.ConfigurationMode == ConfigurationMode.WhenNecessary)
	        {
		        ImmersalLogger.Log($"Removing {localizationMethod.GetType().Name} configuration");
		        
		        // Cancel possible running task
		        if (m_RunningLocalizationTasks.TryGetValue(localizationMethod, out LocalizationTask task))
		        {
			        task.CancellationTokenSource.Cancel();
			        await task.LocalizationMethodTask;
		        }

		        m_ConfiguredLocalizationMethods.Remove(localizationMethod);
	        }

	        return true;
        }
        
        // This ILocalizer implementation can run multiple asynchronous localization tasks (one per method)
        // The Localize task itself always awaits for one of the internal localization tasks to finish
        // before providing results. Remaining localization tasks will propagate to the next cycle.
        public async Task<ILocalizationResults> Localize(ICameraData cameraData)
        {
	        // Localization is already running -> bail out
	        if (m_IsLocalizing)
	        {
		        cameraData.CheckReferences();
		        return new LocalizationResults { Results = Array.Empty<ILocalizationResult>() };
	        }
		        
		    m_IsLocalizing = true;
	        List<ILocalizationResult> results = new List<ILocalizationResult>();

	        // Make sure all LocalizationTasks are running
	        foreach (ILocalizationMethod localizationMethod in m_ConfiguredLocalizationMethods)
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
	        OnLocalizationResult?.Invoke(localizationResults);
	        return localizationResults;
        }
        
        public async Task<List<LocalizationTask>> CreateLocalizationTasks(ICameraData cameraData)
        {
	        List<LocalizationTask> tasks = new List<LocalizationTask>();
	        
	        foreach (ILocalizationMethod localizationMethod in m_ConfiguredLocalizationMethods)
	        {
		        // Create new localization task
		        tasks.Add(CreateNewLocalizationTask(localizationMethod, cameraData));
	        }

	        return tasks;
        }
        
        public async Task<ILocalizationResults> LocalizeAllMethods(ICameraData cameraData)
        {
	        List<ILocalizationResult> results = new List<ILocalizationResult>();
	        List<LocalizationTask> tasks = await CreateLocalizationTasks(cameraData);
	        await Task.WhenAll(tasks.Select(t => t.LocalizationMethodTask));
	        foreach (Task<ILocalizationResult> t in tasks.Select(t => t.LocalizationMethodTask))
	        {
		        if (t.Status != TaskStatus.RanToCompletion) continue;
		        results.Add(t.Result);
		        t.Dispose();
	        }
	        return new LocalizationResults { Results = results.ToArray() };
        }

        private void StartNewLocalizationTask(ILocalizationMethod localizationMethod, ICameraData cameraData)
        {
	        CancellationTokenSource cts = new CancellationTokenSource();
	        Task<ILocalizationResult> localizationMethodTask = localizationMethod.Localize(cameraData, cts.Token);
	        LocalizationTask task = new LocalizationTask(localizationMethodTask, cts);
	        m_RunningLocalizationTasks.Add(localizationMethod, task);
        }

        private LocalizationTask CreateNewLocalizationTask(ILocalizationMethod localizationMethod, ICameraData cameraData)
        {
	        CancellationTokenSource cts = new CancellationTokenSource();
	        Task<ILocalizationResult> localizationMethodTask = localizationMethod.Localize(cameraData, cts.Token);
	        LocalizationTask task = new LocalizationTask(localizationMethodTask, cts);
	        return task;
        }

        private ILocalizationResult[] CollectLocalizationResults()
        {
	        // Combine all currently finished results
	        List<ILocalizationResult> resultList = new List<ILocalizationResult>();
	        
	        foreach (ILocalizationMethod localizationMethod in m_ConfiguredLocalizationMethods)
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
	        foreach (ILocalizationMethod localizationMethod in m_ConfiguredLocalizationMethods)
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
	        if (m_RunningLocalizationTasks is not { Count: > 0 }) return;
	        
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

        public async Task StopLocalizationForMethod(ILocalizationMethod localizationMethod)
        {
	        if (m_RunningLocalizationTasks is not { Count: > 0 }) return;
	        
	        if (m_RunningLocalizationTasks.TryGetValue(localizationMethod, out LocalizationTask task))
	        {
		        task.CancellationTokenSource.Cancel();
		        await task.LocalizationMethodTask;
		        m_RunningLocalizationTasks.Remove(localizationMethod);
	        }
        }

        public bool TryGetLocalizationTask(ILocalizationMethod localizationMethod, out LocalizationTask task)
        {
	        return m_RunningLocalizationTasks.TryGetValue(localizationMethod, out task);
        }

         public async Task StopAndCleanUp()
         {
	         // Cancel all running tasks
	         await StopRunningLocalizationTasks();
	         
	         // Clean up methods
	         await Task.WhenAll(m_ConfiguredLocalizationMethods.Select(method => method.StopAndCleanUp()));
	         m_ConfiguredLocalizationMethods.Clear();
	         
	         m_HasLocalizedSuccessfully = false;
         }
    }

    public struct LocalizerConfigurationResult : ILocalizerConfigurationResult
    {
	    public bool Success { get; set; }
    }

    public struct DefaultLocalizerConfiguration : ILocalizerConfiguration
    {
	    public Dictionary<ILocalizationMethod, XRMap[]> ConfigurationsToAdd { get; set; }
	    public Dictionary<ILocalizationMethod, XRMap[]> ConfigurationsToRemove { get; set; }
	    public bool StopRunningTasks { get; set; }
    }

    public struct DefaultLocalizationMethodConfiguration : ILocalizationMethodConfiguration
    {
	    public XRMap[] MapsToAdd { get; set; }
	    public XRMap[] MapsToRemove { get; set; }
	    public SolverType? SolverType { get; set; }
	    [ObsoleteAttribute("PriorNNCount is obsolete. Use PriorNNCountMin/Max instead.", false)]
	    public int? PriorNNCount { get; set; }
	    public int? PriorNNCountMin { get; set; }
	    public int? PriorNNCountMax { get; set; }
	    public Vector3? PriorScale { get; set; }
	    public float? PriorRadius { get; set; }
    }
}