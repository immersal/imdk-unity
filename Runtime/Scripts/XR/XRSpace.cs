/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Immersal.XR
{
    public class XRSpace : MonoBehaviour, ISceneUpdateable
    {
        [SerializeField]
        private bool m_ProcessPoses = false;

        [SerializeField] [Interface(typeof(IDataProcessor<Matrix4x4>))]
        private Object[] m_DataProcessors;

        public IDataProcessor<Matrix4x4>[] PoseDataProcessors =>
            m_DataProcessors.OfType<IDataProcessor<Matrix4x4>>().ToArray();
        
        public bool ProcessPoses
        {
            get => m_ProcessPoses;
            set => m_ProcessPoses = value;
        }

        public Matrix4x4 InitialPose => m_InitialPose;

        private Transform m_TransformToUpdate;
        private DataProcessingChain<Matrix4x4> m_PoseProcessingChain;

        private Matrix4x4 m_InitialPose = Matrix4x4.identity;
        private Matrix4x4 m_CurrentPose;

        private void Awake()
        {
            m_TransformToUpdate = transform;
            m_InitialPose = Matrix4x4.TRS(m_TransformToUpdate.position, m_TransformToUpdate.rotation, Vector3.one);
            m_CurrentPose = Matrix4x4.TRS(m_TransformToUpdate.localPosition, m_TransformToUpdate.localRotation, m_TransformToUpdate.localScale);
        }

        private void Start()
        {
            if (m_DataProcessors != null)
                m_PoseProcessingChain = new DataProcessingChain<Matrix4x4>(PoseDataProcessors);
        }

        private void Update()
        {
            if (m_ProcessPoses && m_PoseProcessingChain != null)
            {
                m_PoseProcessingChain.UpdateChain();
                m_CurrentPose = m_PoseProcessingChain.GetCurrentData();
                m_TransformToUpdate.SetPositionAndRotation(m_CurrentPose.GetColumn(3), m_CurrentPose.rotation);
            }
        }

        public async Task SceneUpdate(Matrix4x4 poseMatrix)
        {
            if (m_TransformToUpdate == null)
                return;
            
            if (m_ProcessPoses && m_PoseProcessingChain != null)
            {
                await m_PoseProcessingChain.ProcessNewData(poseMatrix);
            }
            else
            {
                m_CurrentPose = poseMatrix;
                m_TransformToUpdate.SetPositionAndRotation(m_CurrentPose.GetColumn(3), m_CurrentPose.rotation);
            }
        }
        
        public Transform GetTransform()
        {
            return transform;
        }

        public void TriggerResetScene()
        {
            ResetScene();
        }

        public async Task ResetScene()
        {
            if (m_ProcessPoses && m_PoseProcessingChain != null)
            {
                await m_PoseProcessingChain.ResetProcessors();
            }
        }
    }
}