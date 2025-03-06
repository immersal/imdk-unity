/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Threading.Tasks;

namespace Immersal.XR
{
    public interface IDataProcessingChain<T>
    {
        public Task ProcessNewData(T inputData);
        public Task UpdateChain();
        public T GetCurrentData();
        public Task ResetProcessors();
        public void AddProcessor(IDataProcessor<T> processor);
        public void RemoveProcessor(IDataProcessor<T> processor);
    }
}
