/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using UnityEditor;
using UnityEngine;

namespace Immersal.XR
{
    public interface IMapOption
    {
        string Name { get; }
        void DrawEditorGUI(XRMap map);
    }
    
    // Example IMapOption implementation
    [Serializable]
    public class IntOption : IMapOption
    {
        public string Name { get; private set; }

        [SerializeField]
        public int Value;

        public IntOption(string name, int initialValue)
        {
            Name = name;
            Value = initialValue;
        }

        public void DrawEditorGUI(XRMap map)
        {
#if UNITY_EDITOR
            Value = EditorGUILayout.IntField(Name, Value);
#endif
        }
    }
    
    [Serializable]
    public class SerializableMapOption
    {
        public string TypeName;
        public string Data;

        // Serialize an IMapOption instance
        public static SerializableMapOption Serialize(IMapOption option)
        {
            var serializableOption = new SerializableMapOption
            {
                TypeName = option.GetType().AssemblyQualifiedName,
                Data = JsonUtility.ToJson(option)
            };
            return serializableOption;
        }

        // Deserialize into an IMapOption instance
        public IMapOption Deserialize()
        {
            var type = Type.GetType(TypeName);
            if (type != null)
            {
                return (IMapOption)JsonUtility.FromJson(Data, type);
            }
            return null;
        }
    }
}

