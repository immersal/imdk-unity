/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Threading;
using System.Threading.Tasks;
using Immersal.REST;

namespace Immersal.XR
{
    public interface ILocalizationResult
    {
        bool Success { get; }
        int MapId { get; }
        LocalizeInfo LocalizeInfo { get; }
    }

    public interface ILocalizationMethodConfiguration
    {
        XRMap[] MapsToAdd { get; }
        XRMap[] MapsToRemove { get; }
    }

    public enum ConfigurationMode
    {
        WhenNecessary,
        Always
    }

    public interface ILocalizationMethod : IHasNullOrDeadCheck
    {
        ConfigurationMode ConfigurationMode { get; }
        IMapOption[] MapOptions { get; }
        Task<bool> Configure(ILocalizationMethodConfiguration configuration);
        Task<ILocalizationResult> Localize(ICameraData cameraData, CancellationToken cancellationToken);
        Task StopAndCleanUp();
        Task OnMapRegistered(XRMap map);
    }
    
    // Utility interface and extension method for checking if an interface is null.
    // Directly comparing an interface to null sidesteps the custom null-checks used with Unity objects.
    // This means the interface reference might not be null in the C# sense, even if the object it is
    // referencing has been destroyed in the Unity context.
    // This is only an issue if the class implementing the interface is inheriting from Unity objects.
    public interface IHasNullOrDeadCheck {}

    public static class NullOrDeadCheckExtension
    {
        public static bool IsNullOrDead(this IHasNullOrDeadCheck obj)
        {
            // Casting to UnityEngine.Object will force the check to utilize Unity's custom null-checking
            if (obj is UnityEngine.Object o)
                return o == null;
            return obj == null;
        }
    }
}