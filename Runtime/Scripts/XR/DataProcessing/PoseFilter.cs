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
	public enum FilterMethod
	{
		Default,
		Advanced,
		Legacy,
	}
	
    public class PoseFilter : MonoBehaviour, IDataProcessor<SceneUpdateData>
    {
	    // Note:
	    // Custom editor does not draw default inspector
	    
	    [SerializeField] private FilterMethod m_FilterMethod = FilterMethod.Default;
	    [SerializeField] private int m_HistorySize = 8;
	    [SerializeField] private bool m_UseConfidence = false;
	    [SerializeField] private float m_ConfidenceMax = 50f;
	    [SerializeField] private float m_ConfidenceImpact = 0.5f;
	    
	    private Dictionary<int, IImmersalFilter> m_MapFilters;
	    private IImmersalFilter m_LegacyFilter = new AVTFilter();
		private bool m_HasProcessedData = false;
		private SceneUpdateData m_CurrentData;

		public FilterMethod FilterMethod
		{
			get => m_FilterMethod;
			set => m_FilterMethod = value;
		}

		public int HistorySize
		{
			get => m_HistorySize;
			set => m_HistorySize = value;
		}

		public bool UseConfidence
		{
			get => m_UseConfidence;
			set => m_UseConfidence = value;
		}

		public float ConfidenceMax
		{
			get => m_ConfidenceMax;
			set => m_ConfidenceMax = value;
		}

		public float ConfidenceImpact
		{
			get => m_ConfidenceImpact;
			set => m_ConfidenceImpact = value;
		}

		private void OnValidate()
		{
			m_HistorySize = m_FilterMethod == FilterMethod.Advanced ? Mathf.Max(2, m_HistorySize) : 8;
			m_UseConfidence = m_UseConfidence && m_FilterMethod == FilterMethod.Advanced;
		}

		private void Awake()
		{
			m_MapFilters = new Dictionary<int, IImmersalFilter>();
		}

		private void ProcessData(SceneUpdateData data)
		{
			// Legacy method
			if (m_FilterMethod == FilterMethod.Legacy)
			{
				ProcessDataLegacy(data);
				return;
			}
			
			// We want to do filtering in a specific relative space
			Matrix4x4 pose = PreFilterTransform(data);
			
			int mapID = data.MapEntry.Map.mapId;

			// Use existing filter or create new
			if (m_MapFilters.TryGetValue(mapID, out IImmersalFilter filter))
			{
				pose = filter.Filter(pose, data);
			}
			else
			{
				m_MapFilters.Add(mapID, CreateFilter());
				pose = m_MapFilters[mapID].Filter(pose, data);
			}

			// Filtered pose must be transformed to correct space
			pose = PostFilterTransform(pose, data);
			
			data.Pose = pose;
			
			m_CurrentData = data;
			m_HasProcessedData = true;
		}

		private IImmersalFilter CreateFilter()
		{
			return new AVTFilter(m_HistorySize, m_UseConfidence, m_ConfidenceMax, m_ConfidenceImpact);
		}

		private void ProcessDataLegacy(SceneUpdateData data)
		{
			Matrix4x4 pose = m_LegacyFilter.Filter(data.Pose, data);
			data.Pose = pose;
			m_CurrentData = data;
			m_HasProcessedData = true;
		}

		// Gets localized pose in tracker space without MapRelation
		private Matrix4x4 PreFilterTransform(SceneUpdateData data)
		{
			Vector3 pos = data.LocalizeInfo.position;
			Quaternion rot = data.LocalizeInfo.rotation;
			rot *= data.CameraData.Orientation;
			pos.SwitchHandedness();
			rot.SwitchHandedness();
			Matrix4x4 imSpacePose = Matrix4x4.TRS(pos, rot, Vector3.one);
			return data.TrackerSpace * imSpacePose.inverse;
		}

		// Applies MapRelation in cloud space and transforms back to tracker space
		private Matrix4x4 PostFilterTransform(Matrix4x4 pose, SceneUpdateData data)
		{
			Matrix4x4 imSpace = pose.inverse * data.TrackerSpace;
			Vector3 imPos = imSpace.GetColumn(3);
			Quaternion imRot = imSpace.rotation;
			
			MapToSpaceRelation mo = data.MapEntry.Relation;
			Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.Position, mo.Rotation, Vector3.one);
			Vector3 scaledPos = Vector3.Scale(imPos, mo.Scale);
			Matrix4x4 result = offsetNoScale * Matrix4x4.TRS(scaledPos, imRot, Vector3.one);
			
			return data.TrackerSpace * result.inverse;
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
			foreach (KeyValuePair<int,IImmersalFilter> keyValuePair in m_MapFilters)
			{
				keyValuePair.Value.Reset();
			}
			m_LegacyFilter.Reset();
			return Task.CompletedTask;
		}

		public void ForgetIndividualFilters()
		{
			m_MapFilters = new Dictionary<int, IImmersalFilter>();
		}
    }
    
    public interface IImmersalFilter
    {
	    public Matrix4x4 Filter(Matrix4x4 pose, SceneUpdateData data);
	    public void Reset();
    }
}

