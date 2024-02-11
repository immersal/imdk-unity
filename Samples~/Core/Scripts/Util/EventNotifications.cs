/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections;
using System.Collections.Generic;
using Immersal.Samples.Util;
using UnityEngine;

public class EventNotifications : MonoBehaviour
{
    public static void OnSDKInitialization()
    {
        NotificationManager.Instance.GenerateSuccess("SDK initialized");
    }
    
    public static void OnSDKReset()
    {
        NotificationManager.Instance.GenerateNotification("SDK reset");
    }
    
    public static void OnSessionPause()
    {
        NotificationManager.Instance.GenerateNotification("Session paused");
    }
    
    public static void OnSessionResume()
    {
        NotificationManager.Instance.GenerateNotification("Session resumed");
    }
    
    public static void OnSessionReset()
    {
        NotificationManager.Instance.GenerateNotification("Session reset");
    }
    
    public static void OnLocalizerFirstSuccess()
    {
        NotificationManager.Instance.GenerateSuccess("First successful localization");
    }
    
    public static void OnLocalizerSuccess()
    {
        NotificationManager.Instance.GenerateSuccess("Successful localization");
    }
    
    public static void OnLocalizerSuccess(int previousMapId, int newMapId)
    {
        NotificationManager.Instance.GenerateSuccess($"{previousMapId} > {newMapId}");
    }
    
    public static void OnLocalizerFail()
    {
        NotificationManager.Instance.GenerateWarning("Failed localization");
    }
    
    public static void OnPlatformTrackingLost()
    {
        NotificationManager.Instance.GenerateNotification("Platform tracking lost");
    }
    
    public static void OnTrackingWell()
    {
        NotificationManager.Instance.GenerateSuccess("Tracking well");
    }
    
    public static void OnTrackingLost()
    {
        NotificationManager.Instance.GenerateWarning("Tracking lost");
    }
    
    public static void OnMapChange()
    {
        NotificationManager.Instance.GenerateNotification("");
    }
}
