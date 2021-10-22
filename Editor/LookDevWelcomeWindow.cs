﻿using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

namespace LookDev.Editor
{
    public class LookDevWelcomeWindow : EditorWindow
    {
        static readonly string _installHdrpAssetsButtonName = "InstallHdrpAssetsButton";
        static readonly string _installUrpAssetsButtonName = "InstallUrpAssetsButton";

        static readonly string _hdrpPackageAddress = "com.unity.render-pipelines.high-definition";
        static readonly string _urpPackageAddress = "com.unity.render-pipelines.universal";

        const string PathToHDRPAssets =
            "https://github.com/Unity-Technologies/lookdev-studio/releases/download/r537Assets/LookDevHDRPAssets.unitypackage";

        const string PackageTempDirectory = "Temp";

        Button SetupButton => rootVisualElement.Query<Button>("SetupButton").First();
        Button OpenButton => rootVisualElement.Query<Button>("OpenLookDevButton").First();

        public static event Action PipelineExtensionSetup;

        [MenuItem("LookDev Studio/Welcome")]
        public static void ShowWindow()
        {
            GetWindow<LookDevWelcomeWindow>(typeof(SceneView));
        }

        [MenuItem("LookDev Studio DEBUG/Initialized")]
        public static void ToggleInitialized()
        {
            var isInitialized = LookDevPreferences.instance.IsRenderPipelineInitialized;

            if (!isInitialized)
            {
                LookDevPreferences.instance.IsRenderPipelineInitialized = true;
                LookDevPreferences.instance.AreAssetsInstalled = true;
                var window = GetWindow<LookDevWelcomeWindow>();
                window.OpenButton.SetEnabled(true);
                window.SetupButton.SetEnabled(true);
            }
            else
            {
                LookDevPreferences.instance.IsRenderPipelineInitialized = false;
                LookDevPreferences.instance.AreAssetsInstalled = false;
                var window = GetWindow<LookDevWelcomeWindow>();
                window.OpenButton.SetEnabled(false);
                window.SetupButton.SetEnabled(false);
            }
        }

        [MenuItem("LookDev Studio DEBUG/Initialized", true)]
        public static bool ValidateInitialized()
        {
            var isInitialized = LookDevPreferences.instance.IsRenderPipelineInitialized;
            Menu.SetChecked("LookDev Studio DEBUG/Initialized", isInitialized);
            return true;
        }

        [MenuItem("LookDev Studio DEBUG/AutoLoad")]
        public static void ToggleAutoLoad()
        {
            var autoloadEnabled = !File.Exists(LookDevStudioEditor.PathToLookDevDisableAutoLaunchFile);
            if (autoloadEnabled)
                File.Create(LookDevStudioEditor.PathToLookDevDisableAutoLaunchFile);
            else
                File.Delete(LookDevStudioEditor.PathToLookDevDisableAutoLaunchFile);
        }

        [MenuItem("LookDev Studio DEBUG/AutoLoad", true)]
        public static bool ValidateAutoLoad()
        {
            var autoloadEnabled = !File.Exists(LookDevStudioEditor.PathToLookDevDisableAutoLaunchFile);
            Menu.SetChecked("LookDev Studio DEBUG/AutoLoad", autoloadEnabled);
            return true;
        }

        [MenuItem("LookDev Studio DEBUG/Create Asset Package")]
        public static void CreateAssetPackage()
        {
            AssetDatabase.ExportPackage("Assets/LookDev", "Extensions/ExportedAssetPackage.unitypackage",
                ExportPackageOptions.Recurse);
        }

        void CreateGUI()
        {
            var uxmlTemplate =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    $"Packages/com.unity.lookdevstudio/UI/LookDevWelcome.uxml");

            var ui = uxmlTemplate.CloneTree();
            rootVisualElement.Add(ui);

            var installHdrpAssetsButton = rootVisualElement.Query<Button>(_installHdrpAssetsButtonName).First();
            installHdrpAssetsButton.clicked += () => { InstallAssets(this, _hdrpPackageAddress, "HDRP"); };

            var installUrpAssetsButton = rootVisualElement.Query<Button>(_installUrpAssetsButtonName).First();
            installUrpAssetsButton.clicked += () => { InstallAssets(this, _urpPackageAddress, "URP"); };

            var setupButton = SetupButton;
            setupButton.clicked += () => { Setup(this); };
            setupButton.SetEnabled(LookDevPreferences.instance.AreAssetsInstalled);

            var openLookDevButton = OpenButton;
            openLookDevButton.clicked += OpenLookDev;
            openLookDevButton.SetEnabled(LookDevPreferences.instance.IsRenderPipelineInitialized);
        }

        static async Task InstallPackage(string address)
        {
            var listRequest = Client.List();
            while (!listRequest.IsCompleted)
            {
                await Task.Delay(1000);
            }

            bool isPackageInstalled = false;
            var packageList = listRequest.Result;
            foreach (var packageInfo in packageList)
            {
                if (packageInfo.name == address)
                {
                    isPackageInstalled = true;
                    break;
                }
            }

            if (!isPackageInstalled)
            {
                var addRequest = Client.Add(address);
                while (!addRequest.IsCompleted)
                {
                    Debug.Log($"Installing Package {address}...");
                    await Task.Delay(1000);
                }
            }
        }

        static async void InstallAssets(LookDevWelcomeWindow window, string packageAddress,
            string expectedSourceFolderName)
        {
            //EditorApplication.LockReloadAssemblies();
            await InstallPackage(packageAddress);
            //EditorApplication.UnlockReloadAssemblies();

            string sourceFolderPath = EditorUtility.OpenFolderPanel("Select Folder Source", "", "");
            string selectedFolderName = new DirectoryInfo(sourceFolderPath).Name;
            if (selectedFolderName != expectedSourceFolderName)
            {
                Debug.LogError($"Error: Wrong folder selected. Expecting a folder called {expectedSourceFolderName}");
                return;
            }

            SymlinkUtility.Symlink(SymlinkUtility.SymlinkType.Junction, sourceFolderPath, "Assets", "LookDev");
            LookDevPreferences.instance.AreAssetsInstalled = true;
            window.SetupButton.SetEnabled(LookDevPreferences.instance.AreAssetsInstalled);

            /*
            using (var uwr = new UnityWebRequest($"{PathToHDRPAssets}", UnityWebRequest.kHttpVerbGET))
            {
                EditorApplication.LockReloadAssemblies();

                uwr.downloadHandler = new DownloadHandlerBuffer();
                Debug.Log($"About to download: {uwr.url}");
                var asyncRequest = uwr.SendWebRequest();
                while (!asyncRequest.isDone)
                {
                    Debug.Log($"Progress: {asyncRequest.progress * 100}%");
                    await Task.Delay(1000);
                }

                if (uwr.downloadHandler.isDone && string.IsNullOrEmpty(uwr.downloadHandler.error))
                {
                    Debug.Log("Download Complete");

                    if (!Directory.Exists(PackageTempDirectory))
                        Directory.CreateDirectory(PackageTempDirectory);

                    var packagePath = $"{PackageTempDirectory}/LookDevStudioHDAssets.unitypackage";
                    try
                    {
                        File.WriteAllBytes(packagePath, uwr.downloadHandler.data);
                        Debug.Log("Importing Package");
                        AssetDatabase.ImportPackage(packagePath, false);
                        Debug.Log("Importing complete");
                        LookDevPreferences.instance.AreAssetsInstalled = true;
                        window.SetupButton.SetEnabled(LookDevPreferences.instance.AreAssetsInstalled);
                        EditorApplication.UnlockReloadAssemblies();
                    }
                    catch
                    {
                        Debug.LogError("Failed to import package");
                        if (File.Exists(packagePath))
                            File.Delete(packagePath);
                    }
                }
                else
                {
                    Debug.LogError($"Download stopped with error: {uwr.downloadHandler.error}");
                }
            }
            */
        }

        static void Setup(LookDevWelcomeWindow window)
        {
            PipelineExtensionSetup?.Invoke();

            LookDevPreferences.instance.IsRenderPipelineInitialized = true;
            window.OpenButton.SetEnabled(LookDevPreferences.instance.IsRenderPipelineInitialized);
        }

        static void OpenLookDev()
        {
            LookDevStudioEditor.EnableLookDev();
        }
    }
}