/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Immersal.XR
{
    public class MapChangeDetector : MonoBehaviour, IDataProcessor<SessionData>
    {
        public UnityEvent<int, int> OnMapChanged;
        public bool InvokeOnFirstLocalization = false;

        private int m_LastLocalizedMapId = -1;

        public Task<SessionData> ProcessData(SessionData data, DataProcessorTrigger trigger)
        {
            if (trigger == DataProcessorTrigger.NewData)
            {
                int mapId = data.Entry.Map.mapId;
                if (mapId != m_LastLocalizedMapId)
                {
                    if (m_LastLocalizedMapId == -1 && !InvokeOnFirstLocalization)
                    {
                        m_LastLocalizedMapId = mapId;
                        return Task.FromResult(data);
                    }
                    OnMapChanged?.Invoke(m_LastLocalizedMapId, mapId);
                    m_LastLocalizedMapId = mapId;
                }
            }
            return Task.FromResult(data);
        }

        public Task ResetProcessor()
        {
            m_LastLocalizedMapId = -1;
            return Task.CompletedTask;
        }
    }
}
