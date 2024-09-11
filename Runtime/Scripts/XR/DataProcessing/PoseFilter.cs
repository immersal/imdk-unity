/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Threading.Tasks;
using UnityEngine;

namespace Immersal.XR
{
    public class PoseFilter : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
	    [Header("Experimental options")]
	    [SerializeField] private bool m_UseConfidence = false;
        [SerializeField] private float m_ConfidenceMax = 20f;
        [SerializeField] private float m_ConfidenceImpact = 0.5f;
        
        // processing should be done in map space,
        // option left here for testing
        private const bool m_ProcessInMapSpace = true;
        
        private const int m_HistorySize = 8;
        
		private Vector3[] m_P = new Vector3[m_HistorySize];
		private Vector3[] m_X = new Vector3[m_HistorySize];
		private Vector3[] m_Z = new Vector3[m_HistorySize];
		private float[] m_C = new float[m_HistorySize];
		
		private Vector3[] m_tP = new Vector3[m_HistorySize];
		private Vector3[] m_tX = new Vector3[m_HistorySize];
		private Vector3[] m_tZ = new Vector3[m_HistorySize];
		
		private uint m_Samples = 0;
		
		private bool m_HasProcessedData = false;
		private SceneUpdateData m_CurrentData;
		
		public void InvalidateHistory()
		{
			m_Samples = 0;
		}

		public bool IsValid()
		{
			return m_Samples > 1;
		}

		private void ProcessData(SceneUpdateData data)
		{
			Matrix4x4 pose = data.Pose;
			if (m_ProcessInMapSpace)
				pose = data.Pose.inverse * data.TrackerSpace;
			float confidence = data.LocalizeInfo.confidence;
			int idx = (int)(m_Samples % m_HistorySize);
			
			m_P[idx] = pose.GetColumn(3);
			m_X[idx] = pose.GetColumn(0);
			m_Z[idx] = pose.GetColumn(2);

			float c = 1f;
			if (m_UseConfidence)
			{
				float impact = Mathf.Clamp01(m_ConfidenceImpact);
				float cc = Mathf.Clamp01(confidence / m_ConfidenceMax);
				c = 1f - impact + (cc * impact * 2f);
			}
			m_C[idx] = c;
			m_Samples++;
			uint n = m_Samples > m_HistorySize ? m_HistorySize : m_Samples;
			
			Vector3 position = Filter(m_P, n, m_C, idx);
			Vector3 x = Vector3.Normalize(Filter(m_X, n, m_C, idx));
			Vector3 z = Vector3.Normalize(Filter(m_Z, n, m_C, idx));
			Vector3 up = Vector3.Normalize(Vector3.Cross(z, x));
			Quaternion rotation = Quaternion.LookRotation(z, up);

			if (m_ProcessInMapSpace)
			{
				Matrix4x4 filteredPose = Matrix4x4.TRS(position, rotation, Vector3.one);
				
				// Filter TrackerSpace
				m_tP[idx] = data.TrackerSpace.GetColumn(3);
				m_tX[idx] = data.TrackerSpace.GetColumn(0);
				m_tZ[idx] = data.TrackerSpace.GetColumn(2);
			
				Vector3 tpos = Filter(m_tP, n, m_C, idx);
				Vector3 tx = Vector3.Normalize(Filter(m_tX, n, m_C, idx));
				Vector3 tz = Vector3.Normalize(Filter(m_tZ, n, m_C, idx));
				Vector3 tup = Vector3.Normalize(Vector3.Cross(tz, tx));
				Quaternion trot = Quaternion.LookRotation(tz, tup);
				Matrix4x4 filteredTrackerSpace = Matrix4x4.TRS(tpos, trot, Vector3.one);

				data.Pose = filteredTrackerSpace * filteredPose.inverse;
			}
			else
			{
				data.Pose = Matrix4x4.TRS(position, rotation, Vector3.one);
			}
	
			m_CurrentData = data;
			m_HasProcessedData = true;
		}

		private Vector3 Filter(Vector3[] buf, uint n, float[] confidence, int idx)
		{
			return FilterAVT(buf, n, confidence, idx);
		}

		private Vector3 FilterAVT(Vector3[] buf, uint n, float[] confidence, int idx)
		{
			Vector3 mean = Vector3.zero;
			float totalWeight = 0f;

			// calculate weighted mean
			for (uint i = 0; i < n; i++)
			{
				mean += buf[i] * confidence[i];
				totalWeight += confidence[i];
			}
			mean /= totalWeight;
			
			// return mean when sample count is low
			if (n <= 2)
				return mean;
			
			// calculate standard deviation
			float s = 0;
			for (uint i = 0; i < n; i++)
			{
				Vector3 value = buf[i] * confidence[i];
				s += Vector3.SqrMagnitude(value - mean);
			}
			s /= totalWeight;
			
			// calculate a mean of samples with error less than or equal to st dev
			Vector3 avg = Vector3.zero;
			totalWeight = 0f;
			for (uint i = 0; i < n; i++)
			{
				// for each sample, get error
				Vector3 value = buf[i] * confidence[i];
				float d = Vector3.SqrMagnitude(value - mean);
				
				// if error <= st dev, count it in
				if (d <= s)
				{
					avg += buf[i] * confidence[i];
					totalWeight += confidence[i];
				}
			}
			if (totalWeight > 0)
			{
				avg /= totalWeight;
				return avg;
			}
			return mean;
		}
		
		public Task<SceneUpdateData> ProcessData(SceneUpdateData data, DataProcessorTrigger trigger)
		{
			// skip on updates with no new data
			if (trigger == DataProcessorTrigger.Update)
			{
				if (m_HasProcessedData)
					return Task.FromResult(m_CurrentData);
				return Task.FromResult(data);
			}
			
			ProcessData(data);

			return Task.FromResult(m_CurrentData);
		}

		public Task ResetProcessor()
		{
			InvalidateHistory();
			return Task.CompletedTask;
		}
    }

}