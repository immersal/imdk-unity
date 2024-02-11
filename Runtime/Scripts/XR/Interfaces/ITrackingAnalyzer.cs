/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

namespace Immersal.XR
{
    public interface ITrackingStatus
    {
        int LocalizationAttemptCount { get; }
        int LocalizationSuccessCount { get; }
        int TrackingQuality { get; }
    }
    
    public interface ITrackingAnalyzer
    {
        ITrackingStatus TrackingStatus { get; }
        void Analyze(IPlatformStatus platformStatus, ILocalizationResults localizationResults);
        void Reset();
    }
}