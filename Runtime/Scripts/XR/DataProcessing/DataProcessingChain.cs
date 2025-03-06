/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Immersal.XR
{
    public class DataProcessingChain<T> : IDataProcessingChain<T> where T : class
    {
        //private IDataProcessor<T>[] m_DataProcessors;
        private List<IDataProcessor<T>> m_DataProcessors;
        private T m_CurrentDataInChain = null;
        private bool m_IsProcessing = false;

        public DataProcessingChain()
        {
            m_DataProcessors = new List<IDataProcessor<T>>();
        }
        
        public DataProcessingChain(IDataProcessor<T>[] dataProcessors)
        {
            //m_DataProcessors = dataProcessors;
            m_DataProcessors = new List<IDataProcessor<T>>(dataProcessors);
        }

        public void AddProcessor(IDataProcessor<T> processor)
        {
            m_DataProcessors.Add(processor);
        }

        public void RemoveProcessor(IDataProcessor<T> processor)
        {
            m_DataProcessors.Remove(processor);
        }

        public async Task ProcessNewData(T inputData)
        {
            await ProcessChain(inputData, DataProcessorTrigger.NewData);
        }

        public async Task UpdateChain()
        {
            if (m_CurrentDataInChain == null)
            {
                return;
            }
            
            await ProcessChain(m_CurrentDataInChain, DataProcessorTrigger.Update);
        }

        private async Task ProcessChain(T inputData, DataProcessorTrigger trigger)
        {
            if (m_IsProcessing)
                return;

            if (inputData == null)
                return;

            m_IsProcessing = true;
            
            T dataBeingProcessed = inputData;

            foreach (IDataProcessor<T> processor in m_DataProcessors)
            {
                dataBeingProcessed = await processor.ProcessData(dataBeingProcessed, trigger);
            }

            m_CurrentDataInChain = dataBeingProcessed;

            m_IsProcessing = false;
        }

        public T GetCurrentData()
        {
            return m_CurrentDataInChain;
        }

        public async Task ResetProcessors()
        {
            foreach (IDataProcessor<T> processor in m_DataProcessors)
            {
                await processor.ResetProcessor();
            }
        }
    }
    
    public interface IDataProcessor<T>
    {
        Task<T> ProcessData(T data, DataProcessorTrigger trigger);
        Task ResetProcessor();
    }
    
    public enum DataProcessorTrigger
    {
        NewData,
        Update
    }
}