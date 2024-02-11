/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Immersal.XR;
using Immersal.REST;

namespace Immersal.Samples
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class ResolutionListController : MonoBehaviour
    {
        private TMP_Dropdown m_Dropdown;
        private ARFoundationSupport m_ARFSupport;

        void Awake()
        {
            m_ARFSupport = (ARFoundationSupport)ImmersalSDK.Instance.PlatformSupport;
            
            if (m_ARFSupport == null)
            {
                Debug.LogError("Could not locate ARFoundationSupport");
                enabled = false;
                return;
            }
            
            m_Dropdown = GetComponent<TMP_Dropdown>();
            m_Dropdown.ClearOptions();
        }

        void Start()
        {
            List<string> modes = new List<string>();

            foreach (ARFoundationSupport.CameraResolution reso in Enum.GetValues(typeof(ARFoundationSupport.CameraResolution)))
            {
                modes.Add(reso.ToString());
            }

            m_Dropdown.AddOptions(modes);
        }

        public void OnValueChanged(TMP_Dropdown dropdown)
        {
            var values = Enum.GetValues(typeof(ARFoundationSupport.CameraResolution));
            ARFoundationSupport.CameraResolution camReso = (ARFoundationSupport.CameraResolution)values.GetValue((long)dropdown.value);
        #if UNITY_ANDROID
            m_ARFSupport.androidResolution = camReso;
        #elif UNITY_IOS
            m_ARFSupport.iOSResolution = camReso;
        #endif
        }
    }
}
