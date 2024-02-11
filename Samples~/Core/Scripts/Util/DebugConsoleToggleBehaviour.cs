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
using UnityEngine.UI;

public class DebugConsoleToggleBehaviour : MonoBehaviour
{
    private Toggle m_Toggle;
    void Awake()
    {
        m_Toggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        m_Toggle.isOn = DebugConsole.Instance.isShown;
    }
}
