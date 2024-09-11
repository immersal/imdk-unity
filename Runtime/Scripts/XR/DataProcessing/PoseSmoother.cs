/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Immersal.XR
{
    public enum SmoothingMode
    {
        SlowApproach,
        Linear,
        Sinusoidal,
        Cubic
    }
    
    public class NewPoseSmoother : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
        [Header("Smoothing")]
        [SerializeField, Tooltip("Classic slow approach method or linear, sinusoidal or cubic timing function.")]
        private SmoothingMode m_Mode = SmoothingMode.SlowApproach;
        
        [SerializeField, Tooltip("Smoothing factor for the slow approach mode.")]
        private float m_SlowApproachSmoothing = 0.025f;
        
        [SerializeField, Tooltip("Interpolation time for linear, sinusoidal and cubic modes.")]
        private float m_SmoothTimeSpan = 0.5f;
        
        [Header("Warping")]
        [SerializeField, Tooltip("Enable to warp to target instantly when distance or angle gets too large.")]
        private bool m_WarpOutsideThreshold = true;
        
        [SerializeField, Tooltip("Warp if distance is larger than this.")]
        private float m_WarpThresholdDist = 5.0f;

        [SerializeField, Tooltip("Warp if angle is larger than this.")]
        private float m_WarpThresholdAngle = 20.0f;

        private Vector3 targetPosition = Vector3.zero;
        private Quaternion targetRotation = Quaternion.identity;
        private Vector3 startPosition = Vector3.zero;
        private Quaternion startRotation = Quaternion.identity;
        private Vector3 currentPosition = Vector3.zero;
        private Quaternion currentRotation = Quaternion.identity;

        private float m_WarpThresholdDistSq;
        private float m_WarpThresholdCosAngle;
        private float elapsedTime = 0f;
        private bool m_HasUpdated = false;

        private void Awake()
        {
            m_WarpThresholdDistSq = m_WarpThresholdDist * m_WarpThresholdDist;
            m_WarpThresholdCosAngle = Mathf.Cos(m_WarpThresholdAngle * Mathf.PI / 180f);
        }

        private void Update()
        {
            UpdatePose();
        }

        private void UpdatePose()
        {
            float distSq = (currentPosition - targetPosition).sqrMagnitude;
            float cosAngle = Quaternion.Dot(currentRotation, targetRotation);

            bool warpCondition = m_WarpOutsideThreshold &&
                                 (distSq > m_WarpThresholdDistSq || cosAngle < m_WarpThresholdCosAngle);
            
            if (!m_HasUpdated || warpCondition)
            {
                currentPosition = targetPosition;
                currentRotation = targetRotation;
            }
            else
            {
                float alpha = 0f;
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / m_SmoothTimeSpan);
                
                switch (m_Mode)
                {
                    case SmoothingMode.SlowApproach:
                        float s = Time.deltaTime / (1.0f / 60.0f);
                        float steps = Mathf.Min(Mathf.Max(s, 1f), 6f);
                        alpha = 1.0f - Mathf.Pow(1.0f - m_SlowApproachSmoothing, steps);
                        startPosition = currentPosition;
                        startRotation = currentRotation;
                        break;
                    case SmoothingMode.Linear:
                        alpha = t;
                        break;
                    case SmoothingMode.Sinusoidal:
                        alpha = Mathf.Sin(t * Mathf.PI * 0.5f);
                        break;
                    case SmoothingMode.Cubic:
                        alpha = t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                currentPosition = Vector3.Lerp(startPosition, targetPosition, alpha);
                currentRotation = Quaternion.Slerp(startRotation, targetRotation, alpha);
            }

            m_HasUpdated = true;
        }

        public Task<SceneUpdateData> ProcessData(SceneUpdateData data, DataProcessorTrigger trigger)
        {
            if (trigger == DataProcessorTrigger.NewData)
            {
                startPosition = currentPosition;
                startRotation = currentRotation;
                targetPosition = data.Pose.GetPosition();
                targetRotation = data.Pose.rotation;
                elapsedTime = 0f;
            }
            UpdatePose();
            data.Pose = Matrix4x4.TRS(currentPosition, currentRotation, Vector3.one);
            return Task.FromResult(data);
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