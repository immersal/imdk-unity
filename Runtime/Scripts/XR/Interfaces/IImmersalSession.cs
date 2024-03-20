/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public interface IImmersalSession
{
    void PauseSession();
    void ResumeSession();
    Task ResetSession();
    Task StopSession(bool cancelRunningTask = true);
    void StartSession();
    Task LocalizeOnce();
}
