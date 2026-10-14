using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace FinkForge.Editor
{
    internal static class FinkForgeTools
    {
        private const string UniTaskGitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10";
        private const string InputSystemPackageId = "com.unity.inputsystem";
        private const string ConfigTypeName = "FinkForge.Config.FinkForgeConfig, FinkForge.Runtime";
        private const string ConfigAssetPath = "Assets/FinkForge/Resources/FinkForgeConfig.asset";
        private static AddRequest installRequest;
        private static bool installInputSystemAfterUniTask;

        [MenuItem("Tools/Fink Forge/Create Config")]
        private static void CreateConfig()
        {
            var existing = FindConfigAsset();
            if (existing != null)
            {
                SelectConfig(existing);
                Debug.Log("[Fink Forge] Config already exists; selected the existing asset.");
                return;
            }

            EnsureFolder("Assets/FinkForge");
            EnsureFolder("Assets/FinkForge/Resources");
            var configType = Type.GetType(ConfigTypeName);
            if (configType == null)
            {
                Debug.LogError("[Fink Forge] Runtime config type is unavailable. Recompile the package and try again.");
                return;
            }

            var asset = ScriptableObject.CreateInstance(configType);
            AssetDatabase.CreateAsset(asset, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            SelectConfig(asset);
            Debug.Log("[Fink Forge] Created config at Assets/FinkForge/Resources/FinkForgeConfig.asset.");
        }

        [MenuItem("Tools/Fink Forge/Select Config")]
        private static void SelectConfigMenu()
        {
            var config = FindConfigAsset();
            if (config == null)
            {
                Debug.LogWarning("[Fink Forge] Config not found. Use Tools > Fink Forge > Create Config.");
                return;
            }

            SelectConfig(config);
        }

        [MenuItem("Tools/Fink Forge/Install Requirements")]
        private static void InstallRequirements()
        {
            if (!HasUniTask())
            {
                installInputSystemAfterUniTask = !HasInputSystem();
                installRequest = Client.Add(UniTaskGitUrl);
                EditorApplication.update -= CompleteInstall;
                EditorApplication.update += CompleteInstall;
                Debug.Log($"[Fink Forge] Installing UniTask from {UniTaskGitUrl}");
                return;
            }

            if (!HasInputSystem())
            {
                installRequest = Client.Add(InputSystemPackageId);
                EditorApplication.update -= CompleteInstall;
                EditorApplication.update += CompleteInstall;
                Debug.Log($"[Fink Forge] Installing Unity Input System ({InputSystemPackageId}).");
                return;
            }

            Debug.Log("[Fink Forge] UniTask and Unity Input System are already installed.");
        }

        [MenuItem("Tools/Fink Forge/Validate Project")]
        private static void ValidateProject()
        {
            var unitask = HasUniTask();
            var inputSystem = HasInputSystem();
            var runtime = Type.GetType("FinkForge.Fink, FinkForge.Runtime") != null;
            var editor = Type.GetType("FinkForge.Editor.FinkForgeTools, FinkForge.Editor") != null;
            var package = AssetDatabase.IsValidFolder("Packages/com.finkgames.finkforge");
            var tests = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Packages/com.finkgames.finkforge/Tests/Editor/FinkForge.Tests.EditMode.asmdef") != null;
            var config = FindConfigAsset();
            var gameName = ReadConfigString(config, "GameName");
            var trustedKeys = Type.GetType("FinkForge.Environment.LaunchSecurityService, FinkForge.Runtime") != null;
            Debug.Log($"[Fink Forge] Validation: UniTask={(unitask ? "OK" : "MISSING")}, InputSystem={(inputSystem ? "OK" : "MISSING")}, Runtime={(runtime ? "OK" : "MISSING")}, Editor={(editor ? "OK" : "MISSING")}, Tests={(tests ? "OK" : "MISSING")}, Package={(package ? "OK" : "MISSING")}, Config={(config != null ? "OK" : "MISSING")}, GameName={(string.IsNullOrWhiteSpace(gameName) ? "MISSING" : "OK")}, launchTrustedKeys={(trustedKeys ? "BOUNDARY ONLY" : "MISSING")}");
            if (!unitask)
                Debug.LogWarning("Fink Forge requires UniTask. Use Tools > Fink Forge > Install Requirements.");
            if (!inputSystem)
                Debug.LogWarning("[Fink Forge] Fink Forge requires Unity Input System. Use Tools > Fink Forge > Install Requirements.");
            if (!runtime || !editor || !tests || !package)
                Debug.LogWarning("[Fink Forge] Project validation found one or more package issues.");
            if (config == null)
                Debug.LogWarning("[Fink Forge] Config not found. Use Tools > Fink Forge > Create Config.");
            if (string.IsNullOrWhiteSpace(gameName))
                Debug.LogError("[Fink Forge] Game Name is missing.");
            if (trustedKeys)
                Debug.Log("[Fink Forge] Launch JWT validation is enabled. The runtime public key is loaded from the project public.pem.");
        }

        private static bool HasUniTask() => Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") != null;

        private static bool HasInputSystem() => Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") != null;

        private static UnityEngine.Object FindConfigAsset()
        {
            var type = Type.GetType(ConfigTypeName);
            if (type == null) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:FinkForgeConfig"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset != null) return asset;
            }

            return null;
        }

        private static string ReadConfigString(UnityEngine.Object config, string propertyName)
        {
            if (config == null) return string.Empty;
            var property = config.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return property?.GetValue(config) as string ?? string.Empty;
        }

        private static void SelectConfig(UnityEngine.Object config)
        {
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            var folder = path.Substring(slash + 1);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static void CompleteInstall()
        {
            if (installRequest == null || !installRequest.IsCompleted) return;
            EditorApplication.update -= CompleteInstall;
            if (installRequest.Status == StatusCode.Success)
            {
                Debug.Log("[Fink Forge] Requirement installed successfully. Unity may recompile the package assemblies.");
                if (installInputSystemAfterUniTask)
                {
                    installInputSystemAfterUniTask = false;
                    installRequest = Client.Add(InputSystemPackageId);
                    EditorApplication.update += CompleteInstall;
                    return;
                }
            }
            else
                Debug.LogError($"[Fink Forge] UniTask installation failed: {installRequest.Error?.message}");
            installRequest = null;
        }
    }
}
