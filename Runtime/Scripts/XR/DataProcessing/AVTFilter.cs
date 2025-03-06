using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Immersal.XR
{
	public class AVTFilter : IImmersalFilter
    {
	    public bool m_UseConfidence = false;
	    public float m_ConfidenceMax = 50f;
	    public float m_ConfidenceImpact = 0.5f;
	    
	    private readonly int m_HistorySize = 8;
	    private Vector3[] m_P;
	    private Vector3[] m_X;
	    private Vector3[] m_Z;
	    private float[] m_C;
		
	    private uint m_Samples = 0;

	    public AVTFilter()
	    {
		    m_P = new Vector3[m_HistorySize];
		    m_X = new Vector3[m_HistorySize];
		    m_Z = new Vector3[m_HistorySize];
		    m_C = new float[m_HistorySize];
	    }

	    public AVTFilter(int historySize, bool useConfidence, float confidenceMax, float confidenceImpact)
	    {
		    m_HistorySize = historySize;
		    m_P = new Vector3[m_HistorySize];
		    m_X = new Vector3[m_HistorySize];
		    m_Z = new Vector3[m_HistorySize];
		    m_C = new float[m_HistorySize];
		    m_UseConfidence = useConfidence;
		    m_ConfidenceMax = confidenceMax;
		    m_ConfidenceImpact = confidenceImpact;
	    }
	    
	    public void InvalidateHistory()
	    {
		    m_Samples = 0;
	    }
	    
	    public bool IsValid()
	    {
		    return m_Samples > 1;
	    }
	    
    	public Matrix4x4 Filter(Matrix4x4 pose, SceneUpdateData data)
    	{
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
		    uint n = m_Samples > m_HistorySize ? (uint)m_HistorySize : m_Samples;
			
		    Vector3 position = FilterAVT(m_P, n, m_C, idx);
		    Vector3 x = Vector3.Normalize(FilterAVT(m_X, n, m_C, idx));
		    Vector3 z = Vector3.Normalize(FilterAVT(m_Z, n, m_C, idx));
		    Vector3 up = Vector3.Normalize(Vector3.Cross(z, x));
		    Quaternion rotation = Quaternion.LookRotation(z, up);

		    Matrix4x4 filteredPose = Matrix4x4.TRS(position, rotation, Vector3.one);
		    return filteredPose;
	    }
    	
        private Vector3 FilterAVT(Vector3[] buf, uint n, float[] confidence, int idx)
        {
        	Vector3 mean = Vector3.zero;
        	float totalWeight = 0f;
    
        	// Calculate weighted mean
        	for (uint i = 0; i < n; i++)
        	{
        		mean += buf[i] * confidence[i];
        		totalWeight += confidence[i];
        	}
        	mean /= totalWeight;
        	
        	// Return mean when sample count is low
        	if (n <= 2)
        		return mean;
        	
        	// Calculate standard deviation / variance
        	float s = 0;
        	for (uint i = 0; i < n; i++)
        	{
        		Vector3 value = buf[i] * confidence[i];
        		s += Vector3.SqrMagnitude(value - mean);
        	}
        	s /= totalWeight;
        	
        	// Calculate a mean of samples with error less than or equal to st dev
        	Vector3 avg = Vector3.zero;
        	totalWeight = 0f;
        	for (uint i = 0; i < n; i++)
        	{
        		// For each sample, get error
        		Vector3 value = buf[i] * confidence[i];
        		float d = Vector3.SqrMagnitude(value - mean);
        		
        		// If error <= st dev, count it in
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

        public void Reset()
        {
	        InvalidateHistory();
        }
    }
}
