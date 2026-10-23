/*===============================================================================
Copyright (C) 2026 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using UnityEngine;

public class InterfaceAttribute : PropertyAttribute
{
    public Type TypeOfInterface;
    public InterfaceAttribute(Type type) { this.TypeOfInterface = type; }
}