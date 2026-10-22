using System;
using System.IO;
using UnityEngine;

namespace UmaDesktop
{
    [Serializable]
    public sealed class DesktopSettings
    {
        public string DataPath = DesktopProduct.DefaultDataPath;
        public int SettingsVersion = 5;
        public string UiLanguage = "";
        public DesktopGroundConfig Ground = new DesktopGroundConfig();
        public string SelectionMode = "All";
        public int[] IncludeIds = new int[0], ExcludeIds = new int[0];
        public int MaxActors = 6;
        public float SpawnMinSeconds = 20, SpawnMaxSeconds = 35;
        public float FullHoldMinSeconds = 20, FullHoldMaxSeconds = 35;
        public float EmptyGapMinSeconds = 8, EmptyGapMaxSeconds = 15;
        public float InteractionProbability = 0.5f;
        public float InteractionCooldownSeconds = 25;
        public float GroundBottomPaddingPixels = 4;
        public bool GroundAtPhysicalScreenBottom = true;
        public float OutlineWidth = 0.45f;
        public int MsaaSamples = 4;
        public bool UseFxaa = true;
        public bool Avoid3DCollisions = false;
        public bool FlipReadbackY = false;
        public int InitialCharacterId = 0; // Legacy field; normal startup always draws randomly.
        public int[] CharacterIds = new int[0];
        public int WindowSize = 768;
        public int CharacterHeightPixels = 190;
        public int FrameRate = 30;
        public float ModelSizeScale = 1;
        public float CharacterScale = 2;
        public float HishiAkebonoScale = 3;
        public float WalkProbability = 0.75f;
        public float SpecialEndTrimSeconds = 1f;
        public float FadeSeconds = 0.5f;
        public float RenderScale = 1;
        public bool AlwaysOnTop = false;
        public bool ShowDiagnostics = false;
        public float SpeedFactorMin = 0.8f, SpeedFactorMax = 1.2f;
        public float SpecialClearancePixels = 20;
        public float WalkPixelsPerSecond = 65;
        public float RunPixelsPerSecond = 260;
        public float LocomotionMinSeconds = 10;
        public float LocomotionMaxSeconds = 24;
        public float CharacterMinSeconds = 45;
        public float CharacterMaxSeconds = 90;
        public float GroundLeftHeight = 0.84f;
        public float GroundRightHeight = 0.84f;
        public float FacingAngle = 65;
        public float CameraElevation = 10;
        public int RandomSeed = 0;

        public static DesktopSettings Load(string path)
        {
            var settings = new DesktopSettings();
            string stored=DesktopProfile.ReadSettings(path,ValidJson);
            if (stored!=null)
            {
                string text = stored;
                JsonUtility.FromJsonOverwrite(text, settings);
                if (!text.Contains("\"SettingsVersion\"") || settings.SettingsVersion < 3)
                {
                    settings.SettingsVersion = 3;
                    settings.SpecialEndTrimSeconds = 1;
                    if (Mathf.Approximately(settings.RunPixelsPerSecond, 140)) settings.RunPixelsPerSecond = 260;
                    if (settings.CharacterIds != null && settings.CharacterIds.Length > 0)
                    { settings.SelectionMode = "Include"; settings.IncludeIds = settings.CharacterIds; }
                    Save(path,settings);
                }
            }
            if(DesktopProfile.IsLegacyPlaceholder(settings.DataPath,Path.GetDirectoryName(Path.GetFullPath(path))))settings.DataPath=DesktopProduct.DefaultDataPath;
            if(settings.SettingsVersion<4) {settings.SettingsVersion=4;settings.InitialCharacterId=0;Save(path,settings);}
            if(string.IsNullOrEmpty(settings.UiLanguage)){settings.UiLanguage=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;settings.UiLanguage=settings.UiLanguage=="zh"||settings.UiLanguage=="ja"?settings.UiLanguage:"en";settings.SettingsVersion=5;Save(path,settings);}
            if(!Path.IsPathRooted(settings.DataPath))settings.DataPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),settings.DataPath);
            settings.Validate();
            Save(path,settings);
            return settings;
        }

        public static void Save(string path,DesktopSettings settings)
        {
            DesktopProfile.WriteSettings(path,JsonUtility.ToJson(settings,true));
        }
        private static bool ValidJson(string text)
        {try{return text.TrimStart().StartsWith("{") && text.Contains("\"DataPath\"") && JsonUtility.FromJson<DesktopSettings>(text)!=null;}catch{return false;}}
        public void Validate()
        {
            // Desktop-only placement is mandatory; old AlwaysOnTop settings are ignored.
            AlwaysOnTop=false;
            if(Ground==null || GroundMap.Validate(Ground)!=null)Ground=new DesktopGroundConfig();
            MaxActors = DesktopLimits.Clamp(MaxActors);
            if (SelectionMode != "Include" && SelectionMode != "Exclude") SelectionMode = "All";
            if (IncludeIds == null) IncludeIds = new int[0];
            if (ExcludeIds == null) ExcludeIds = new int[0];
            SpawnMinSeconds = Mathf.Max(1, SpawnMinSeconds); SpawnMaxSeconds = Mathf.Max(SpawnMinSeconds, SpawnMaxSeconds);
            FullHoldMinSeconds = Mathf.Max(1, FullHoldMinSeconds); FullHoldMaxSeconds = Mathf.Max(FullHoldMinSeconds, FullHoldMaxSeconds);
            EmptyGapMinSeconds = Mathf.Max(1, EmptyGapMinSeconds); EmptyGapMaxSeconds = Mathf.Max(EmptyGapMinSeconds, EmptyGapMaxSeconds);
            InteractionProbability = Mathf.Clamp01(InteractionProbability);
            InteractionCooldownSeconds = Mathf.Max(5, InteractionCooldownSeconds);
            GroundBottomPaddingPixels = Mathf.Clamp(GroundBottomPaddingPixels, 0, 100);
            OutlineWidth = Mathf.Clamp(OutlineWidth, 0, 2);
            MsaaSamples = MsaaSamples >= 4 ? 4 : MsaaSamples >= 2 ? 2 : 1;
            WindowSize = Mathf.Clamp(WindowSize, 256, 768);
            CharacterHeightPixels = Mathf.Clamp(CharacterHeightPixels, 64, WindowSize - 100);
            FrameRate = FrameRate==15||FrameRate==30||FrameRate==60||FrameRate==120?FrameRate:30;
            UiLanguage=UiLanguage=="ja"||UiLanguage=="en"?UiLanguage:"zh";
            ModelSizeScale = float.IsNaN(ModelSizeScale) || float.IsInfinity(ModelSizeScale) ? 1 : Mathf.Clamp(ModelSizeScale,0.5f,1.5f);
            CharacterScale = Mathf.Clamp(CharacterScale, 0.5f, 4);
            HishiAkebonoScale = Mathf.Clamp(HishiAkebonoScale, 0.5f, 4);
            WalkProbability = Mathf.Clamp01(WalkProbability);
            SpecialEndTrimSeconds = Mathf.Clamp(SpecialEndTrimSeconds, 0, 2);
            FadeSeconds = Mathf.Clamp(FadeSeconds, 0.1f, 2);
            RenderScale = Mathf.Clamp(RenderScale, 0.5f, 1);
            SpeedFactorMin=Mathf.Clamp(SpeedFactorMin,0.5f,1.5f);SpeedFactorMax=Mathf.Clamp(SpeedFactorMax,SpeedFactorMin,1.8f);
            SpecialClearancePixels=Mathf.Clamp(SpecialClearancePixels,8,100);
            WalkPixelsPerSecond = Mathf.Clamp(WalkPixelsPerSecond, 10, 400);
            RunPixelsPerSecond = Mathf.Clamp(RunPixelsPerSecond, 10, 600);
            LocomotionMinSeconds = Mathf.Clamp(LocomotionMinSeconds, 1, 120);
            LocomotionMaxSeconds = Mathf.Clamp(LocomotionMaxSeconds, LocomotionMinSeconds, 180);
            CharacterMinSeconds = Mathf.Clamp(CharacterMinSeconds, 5, 3600);
            CharacterMaxSeconds = Mathf.Clamp(CharacterMaxSeconds, CharacterMinSeconds, 7200);
            GroundLeftHeight = Mathf.Clamp(GroundLeftHeight, 0.35f, 0.95f);
            GroundRightHeight = Mathf.Clamp(GroundRightHeight, 0.35f, 0.95f);
            FacingAngle = Mathf.Clamp(FacingAngle, 0, 90);
            CameraElevation = Mathf.Clamp(CameraElevation, 0, 35);
            if (CharacterIds == null) CharacterIds = new int[0];
            DataPath = Path.GetFullPath(DataPath);
        }
    }
}
