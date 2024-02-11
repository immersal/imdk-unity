/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Immersal.XR
{
    public class PoseSmoother : MonoBehaviour, IDataProcessor<Matrix4x4>
    {
        [SerializeField]
        private bool m_WarpOutsideThreshold = true;
        
        [SerializeField]
        private float m_WarpThresholdDistSq = 5.0f * 5.0f;

        [SerializeField]
        private float m_WarpThresholdCosAngle = Mathf.Cos(20.0f * Mathf.PI / 180.0f);

        private Vector3 targetPosition = Vector3.zero;
        private Quaternion targetRotation = Quaternion.identity;
        private Vector3 currentPosition = Vector3.zero;
        private Quaternion currentRotation = Quaternion.identity;

        void Update()
        {
            float distSq = (currentPosition - targetPosition).sqrMagnitude;
            float cosAngle = Quaternion.Dot(currentRotation, targetRotation);
            
            if (m_WarpOutsideThreshold && (distSq > m_WarpThresholdDistSq || cosAngle < m_WarpThresholdCosAngle))
            {
                currentPosition = targetPosition;
                currentRotation = targetRotation;
            }
            else
            {
                float smoothing = 0.025f;
                float steps = Time.deltaTime / (1.0f / 60.0f);
                if (steps < 1.0f)
                    steps = 1.0f;
                else if (steps > 6.0f)
                    steps = 6.0f;
                float alpha = 1.0f - Mathf.Pow(1.0f - smoothing, steps);

                currentPosition = Vector3.Lerp(currentPosition, targetPosition, alpha);
                currentRotation = Quaternion.Slerp(currentRotation, targetRotation, alpha);
            }
        }

        public Task<Matrix4x4> ProcessData(Matrix4x4 data, DataProcessorTrigger trigger)
        {
            if (trigger == DataProcessorTrigger.NewData && data.ValidTRS())
            {
                targetPosition = data.GetPosition();
                targetRotation = data.rotation;
            }
            return Task.FromResult(Matrix4x4.TRS(currentPosition, currentRotation, Vector3.one));
        }

        public Task ResetProcessor()
        {
            targetPosition = Vector3.zero;
            targetRotation = Quaternion.identity;
            currentPosition = Vector3.zero;
            currentRotation = Quaternion.identity;
            return Task.CompletedTask;
        }
    }
}