/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Immersal
{
    [CustomPropertyDrawer(typeof(InterfaceAttribute))]
    public class InterfaceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InterfaceAttribute att = attribute as InterfaceAttribute;

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label.text, "InterfaceType Attribute can only be used with MonoBehaviour Components!");
                return;
            }

            // Pick a specific component
            MonoBehaviour oldComp = property.objectReferenceValue as MonoBehaviour;

            GameObject temp = null;
            string oldName = "";

            if (Event.current.type == EventType.Repaint)
            {
                if (oldComp == null)
                {
                    temp = new GameObject("None [" + att.TypeOfInterface.Name + "]");
                    oldComp = temp.AddComponent<MonoInterface>();
                }
                else
                {
                    oldName = oldComp.name;
                    oldComp.name = oldName + " [" + att.TypeOfInterface.Name + "]";
                }
            }

            MonoBehaviour comp = EditorGUI.ObjectField(position, label, oldComp, typeof(MonoBehaviour), true) as MonoBehaviour;

            if (Event.current.type == EventType.Repaint)
            {
                if (temp != null)
                    GameObject.DestroyImmediate(temp);
                else
                    oldComp.name = oldName;
            }

            // Make sure something changed.
            if (oldComp == comp) return;

            // If a component is assigned, make sure it is the interface we are looking for.
            if (comp != null)
            {
                // Make sure component is of the right interface
                if (comp.GetType() != att.TypeOfInterface)
                    // Component failed. Check game object.
                    comp = comp.gameObject.GetComponent(att.TypeOfInterface) as MonoBehaviour;

                // Item failed test. Do not override old component
                if (comp == null)
                {
                    var previousColor = GUI.color;
                    GUI.color = Color.red;
                    EditorGUI.LabelField(position, label, new GUIContent("Assigned Property does not implement required interface!"));
                    GUI.color = previousColor;
                    
                    return;
                }
            }

            property.objectReferenceValue = comp;
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    public class MonoInterface : MonoBehaviour
    {
    }
}
#endif