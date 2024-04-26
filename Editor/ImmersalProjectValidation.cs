/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

namespace Immersal
{
    internal enum ProjectValidationState
    {
        Uninitialized = 0,
        Issues = 1,
        NoIssues = 2
    }
    
    internal class ImmersalProjectValidationWindow : EditorWindow, IActiveBuildTargetChanged
    {
        private static ImmersalProjectValidationWindow window = null;
        
        private Vector2 m_ScrollViewPos = Vector2.zero;
        
        private static ProjectValidationState SavedValidationState
        {
            get => (ProjectValidationState)PlayerPrefs.GetInt(ImmersalProjectValidation.PlayerPrefsStateString, 0);
            set => PlayerPrefs.SetInt(ImmersalProjectValidation.PlayerPrefsStateString, (int)value);
        }
        
        [InitializeOnLoadMethod]
        internal static void InitializeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (SavedValidationState is ProjectValidationState.Uninitialized or ProjectValidationState.Issues)
                {
                    SavedValidationState = ProjectValidationState.Issues;
                    ShowWindow();
                }
            };
        }

        [MenuItem("Immersal SDK/Project Validation")]
        private static void MenuItem()
        {
            ShowWindow();
        }

        private static void ShowWindow(BuildTargetGroup buildTargetGroup = BuildTargetGroup.Unknown)
        {
            if (window == null)
            {
                window = (ImmersalProjectValidationWindow) GetWindow(typeof(ImmersalProjectValidationWindow));
                window.titleContent = Content.Title;
                window.minSize = new Vector2(500.0f, 300.0f);
            }
            window.UpdateIssues();
            window.Show();
        }

        private static void InitStyles()
        {
            if (Styles.s_ListLabel != null)
                return;

            Styles.s_ListLabel = new GUIStyle(Styles.s_SelectionStyle)
            {
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(5, 5, 5, 5)
            };
            
            Styles.s_TargetPlatformLabel = new GUIStyle(EditorStyles.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(10, 10, 0, 0),
            };

            Styles.s_IssuesTitleLabel = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 0, 0),
            };

            Styles.s_Wrap = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 5, 5, 5)
            };

            Styles.s_Icon = new GUIStyle(EditorStyles.label)
            {
                margin = new RectOffset(10, 10, 8, 0),
                fixedWidth = Content.IconSize.x * 2
            };

            Styles.s_InfoBanner = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(10, 10, 15, 5)
            };
            
            Styles.s_Fix = new GUIStyle(EditorStyles.miniButton)
            {
                stretchWidth = false,
                fixedWidth = 80,
                margin = new RectOffset(0, 0, 8, 5)
            };

            Styles.s_FixAll = new GUIStyle(EditorStyles.miniButton)
            {
                stretchWidth = false,
                fixedWidth = 80,
                margin = new RectOffset(0, 10, 5, 8)
            };
            
        }

        private readonly List<ImmersalProjectValidation.ProjectIssue> projectIssues = new List<ImmersalProjectValidation.ProjectIssue>();
        private List<ImmersalProjectValidation.ProjectIssue> fixAllIssues = new List<ImmersalProjectValidation.ProjectIssue>();

        private double lastUpdate;
        private const double updateInterval = 1.0;
        private const double backgroundUpdateInterval = 1.0;

        private static class Content
        {
            public static readonly GUIContent Title = new GUIContent("Immersal Project Validation", "");
            public static readonly GUIContent WarningIcon = EditorGUIUtility.IconContent("Warning@2x");
            public static readonly GUIContent ErrorIcon = EditorGUIUtility.IconContent("Error@2x");
            public static readonly GUIContent FixButton = new GUIContent("Fix", "");
            public static readonly GUIContent PlayMode = new GUIContent("Exit play mode", EditorGUIUtility.IconContent("console.infoicon").image);
            public static readonly Vector2 IconSize = new Vector2(16.0f, 16.0f);
        }

        private static class Styles
        {
            public static GUIStyle s_SelectionStyle = "TV Selection";
            public static GUIStyle s_IssuesBackground = "ScrollViewAlt";
            public static GUIStyle s_ListLabel;
            public static GUIStyle s_TargetPlatformLabel;
            public static GUIStyle s_IssuesTitleLabel;
            public static GUIStyle s_Wrap;
            public static GUIStyle s_Icon;
            public static GUIStyle s_InfoBanner;
            public static GUIStyle s_Fix;
            public static GUIStyle s_FixAll;
        }

        protected void OnFocus() => UpdateIssues(true);

        protected void Update() => UpdateIssues();

        private void UpdateIssues(bool force = false)
        {
            var interval = EditorWindow.focusedWindow == this ? updateInterval : backgroundUpdateInterval;
            if (!force && EditorApplication.timeSinceStartup - lastUpdate < interval)
                return;

            // Fix all
            foreach (ImmersalProjectValidation.ProjectIssue issue in fixAllIssues)
            {
                issue.Fix?.Invoke();
            }
            fixAllIssues.Clear();

            ImmersalProjectValidation.CheckIssues(projectIssues);
            Repaint();

            if (projectIssues.Count > 0)
            {
                if(SavedValidationState != ProjectValidationState.Uninitialized)
                    SavedValidationState = ProjectValidationState.Issues;
                
            }
            else
            {
                SavedValidationState = ProjectValidationState.NoIssues;
            }

            lastUpdate = EditorApplication.timeSinceStartup;
        }
        
        public void OnGUI()
        {
            InitStyles();

            EditorGUIUtility.SetIconSize(Content.IconSize);

            using (new EditorGUI.DisabledScope(fixAllIssues.Count > 0))
            {
                EditorGUILayout.BeginVertical();

                if (EditorApplication.isPlaying && projectIssues.Count > 0)
                {
                    GUILayout.Label(Content.PlayMode, Styles.s_InfoBanner);
                }

                EditorGUILayout.Space();

                bool fixableIssues = projectIssues.Any(f => f.Fix != null);

                EditorGUILayout.LabelField($"Target platform: {ImmersalProjectValidation.ActiveBuildTarget.ToString()}", Styles.s_TargetPlatformLabel);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    EditorGUILayout.LabelField($"Project issues ({projectIssues.Count}):", Styles.s_IssuesTitleLabel);
                }

                if (fixableIssues)
                {
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    {
                        if (GUILayout.Button("Fix All", Styles.s_FixAll))
                        {
                            fixAllIssues = new List<ImmersalProjectValidation.ProjectIssue>();
                            foreach (ImmersalProjectValidation.ProjectIssue issue in projectIssues)
                            {
                                if (!issue.RequiresManualFix)
                                    fixAllIssues.Add(issue);
                            }
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();

                m_ScrollViewPos = EditorGUILayout.BeginScrollView(m_ScrollViewPos, Styles.s_IssuesBackground,
                    GUILayout.ExpandHeight(true));

                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    foreach (ImmersalProjectValidation.ProjectIssue issue in projectIssues)
                    {
                        EditorGUILayout.BeginHorizontal(Styles.s_ListLabel);

                        GUILayout.Label(issue.Error ? Content.ErrorIcon : Content.WarningIcon, Styles.s_Icon,
                            GUILayout.Width(Content.IconSize.x));
                        GUILayout.Label(issue.Message(), Styles.s_Wrap);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("", GUILayout.Width(Content.IconSize.x * 1.5f));

                        if (issue.Fix != null)
                        {
                            if (GUILayout.Button(Content.FixButton, Styles.s_Fix))
                            {
                                issue.Fix();
                            }
                        }
                        else if (fixableIssues)
                        {
                            GUILayout.Label("", GUILayout.Width(80.0f));
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndVertical();
            }
        }

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            UpdateIssues(true);
        }

        public int callbackOrder => 0;
    }

#if UNITY_EDITOR
    public static class ImmersalProjectValidation
    {
        public class ProjectIssue
        {
            internal ProjectIssue() {}
            public Func<string> Message;
            public Func<bool> Check;
            public Action Fix;
            public bool Error;
            public bool RequiresManualFix;
        }

        public static string PlayerPrefsStateString = "ImmersalProjectValidationState"; 
        
        public static BuildTargetGroup ActiveBuildTargetGroup = ActiveBuildTarget switch
        {
            BuildTarget.iOS => BuildTargetGroup.iOS,
            BuildTarget.Android => BuildTargetGroup.Android,
            BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
            BuildTarget.StandaloneWindows => BuildTargetGroup.Standalone,
            BuildTarget.StandaloneWindows64 => BuildTargetGroup.Standalone,
            BuildTarget.StandaloneLinux64 => BuildTargetGroup.Standalone,
            BuildTarget.WSAPlayer => BuildTargetGroup.WSA,
            _ => BuildTargetGroup.Unknown
        };
        
        public static BuildTarget ActiveBuildTarget => EditorUserBuildSettings.activeBuildTarget;
        
        public static void CheckIssues(List<ProjectIssue> issues)
        {
            issues.Clear();
            foreach (ProjectIssue issue in ProjectIssues)
            {
                if (!issue.Check?.Invoke() ?? false)
                {
                    issues.Add(issue);
                }
            }
        }
        
        // ReSharper disable once HeapView.ObjectAllocation
        private static readonly ProjectIssue[] ProjectIssues =
        {
            // Graphics API
#if !IMMERSAL_MAGIC_LEAP_ENABLED
            new ProjectIssue()
            {
                Message = () =>
                {
                    return ActiveBuildTarget switch
                    {
                        BuildTarget.iOS => "Graphics API must be set to Metal",
                        BuildTarget.Android => "Graphics API must be set to OpenGLES3",
                        BuildTarget.StandaloneWindows => "Graphics API must set to OpenGLCore",
                        _ => "Unsupported GraphicsAPI for current build target."
                    };
                },
                Check = () =>
                {
                    GraphicsDeviceType[] graphicAPIs = PlayerSettings.GetGraphicsAPIs(ActiveBuildTarget);
                    return ActiveBuildTarget switch
                    {
                        BuildTarget.iOS => graphicAPIs.Length == 1 && graphicAPIs[0] == GraphicsDeviceType.Metal,
                        BuildTarget.Android => graphicAPIs.Length == 1 && graphicAPIs[0] == GraphicsDeviceType.OpenGLES3,
                        BuildTarget.StandaloneWindows => graphicAPIs.Length == 1 && graphicAPIs[0] == GraphicsDeviceType.OpenGLCore,
                        _ => true
                    };
                },
                Fix = () =>
                {
                    GraphicsDeviceType[] cga = PlayerSettings.GetGraphicsAPIs(ActiveBuildTarget);
                    var autoGraphicAPI = PlayerSettings.GetUseDefaultGraphicsAPIs(ActiveBuildTarget);
                    if (autoGraphicAPI)
                        PlayerSettings.SetUseDefaultGraphicsAPIs(ActiveBuildTarget, false);

                    GraphicsDeviceType[] graphicAPIs = ActiveBuildTarget switch
                    {
                        BuildTarget.iOS => new GraphicsDeviceType[] { GraphicsDeviceType.Metal },
                        BuildTarget.Android => new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 },
                        BuildTarget.StandaloneWindows => new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLCore },
                        _ => PlayerSettings.GetGraphicsAPIs(ActiveBuildTarget)
                    };

                    PlayerSettings.SetGraphicsAPIs(ActiveBuildTarget, graphicAPIs);
                },
                Error = true,
            },
            // IL2CPP
            new ProjectIssue()
            {
                Message = () => "IL2CPP must be enabled.",
                Check = () => PlayerSettings.GetScriptingBackend(ActiveBuildTargetGroup) == ScriptingImplementation.IL2CPP,
                Fix = () => { PlayerSettings.SetScriptingBackend(ActiveBuildTargetGroup, ScriptingImplementation.IL2CPP); },
                Error = true,
            },
            // Allow unsafe code
            new ProjectIssue()
            {
                Message = () => "Allow 'unsafe' code must be enabled.",
                Check = () => PlayerSettings.allowUnsafeCode,
                Fix = () => { PlayerSettings.allowUnsafeCode = true; },
                Error = true,
            },
            // Camera usage description
            new ProjectIssue()
            {
                Message = () => "Camera Usage Description must be defined.",
                Check = () => ActiveBuildTarget != BuildTarget.iOS || PlayerSettings.iOS.cameraUsageDescription != "",
                Fix = () => { PlayerSettings.iOS.cameraUsageDescription = "Required for augmented reality support."; },
                Error = true,
            },
            // Location usage description
            new ProjectIssue()
            {
                Message = () => "Location Usage Description must be defined.",
                Check = () => ActiveBuildTarget != BuildTarget.iOS || PlayerSettings.iOS.locationUsageDescription != "",
                Fix = () => { PlayerSettings.iOS.locationUsageDescription = "Required for satellite positioning support."; },
                Error = true,
            },
            // minimum ios version 12.0
            new ProjectIssue()
            {
                Message = () => "Target minimum iOS Version must be 12.0 or higher.",
                Check = () => ActiveBuildTarget != BuildTarget.iOS || (float.TryParse(PlayerSettings.iOS.targetOSVersionString, out float minVersion) && minVersion >= 12.0f),
                Fix = () => { PlayerSettings.iOS.targetOSVersionString = "12.0"; },
                Error = true,
            },
            // minimum android api version 26
            new ProjectIssue()
            {
                Message = () => "Minimum Android API Level must be 26 or higher.",
                Check = () => ActiveBuildTarget != BuildTarget.Android ||
                              PlayerSettings.Android.minSdkVersion >= AndroidSdkVersions.AndroidApiLevel26,
                Fix = () => { PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; },
                Error = true,
            },
            // ARM64
            new ProjectIssue()
            {
                Message = () => "ARM64 Target Architecture must be enabled.",
                Check = () => ActiveBuildTarget != BuildTarget.Android ||
                              (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0,
                Fix = () => { PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64; },
                Error = true,
            },
            // ARKit loader
            new ProjectIssue()
            {
                Message = () => "ARKit XR-Plugin Provider must be enabled.",
                Check = () => ActiveBuildTarget != BuildTarget.iOS || IsPluginLoaderEnabled("AR Kit Loader") ,
                Fix = () =>
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = ActiveBuildTargetGroup;
                    SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                },
                Error = true,
                RequiresManualFix = true
            },
            // ARCore loader
            new ProjectIssue()
            {
                Message = () => "ARCore XR-Plugin Provider must be enabled.",
                Check = () => ActiveBuildTarget != BuildTarget.Android || IsPluginLoaderEnabled("AR Core Loader"),
                Fix = () =>
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = ActiveBuildTargetGroup;
                    SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                },
                Error = true,
                RequiresManualFix = true
            },
            new ProjectIssue()
            {
                Message = () => "Universal Render Pipeline is recommended",
                Check = () => GraphicsSettings.defaultRenderPipeline != null,
                Fix = SetupRenderPipeline,
                Error = false,
                RequiresManualFix = false
            },
            // android manifest exists
            new ProjectIssue()
            {
                Message = () => "Custom Android Manifest is required.",
                Check = () => ActiveBuildTarget != BuildTarget.Android || CheckManifestExists(),
                Fix = CreateManifest,
                Error = true,
            },
            // android manifest has necessary attributes
            new ProjectIssue()
            {
                Message = () => "Android Manifest should include network permissions",
                Check = () => ActiveBuildTarget != BuildTarget.Android || CheckManifestContent(),
                Fix = ConfigureManifest,
                Error = false,
            },
#elif IMMERSAL_MAGIC_LEAP_ENABLED
            new ProjectIssue()
            {
                Message = () => "Please refer to Magic Leap 2 documentation for project validation",
                Check = () => false,
                Fix = null,
                Error = false,
            },
#endif
        };
        
        private static bool IsPluginLoaderEnabled(string loaderName)
        {
            XRGeneralSettings generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(ActiveBuildTargetGroup);
            if (generalSettings == null)
                return false;
                
            XRManagerSettings managerSettings = generalSettings.AssignedSettings;

            string loaderNameNoWhitespace = loaderName.Replace(" ", "");
            return managerSettings != null && managerSettings.activeLoaders.Any(loader => (loader.name == loaderName || loader.name == loaderNameNoWhitespace));
        }
        
        private static void SetupRenderPipeline()
        {
            string destinationPath = Application.dataPath;
            
            // Renderer and RenderPipelineAsset
            if (CopyFromPackage("Editor/ImmersalURPAsset_Renderer.asset", Path.Combine(destinationPath, "ImmersalURPAsset_Renderer.asset"), true))
            {
                // Load RendererData
                string rendererDataPath = Path.Combine("Assets", "ImmersalURPAsset_Renderer.asset");
                UniversalRendererData rendererData =
                    (UniversalRendererData)AssetDatabase.LoadAssetAtPath(rendererDataPath, typeof(UniversalRendererData));

                if (rendererData != null)
                {
                    // Create / load RenderPipelineAsset
                    UniversalRenderPipelineAsset rpa = UniversalRenderPipelineAsset.Create(rendererData);
                    
                    // Save to file
                    string rpaSavePath = Path.Combine("Assets", "ImmersalURPAsset.asset");
                    AssetDatabase.CreateAsset(rpa, rpaSavePath);
                    
                    // Configure
                    ConfigureRenderPipelineAsset(rpa);

                    // Set in settings
                    GraphicsSettings.defaultRenderPipeline = rpa;
                    QualitySettings.renderPipeline = rpa;
                    
                }
            }

            // Global Settings
            if (CopyFromPackage("Editor/ImmersalURPGlobalSettings.asset", Path.Combine(destinationPath, "ImmersalURPGlobalSettings.asset"), true))
            {
                string settingsPath = Path.Combine("Assets", "ImmersalURPGlobalSettings.asset");
                RenderPipelineGlobalSettings settings =
                    (RenderPipelineGlobalSettings)AssetDatabase.LoadAssetAtPath(settingsPath,
                        typeof(RenderPipelineGlobalSettings));
                
                if (settings != null)
                {
                    GraphicsSettings.RegisterRenderPipelineSettings<UniversalRenderPipeline>(settings);
                }
            }
        }

        private static void ConfigureRenderPipelineAsset(UniversalRenderPipelineAsset asset)
        {
            asset.supportsHDR = false;
            asset.cascadeBorder = 0.1f;
        }

        private static bool CopyFromPackage(string packageRelativePath, string destinationPath, bool refreshDatabase = true)
        {
            string sourcePath = Path.Combine("Packages/com.immersal.core/", packageRelativePath);
            string absolutePath = FixWindowsPath(Path.GetFullPath(sourcePath));
            destinationPath = FixWindowsPath(destinationPath);
            
            if (File.Exists(absolutePath))
            {
                if (File.Exists(destinationPath))
                {
                    ImmersalLogger.LogWarning(
                        $"Attempting to copy file from Immersal package but destination already exists: {destinationPath}");
                }
                else
                {
                    // Have to manually create directories or CopyFileOrDirectory will error on Windows
                    string dirName = Path.GetDirectoryName(destinationPath);
                    if (dirName == null)
                        return false;
                    
                    if (!Directory.Exists(dirName))
                        Directory.CreateDirectory(dirName);
                    
                    FileUtil.CopyFileOrDirectory(absolutePath, destinationPath);
                }
                if (refreshDatabase)
                    AssetDatabase.Refresh();
                return true;
            }

            ImmersalLogger.LogWarning($"Could not locate {packageRelativePath} in Immersal package.");
            return false;
        }

        // Unity API expects forward-slashes everywhere, but .NET methods produce paths with back-slashes on Windows
        private static string FixWindowsPath(string path)
        {
            return path.Replace("\\", "/");
        }

        private static bool CheckManifestExists()
        {
            return File.Exists(GetManifestPath());
        }
        
        private static bool CheckManifestContent()
        {
            if (CheckManifestExists())
            {
                AndroidManifest manifest = new AndroidManifest(GetManifestPath());
                bool internet = manifest.CheckPermission("android.permission.INTERNET");
                bool network = manifest.CheckPermission("android.permission.ACCESS_NETWORK_STATE");
                return internet && network;
            }
            return false;
        }
    
        private static string GetManifestPath()
        {
            return Path.Combine(Application.dataPath, "Plugins/Android/AndroidManifest.xml");
        }

        private static void CreateManifest()
        {
            CopyFromPackage("Editor/SampleAndroidManifest.xml", GetManifestPath());
        }

        private static void ConfigureManifest()
        {
            if (CheckManifestExists())
            {
                AndroidManifest manifest = new AndroidManifest(GetManifestPath());
                manifest.AddPermission("android.permission.INTERNET");
                manifest.AddPermission("android.permission.ACCESS_NETWORK_STATE");
                manifest.Save();
                AssetDatabase.Refresh();
            }
        }
    }
    
    internal class AndroidXmlDocument : XmlDocument
    {
        private string m_Path;
        protected XmlNamespaceManager nsMgr;
        public readonly string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";
        public AndroidXmlDocument(string path)
        {
            m_Path = path;
            using (var reader = new XmlTextReader(m_Path))
            {
                reader.Read();
                Load(reader);
            }
            nsMgr = new XmlNamespaceManager(NameTable);
            nsMgr.AddNamespace("android", AndroidXmlNamespace);
        }

        public string Save()
        {
            return SaveAs(m_Path);
        }

        public string SaveAs(string path)
        {
            using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                Save(writer);
            }
            return path;
        }
    }

    internal class AndroidManifest : AndroidXmlDocument
    {
        private readonly XmlElement ApplicationElement;

        public AndroidManifest(string path) : base(path)
        {
            ApplicationElement = SelectSingleNode("/manifest/application") as XmlElement;
        }

        private XmlAttribute CreateAndroidAttribute(string key, string value)
        {
            XmlAttribute attr = CreateAttribute("android", key, AndroidXmlNamespace);
            attr.Value = value;
            return attr;
        }

        private XmlElement CreatePermissionElement(string permissionName)
        {
            XmlElement elem = CreateElement("uses-permission");
            XmlAttribute attr = CreateAttribute("android", "name", AndroidXmlNamespace);
            attr.Value = permissionName;
            elem.Attributes.Append(attr);
            return elem;
        }

        internal XmlNode GetActivityWithLaunchIntent()
        {
            return SelectSingleNode("/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' and " +
                                    "intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
        }

        internal void SetApplicationTheme(string appTheme)
        {
            ApplicationElement.Attributes.Append(CreateAndroidAttribute("theme", appTheme));
        }

        internal void SetStartingActivityName(string activityName)
        {
            GetActivityWithLaunchIntent().Attributes.Append(CreateAndroidAttribute("name", activityName));
        }

        internal void AddPermission(string permissionName)
        {
            XmlElement ManifestElement = SelectSingleNode("/manifest") as XmlElement;
            XmlElement permissionElement = CreatePermissionElement(permissionName);
            ManifestElement?.AppendChild(permissionElement);
        }

        internal bool CheckPermission(string permissionName)
        {
            XmlElement ManifestElement = SelectSingleNode("/manifest") as XmlElement;
            XmlNodeList children = ManifestElement?.ChildNodes;
            if (children == null) return false;
            foreach (XmlNode child in children)
            {
                if (child.Name != "uses-permission") continue;
                if (child.Attributes == null) continue;
                XmlAttribute attr = child.Attributes?["android:name"];
                if (attr == null) continue;
                if (attr.Value == permissionName) return true;
            }
            return false;
        }
    }
#endif
}
