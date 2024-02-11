/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using UnityEngine;
using Immersal.XR;

namespace Immersal.Samples
{
    public class LocalizerSettingsPanel : MonoBehaviour
    {
        private XRSpace m_Space;
        
        private void Awake()
        {
            m_Space = FindFirstObjectByType<XRSpace>();
        }

        public void Downsample(bool value)
        {
            ImmersalSDK.Instance.downsample = value;
        }
        
        public void UseProcessing(bool value)
        {
            if (m_Space == null)
                return;
            
            m_Space.ProcessPoses = value;
        }

        public void Pause()
        {
            ImmersalSDK.Instance.Session.PauseSession();
        }

        public void Resume()
        {
            ImmersalSDK.Instance.Session.ResumeSession();
        }

        public void StopLocalizing()
        {
            ImmersalSDK.Instance.Session.StopSession();
        }
        public void StartLocalizing()
        {
            ImmersalSDK.Instance.Session.StartSession();
        }
        public void Localize()
        {
            ImmersalSDK.Instance.Session.LocalizeOnce();
        }

        public void ClosePanel()
        {
            Destroy(this.gameObject);
        }
    }
}
