/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Immersal.REST;

namespace Immersal.XR
{
    public interface ILocalizationResults
    {
        ILocalizationResult[] Results { get; }
    }

    public interface ILocalizerConfiguration
    {
        Dictionary<ILocalizationMethod, XRMap[]> ConfigurationsToAdd { get; }
        Dictionary<ILocalizationMethod, XRMap[]> ConfigurationsToRemove { get; }
        bool StopRunningTasks { get; }
    }

    public interface ILocalizerConfigurationResult
    {
        bool Success { get; set; }
    }

    public interface ILocalizer
    {
        ILocalizationMethod[] AvailableLocalizationMethods { get;  }
        Task<ILocalizerConfigurationResult> ConfigureLocalizer(ILocalizerConfiguration configuration);
        Task<ILocalizationResults> Localize(ICameraData cameraData);
        Task<List<LocalizationTask>> CreateLocalizationTasks(ICameraData cameraData);
        Task<ILocalizationResults> LocalizeAllMethods(ICameraData cameraData);
        Task StopAndCleanUp();
        Task StopLocalizationForMethod(ILocalizationMethod localizationMethod);
        bool TryGetLocalizationTask(ILocalizationMethod localizationMethod, out LocalizationTask task);
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
}