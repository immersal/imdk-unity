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
        
        [SerializeField] [Interface(typeof(IDataProcessor<SceneUpdateData>))]
        private Object[] m_DataProcessors;

        public IDataProcessor<SceneUpdateData>[] SceneDataProcessors =>
            m_DataProcessors.OfType<IDataProcessor<SceneUpdateData>>().ToArray();
        
        public bool ProcessPoses
        {
            get => m_ProcessPoses;
            set => m_ProcessPoses = value;
        }

        public Matrix4x4 InitialPose => m_InitialPose;

        private Transform m_TransformToUpdate;
        private IDataProcessingChain<SceneUpdateData> m_DataProcessingChain;

        private Matrix4x4 m_InitialPose = Matrix4x4.identity;
        private SceneUpdateData m_CurrentData;

        private void Awake()
        {
            m_TransformToUpdate = transform;
            m_InitialPose = Matrix4x4.TRS(m_TransformToUpdate.position, m_TransformToUpdate.rotation, Vector3.one);
            m_CurrentData = null;
            
            if (m_DataProcessors != null)
            {
                m_DataProcessingChain = new DataProcessingChain<SceneUpdateData>(SceneDataProcessors);
            }
            else
            {
                m_DataProcessingChain =
                    new DataProcessingChain<SceneUpdateData>();
            }
        }
        
        private void Start()
        {
            
                
        }
        
        private async void Update()
        {
            if (m_ProcessPoses && m_DataProcessingChain != null)
            {
                await m_DataProcessingChain.UpdateChain();
                SceneUpdateData result = m_DataProcessingChain.GetCurrentData();
                m_CurrentData = result;
                UpdateSpace(m_CurrentData);
            }
        }
        
        public async Task SceneUpdate(SceneUpdateData data)
        {
            if (!m_TransformToUpdate)
                return;
            
            if (m_ProcessPoses && m_DataProcessingChain != null)
            {
                await m_DataProcessingChain.ProcessNewData(data);
            }
            else
            {
                m_CurrentData = data;
                UpdateSpace(m_CurrentData);
            }
        }
        
        private void UpdateSpace(SceneUpdateData data)
        {
            if (data == null || data.Ignore || !data.Pose.ValidTRS()) return;
            Matrix4x4 pose = data.Pose;
            m_TransformToUpdate.SetPositionAndRotation(pose.GetPosition(), pose.rotation);
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
            if (m_ProcessPoses && m_DataProcessingChain != null)
            {
                await m_DataProcessingChain.ResetProcessors();
            }
        }

        public void AddSceneDataProcessor(IDataProcessor<SceneUpdateData>
            processor)
        {
            m_DataProcessingChain.AddProcessor(processor);
        }
        
        public void RemoveSceneDataProcessor(IDataProcessor<SceneUpdateData>
            processor)
        {
            m_DataProcessingChain.RemoveProcessor(processor);
        }
    }
}