/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Immersal.XR
{
    public class TrackingAnalyzer : MonoBehaviour, ITrackingAnalyzer
    {
        [SerializeField]
        private float m_SecondsToDecayPose = 10f;

        // LocalizationResults might contain multiple fails / success results.
        // By default these are combined to one attempt and one fail/success.
        // This options allows counting each result separately.
        [SerializeField]
        private bool m_SeparateLocalizationResults = false;

        // This is invoked the first time we notice platform tracking quality drops to 0.
        // After platform tracking quality goes up again, we enable a new invoke on the next quality drop.
        // Platform tracking must go above 0 for this to ever be invoked.
        public UnityEvent OnPlatformTrackingLost;
        
        // These actions are invoked when tracking quality is high enough or drops to 0.
        // Only invoked once when the change happens. Invoking one resets the other.
        // OnTrackingLost will not be invoked before TrackingWell has occured.
        // The bool value is true on the first ever invocation, otherwise false.
        public UnityEvent<bool> OnTrackingWell;
        public UnityEvent<bool> OnTrackingLost;
        
        public ITrackingStatus TrackingStatus => m_CurrentTrackingStatus;

        private TrackingStatus m_CurrentTrackingStatus = new TrackingStatus();
        private bool m_HasPose = false;
        private int m_CurrentResult = 0;
        private int m_PreviousResult = 0;
        private float m_LatestPoseUpdateTime = 0f;
        
        private bool m_AllowPlatformTrackingLostInvoke = false;
        private bool m_TrackingWell = false;
        private bool m_NeverLostTracking = true;
        private bool m_NeverTrackedWell = true;
        
        // Analyze is run after other tasks have been completed / attempted.
        // Frequency depends on ImmersalSDK session update interval.
        public void Analyze(IPlatformStatus platformStatus, ILocalizationResults localizationResults)
        {
            if (platformStatus.TrackingQuality == 0 && m_AllowPlatformTrackingLostInvoke)
            {
                OnPlatformTrackingLost?.Invoke();
                m_AllowPlatformTrackingLostInvoke = false;
            }
            else if (platformStatus.TrackingQuality > 0 && !m_AllowPlatformTrackingLostInvoke)
            {
                m_AllowPlatformTrackingLostInvoke = true;
            }
            
            if (!m_SeparateLocalizationResults)
                m_CurrentTrackingStatus.LocalizationAttemptCount++;

            bool hadSuccess = false;
            foreach (ILocalizationResult result in localizationResults.Results)
            {
                if (m_SeparateLocalizationResults)
                    m_CurrentTrackingStatus.LocalizationAttemptCount++;
                
                if (result.Success)
                {
                    hadSuccess = true;
                    
                    if (m_SeparateLocalizationResults)
                        m_CurrentTrackingStatus.LocalizationSuccessCount++;
                    
                    if (!m_HasPose)
                    {
                        m_HasPose = true;
                    }
                }
            }
            
            if (!m_SeparateLocalizationResults && hadSuccess)
                m_CurrentTrackingStatus.LocalizationSuccessCount++;
        }

        public void Reset()
        {
            ImmersalLogger.Log("Resetting TrackingAnalyzer");
            m_CurrentTrackingStatus = new TrackingStatus();
            m_HasPose = false;
            m_CurrentResult = 0;
            m_PreviousResult = 0;
            m_LatestPoseUpdateTime = 0f;
            m_TrackingWell = false;
            m_NeverLostTracking = true;
            m_NeverTrackedWell = true;
        }

        private void Update()
        {
            if (m_CurrentTrackingStatus.LocalizationAttemptCount == 0) return;
            
            // Update cumulative result tracking
            
            int diffResults = m_CurrentTrackingStatus.LocalizationSuccessCount - m_PreviousResult;
            m_PreviousResult = m_CurrentTrackingStatus.LocalizationSuccessCount;

            if (diffResults > 0)
            {
                m_LatestPoseUpdateTime = Time.time;
                m_CurrentResult = Mathf.Min(m_CurrentResult + diffResults, 3);
            }
            else if (Time.time - m_LatestPoseUpdateTime > m_SecondsToDecayPose)
            {
                m_LatestPoseUpdateTime = Time.time;
                m_CurrentResult = Mathf.Max(m_CurrentResult - 1, 0);
            }
            
            // Tracking lost
            if (m_HasPose && m_CurrentResult < 1)
            {
                m_HasPose = false;
                if (m_TrackingWell)
                {
                    OnTrackingLost?.Invoke(m_NeverLostTracking);
                    m_NeverLostTracking = false;
                    m_TrackingWell = false;
                }
            }
            // Tracking well
            else if (m_HasPose && diffResults > 0 && m_CurrentResult > 2 && !m_TrackingWell)
            {
                OnTrackingWell?.Invoke(m_NeverTrackedWell);
                m_NeverTrackedWell = false;
                m_TrackingWell = true;
            }

            m_CurrentTrackingStatus.TrackingQuality = m_CurrentResult;
        }
    }
    
    public class TrackingStatus : ITrackingStatus
    {
        public int LocalizationAttemptCount { get; set; }
        public int LocalizationSuccessCount { get; set; }
        public int TrackingQuality { get; set; }
    }
}
