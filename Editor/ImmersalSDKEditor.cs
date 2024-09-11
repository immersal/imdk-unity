/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

#if UNITY_EDITOR
using System;
using System.Collections;
using Immersal.XR;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Networking;
using Immersal.REST;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Immersal
{
    [CustomEditor(typeof(ImmersalSDK))]
    public class ImmersalSDKEditor : Editor, IPreprocessBuildWithReport
    {
        private SerializedProperty immersalServer;
        private SerializedProperty immersalServerUrl;

        private void OnEnable()
        {
            immersalServer = serializedObject.FindProperty("ImmersalServer");
            immersalServerUrl = serializedObject.FindProperty("m_CustomServerUrl");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ImmersalSDK obj = (ImmersalSDK)target;
            
            EditorGUILayout.HelpBox("Immersal SDK v" + ImmersalSDK.sdkVersion, MessageType.Info);
            
            EditorGUILayout.LabelField("Cloud configuration", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            ImmersalSDK.APIServer serverSelection = (ImmersalSDK.APIServer)EditorGUILayout.EnumPopup("Immersal Server", (ImmersalSDK.APIServer)immersalServer.enumValueIndex);
            if (EditorGUI.EndChangeCheck())
            {
                immersalServer.enumValueIndex = (int)serverSelection;
            }

            if (serverSelection == ImmersalSDK.APIServer.CustomServer && immersalServerUrl != null)
            {
                EditorGUILayout.PropertyField(immersalServerUrl);
            }
            
            DrawPropertiesExcluding(serializedObject, "m_Script", "ImmersalServer", "m_CustomServerUrl");
            serializedObject.ApplyModifiedProperties();
        }
        
        // Preprocess builds
        public int callbackOrder => 0;
        
        public void OnPreprocessBuild(BuildReport report)
        {
            // ARMaps
            XRMap[] maps = FindObjectsOfType<XRMap>();
            foreach (XRMap map in maps)
            {
                if (!map.PreBuildCheck(out string msg))
                {
                    throw new BuildFailedException(msg);
                }
            }
        }
    }

    public class LoginWindow: EditorWindow
    {
        private string myEmail = "";
        private string myPassword = "";
        private string myToken = "";
        private ImmersalSDK sdk = null;

        private static UnityWebRequest request;

        [MenuItem("Immersal SDK/Login")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<LoginWindow>("Immersal Login");
        }

        void OnGUI()
        {
            GUILayout.Label("Credentials", EditorStyles.boldLabel);
            myEmail = EditorGUILayout.TextField("Email", myEmail);
            myPassword = EditorGUILayout.PasswordField("Password", myPassword);

            if (GUILayout.Button("Login"))
            {
                SDKLoginRequest loginRequest = new SDKLoginRequest();
                loginRequest.login = myEmail;
                loginRequest.password = myPassword;

                EditorCoroutineUtility.StartCoroutine(Login(loginRequest), this);
            }

            EditorGUILayout.Separator();

            myToken = EditorGUILayout.TextField("Token", myToken);

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("(C) 2023 Immersal - Part of Hexagon. All Right Reserved.");
        }

        void OnInspectorUpdate()
        {
            Repaint();
        }

        IEnumerator Login(SDKLoginRequest loginRequest)
        {
            string jsonString = JsonUtility.ToJson(loginRequest);
            sdk = ImmersalSDK.Instance;
            using (UnityWebRequest request = UnityWebRequest.Put(string.Format(ImmersalHttp.URL_FORMAT, sdk.localizationServer, SDKLoginRequest.endpoint), jsonString))
            {
                request.method = UnityWebRequest.kHttpVerbPOST;
                request.useHttpContinue = false;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogError(request.error);
                }
                else
                {
                    SDKLoginResult loginResult = JsonUtility.FromJson<SDKLoginResult>(request.downloadHandler.text);
                    if (loginResult.error == "none")
                    {
                        myToken = loginResult.token;
                        sdk.developerToken = myToken;
                        
                        // Apply override to the prefab instance
                        PrefabUtility.RecordPrefabInstancePropertyModifications(sdk);

                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }
            }
        }
    }
}
#endif