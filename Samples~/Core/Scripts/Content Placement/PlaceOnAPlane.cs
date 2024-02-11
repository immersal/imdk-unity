/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Immersal.XR;
using UnityEngine.Serialization;

namespace Immersal.Samples.ContentPlacement
{
    public class PlaceOnAPlane : MonoBehaviour
    {
        [SerializeField]
        private List<GameObject> m_objects = new List<GameObject>();
        [FormerlySerializedAs("m_ARSpace")] [SerializeField]
        private XRSpace m_XRSpace = null;

        public void Place(int index)
        {
            Transform cam = Camera.main?.transform;
            m_XRSpace = FindObjectOfType<XRSpace>();

            if (cam != null && m_objects[index] != null && m_XRSpace != null)
            {
                RaycastHit hit;
                Vector3 direction = cam.forward;
                Vector3 origin = cam.position;

                if (Physics.Raycast(origin, direction, out hit, Mathf.Infinity))
                {
                    if (hit.collider != null)
                    {
                        ARPlane arPlane = hit.collider.GetComponentInParent<ARPlane>();
                        if (arPlane)
                        {
                            Vector3 pos = hit.point;
                            GameObject go = Instantiate(m_objects[index], m_XRSpace.transform);
                            go.transform.localRotation = Quaternion.identity;
                            go.transform.position = pos;
                        }
                    }
                }
            }
        }
    }
}
