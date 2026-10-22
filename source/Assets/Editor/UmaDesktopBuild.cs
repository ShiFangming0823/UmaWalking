using System;
using System.IO;
using System.Linq;
using UmaDesktop;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class UmaDesktopBuild
{
    private const string DesktopScene = "Assets/Scenes/DesktopPrototype.unity";

    [MenuItem("Uma Desktop/Create or refresh prototype scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene before generating the prototype scene.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Version2.unity", OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                // Keep the serialized viewer references and Awake singletons,
                // but skip list-building, network translations and UI updates.
                foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script != null) script.enabled = script is UmaViewerBuilder || script is UmaAssetManager || script is UmaViewerGlobalShader;
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) { camera.enabled = false; camera.tag = "Untagged"; }
                foreach (Light light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
            }
            var cameraObject = new GameObject("DesktopCamera");
            var desktopCamera = cameraObject.AddComponent<Camera>();
            desktopCamera.tag = "MainCamera";
            desktopCamera.clearFlags = CameraClearFlags.SolidColor;
            desktopCamera.backgroundColor = Color.magenta;
            desktopCamera.allowHDR = false;
            desktopCamera.allowMSAA = false;
            desktopCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UIHandle"));
            var cameraData = desktopCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;
            var lightObject = new GameObject("DesktopKeyLight");
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.1f;
            keyLight.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(40, -25, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.75f);
            RenderSettings.fog = false;
            new GameObject("UmaDesktopPrototype").AddComponent<DesktopPrototype>().DesktopCamera = desktopCamera;
            EditorSceneManager.SaveScene(scene, DesktopScene);
            Debug.Log("Desktop scene generated: " + DesktopScene);
        }
        finally
        {
            // A fresh batch-mode editor may start without any loaded scene.
            if (previous.Any(s => s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    [Serializable] private sealed class SplashBuildReport
    {
        public bool Passed,blur,show,UnityLogo;
        public string assetPath,sha256,animation;
        public int width,height;
    }
    private static void ConfigureSplash(string executable)
    {
        const string path="Assets/DesktopSplash/Background.jpg";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new FileNotFoundException("Desktop splash image is missing",path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(textureSettings);importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var background=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(background==null)throw new InvalidDataException("Failed to import desktop splash sprite");
        PlayerSettings.SplashScreen.background=background;PlayerSettings.SplashScreen.backgroundPortrait=background;
        PlayerSettings.SplashScreen.blurBackgroundImage=false;
        PlayerSettings.SplashScreen.animationMode=PlayerSettings.SplashScreen.AnimationMode.Static;
        string hash;
        using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(path))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        var report=new SplashBuildReport {Passed=PlayerSettings.SplashScreen.background==background&&!PlayerSettings.SplashScreen.blurBackgroundImage,assetPath=path,sha256=hash,width=background.texture.width,height=background.texture.height,blur=PlayerSettings.SplashScreen.blurBackgroundImage,show=PlayerSettings.SplashScreen.show,UnityLogo=PlayerSettings.SplashScreen.showUnityLogo,animation=PlayerSettings.SplashScreen.animationMode.ToString()};
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(executable),"splash-build-report.json"),JsonUtility.ToJson(report,true));
    }

    [MenuItem("Uma Desktop/Build Windows prototype")]
    public static void Build()
    {
        CreateScene();
        string executable = Environment.GetEnvironmentVariable("UMA_DESKTOP_BUILD");
        if (string.IsNullOrEmpty(executable)) executable = Path.GetFullPath("../Builds/UmaDesktop/UmaDesktop.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable));

        string oldProduct = PlayerSettings.productName;
        string oldVersion=PlayerSettings.bundleVersion;
        bool oldRun = PlayerSettings.runInBackground, oldResize = PlayerSettings.resizableWindow;
        bool oldFlip = PlayerSettings.useFlipModelSwapchain, oldSingle = PlayerSettings.forceSingleInstance;
        int oldWidth = PlayerSettings.defaultScreenWidth, oldHeight = PlayerSettings.defaultScreenHeight;
        FullScreenMode oldMode = PlayerSettings.fullScreenMode;
        var oldSplash=PlayerSettings.SplashScreen.background;
        var oldPortraitSplash=PlayerSettings.SplashScreen.backgroundPortrait;
        bool oldSplashBlur=PlayerSettings.SplashScreen.blurBackgroundImage;
        var oldSplashAnimation=PlayerSettings.SplashScreen.animationMode;
        var oldBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
        var oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
        bool oldDefaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
        try
        {
            ConfigureSplash(executable);
            PlayerSettings.productName = "UmaDesktopPrototype";
            PlayerSettings.bundleVersion = DesktopProduct.Version;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.defaultScreenWidth = PlayerSettings.defaultScreenHeight = 768;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.useFlipModelSwapchain = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { DesktopScene }, locationPathName = executable,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Desktop build failed: " + report.summary.result);
            string settingsFile = Path.Combine(Path.GetDirectoryName(executable), "DesktopSettings.json");
            if (!File.Exists(settingsFile)) File.WriteAllText(settingsFile, JsonUtility.ToJson(new DesktopSettings(), true));
            Debug.Log("UMA_DESKTOP_BUILD_SUCCEEDED " + executable);
        }
        finally
        {
            PlayerSettings.SplashScreen.background=oldSplash;
            PlayerSettings.SplashScreen.backgroundPortrait=oldPortraitSplash;
            PlayerSettings.SplashScreen.blurBackgroundImage=oldSplashBlur;
            PlayerSettings.SplashScreen.animationMode=oldSplashAnimation;
            PlayerSettings.productName = oldProduct;
            PlayerSettings.bundleVersion = oldVersion;
            PlayerSettings.runInBackground = oldRun;
            PlayerSettings.resizableWindow = oldResize;
            PlayerSettings.forceSingleInstance = oldSingle;
            PlayerSettings.defaultScreenWidth = oldWidth;
            PlayerSettings.defaultScreenHeight = oldHeight;
            PlayerSettings.fullScreenMode = oldMode;
            PlayerSettings.useFlipModelSwapchain = oldFlip;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, oldBackend);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, oldApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, oldDefaultApis);
        }
    }
}
