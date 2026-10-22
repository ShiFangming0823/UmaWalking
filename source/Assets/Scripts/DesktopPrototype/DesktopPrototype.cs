using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace UmaDesktop
{
    [DefaultExecutionOrder(-20000)]
    public sealed partial class DesktopPrototype : MonoBehaviour
    {
        public Camera DesktopCamera;
        private DesktopSettings settings, persisted;
        private MiniMotionCatalog catalog;
        private System.Random random;
        private PopulationClock population;
        private readonly List<DesktopActor> actors = new List<DesktopActor>();
        private readonly List<MiniMotionCatalog.Character> drawBag = new List<MiniMotionCatalog.Character>();
        private readonly List<PairMotion> pairMotions = new List<PairMotion>();
        private readonly List<PairSession> pairs = new List<PairSession>();
        private string root, logDirectory, commandDirectory, settingsPath, error, message = "正在读取游戏资源…";
        private float now, frameDelta, nextStatus, nextCommands, exitSeconds;
        private int nextToken, nextOrdinal, loads, departures, clickedSpecials, specials, trimmed, gaits, interactions, encounterRolls, drawsAccepted;
        private bool busy, ready, paused, testMode, pairAudit, visualAudit, controllerDisabled, quitting;
        private bool desktopPaused, visibilityKnown, wasEffectivelyPaused, testIgnoreOcclusion;
        private float nextVisibility;
        private bool EffectivePaused { get { return paused || desktopPaused || groundEditing; } }
        private float nextPairAudit;
        private int auditPairIndex;
        private Process controller;
        private int? preferredCharacter;
        private float? pendingModelSize;

        [Serializable] private sealed class ViewerConfiguration
        {
            public string MainPath; public int Language=1, Region=1, WorkMode=0, TargetFrameRate=30;
            public bool DownloadMissingResources=false, RegionDetectionPassed=true;
        }
        [Serializable] private sealed class Command
        {
            public string action, mode, costumeId, dataPath, language, requestId;
            public int frameRate,maxActors;public bool antiAliasing,avoid3DCollisions;
            public float modelSizeScale,groundU,groundV,poseYaw;
            public DesktopGroundConfig ground;
            public int token, characterId,groundIndex;
            public GroundPoint navigationTarget;public float navigationSpeed;
            public int[] includeIds, excludeIds;
        }
        [Serializable] private sealed class CharacterExport
        {
            public int id; public string name; public CostumeExport[] costumes;
        }
        [Serializable] private sealed class CostumeExport { public string id, name; public int category; }
        [Serializable] private sealed class CatalogExport { public CharacterExport[] characters; }
        [Serializable] private sealed class ActorState
        {
            public bool manualEntry;
            public int token, ordinal, characterId, costumeCategory, direction, presentedFrames, facialEvaluations, specialDeferrals, automaticSpecials;
            public bool automaticSpecial, specialPending, pointerHeld, dragging, motionHeld;public float animationTime;
            public string avoidanceState;public int avoidanceFrames,avoidanceReversals;public float avoidanceRadius;
            public int boundaryReplans;public float groundTargetX,groundTargetY;
            public int groundIndex, groundStage,poseVersion;public float groundU,groundV,groundDepthScale,portalOpacity,groundFitScale,canvasFootViewport,calibratedFloorY,soleClearancePixels,footprintRadiusX,footprintRadiusY;public GroundPoint[] footContacts,actualShoePixels;
            public float walkSpeedFactor, runSpeedFactor, clearanceHalfWidth, clearanceX, clearanceOffset, specialRetryDistance, movementFraction;
            public string displayName, name, costumeId, costumeName, phase, state, clip, expression, expressionAction;
            public int expressionSelections;public bool expressionOverride, expressionMatchesPreset;public int[] expressionIndices;
            public float screenX, groundY, height, speed, playbackRate, opacity, specialElapsed, specialDuration, specialCutoff;
            public long nativeWindow, desktopParent;
            public bool desktopAttached, windowVisible;
        }
        [Serializable] private sealed class Snapshot
        {
            public string activeDataPath, configuredDataPath, language, optionsAck, gpu, graphicsApi, operatingSystem, applicationVersion;
            public int frameRate,maxActors,activeCount,retiringCount,overLimitCount;public bool useFxaa,avoid3DCollisions;
            public bool includeVariants, occupancyValid, costumesUnique;
            public int schemaVersion=8, count, targetCount, charactersLoaded, departures, clickSpecials, specialCount, trimmedSpecials, gaitCount, interactions, encounterRolls, interactionAccepted;
            public string utc, error, message, mode, populationState;
            public int[] includeIds, excludeIds;
            public bool paused, manualPaused, desktopPaused, desktopCovered, desktopAvailable, loading, namesUnique, controllerRunning, testIgnoresOcclusion, mouseInputInstalled, windowManagementActive, switchEventHookInstalled;
            public long desktopHost; public int coveringWindows;
            public float fps, nextEventIn, simulationSeconds, groundBottomPadding, specialTrim, modelSizeScale;
            public PendingEntryExport[] pendingEntries;
            public int processId;
            public DesktopGroundConfig ground;public bool groundEditing;
            public int msaaSamples; public float outlineWidth;
            public long managedBytes;
            public ActorState[] actors;
        }
        private sealed class PairMotion { public string Name; public UmaDatabaseEntry A, B; }
        private sealed class PairSession { public DesktopActor A, B; public float Until; }

        private void Awake()
        {
            try
            {
                root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                settingsPath = Path.Combine(root, "DesktopSettings.json");
                persisted = DesktopSettings.Load(settingsPath);
                settings = JsonUtility.FromJson<DesktopSettings>(JsonUtility.ToJson(persisted));
                string[] args = Environment.GetCommandLineArgs();
                testMode = args.Contains("--desktop-test-mode") || args.Contains("--desktop-smoke-seconds") || args.Contains("--desktop-expression-audit") || args.Contains("--desktop-pair-spacing-audit");
                testIgnoreOcclusion=testMode && args.Contains("--desktop-test-ignore-occlusion");
                expressionAudit=args.Contains("--desktop-expression-audit");
                pairSpacingAudit=args.Contains("--desktop-pair-spacing-audit");
                pairAudit = args.Contains("--desktop-pair-audit");
                visualAudit = args.Contains("--desktop-visual-audit");
                controllerDisabled = pairSpacingAudit || expressionAudit || visualAudit || args.Contains("--desktop-no-controller");
                exitSeconds = FloatArgument(args, "--desktop-exit-seconds", 0);
                if (args.Contains("--desktop-smoke-seconds")) exitSeconds = FloatArgument(args, "--desktop-smoke-seconds", 120);
                settings.FrameRate = Mathf.Clamp((int)FloatArgument(args, "--desktop-fps", settings.FrameRate), 15, 120);
                settings.RenderScale = Mathf.Clamp(FloatArgument(args, "--desktop-render-scale", settings.RenderScale), 0.5f, 1);
                settings.MaxActors = (int)FloatArgument(args, "--desktop-max-actors", settings.MaxActors);
                settings.MsaaSamples = (int)FloatArgument(args, "--desktop-msaa", settings.MsaaSamples);
                if (testMode)
                {
                    settings.SpawnMinSeconds=1; settings.SpawnMaxSeconds=1.4f;
                    settings.FullHoldMinSeconds=12; settings.FullHoldMaxSeconds=13;
                    settings.EmptyGapMinSeconds=1.5f; settings.EmptyGapMaxSeconds=2;
                    settings.LocomotionMinSeconds=1.5f; settings.LocomotionMaxSeconds=2.5f;
                    settings.SelectionMode="All"; settings.RandomSeed=581;
                    if(args.Contains("--desktop-test-stable-population"))settings.FullHoldMinSeconds=settings.FullHoldMaxSeconds=600;
                    if(args.Contains("--desktop-test-long-gait")){settings.LocomotionMinSeconds=settings.LocomotionMaxSeconds=600;settings.CharacterMinSeconds=settings.CharacterMaxSeconds=600;}
                }
                settings.Validate();
                logDirectory=Path.Combine(root,"DesktopLogs"); commandDirectory=Path.Combine(root,"DesktopCommands");
                Directory.CreateDirectory(logDirectory); Directory.CreateDirectory(commandDirectory);WriteLaunchInfo();
                // Commands belong to one live run; do not replay an old exit/filter command.
                foreach (string file in Directory.GetFiles(commandDirectory,"*.json")) File.Delete(file);
                string configDir=Path.Combine(Application.persistentDataPath,"DesktopPrototype"); Directory.CreateDirectory(configDir);
                string configPath=Path.Combine(configDir,"ViewerConfig.json");
                File.WriteAllText(configPath,JsonUtility.ToJson(new ViewerConfiguration { MainPath=settings.DataPath,TargetFrameRate=settings.FrameRate },true));
                Environment.SetEnvironmentVariable("UMA_DESKTOP_CONFIG",configPath);
                Application.runInBackground=true; Application.targetFrameRate=settings.FrameRate; QualitySettings.vSyncCount=0; AudioListener.volume=0;
                if (!File.Exists(Path.Combine(settings.DataPath,"meta")) || !File.Exists(Path.Combine(settings.DataPath,"master","master.mdb")))
                    throw new DirectoryNotFoundException("DataPath must contain meta, master/master.mdb and dat.");
            }
            catch(Exception ex) { Fail(ex); }
        }
        private static float FloatArgument(string[] args,string key,float fallback)
        {
            int i=Array.IndexOf(args,key);
            return i>=0 && i+1<args.Length && float.TryParse(args[i+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float value) ? value : fallback;
        }
        private IEnumerator Start()
        {
            StartController();
            if(error!=null) {WindowsOverlay.HideUnityWindow();yield break;}
            try
            {
                if(UmaViewerMain.Instance==null || UmaViewerMain.Instance.AbList==null) throw new InvalidOperationException("Game resource index failed to initialize.");
                GraphicsSettings.renderPipelineAsset=UmaViewerMain.Instance.DefaultRenderPipeline;
                QualitySettings.renderPipeline=UmaViewerMain.Instance.DefaultRenderPipeline;
                var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if(pipeline!=null) { pipeline.renderScale=1; pipeline.msaaSampleCount=settings.MsaaSamples; pipeline.supportsHDR=false; }
                foreach(var globals in FindObjectsOfType<UmaViewerGlobalShader>())
                { globals._GlobalOutlineWidth=settings.OutlineWidth; globals._GlobalCameraFov=0; globals._GlobalOutlineOffset=0; globals.SendMessage("setGlobal"); globals.enabled=false; }
                random=settings.RandomSeed==0 ? new System.Random() : new System.Random(settings.RandomSeed);
                catalog=new MiniMotionCatalog(settings);RefreshIncludeVariantPolicy();
                population=new PopulationClock(random,settings.SpawnMinSeconds,settings.SpawnMaxSeconds,settings.FullHoldMinSeconds,settings.FullHoldMaxSeconds,settings.EmptyGapMinSeconds,settings.EmptyGapMaxSeconds);
                var shaderEntry=UmaViewerMain.Instance.AbList["shader"];
                var shaders=UmaAssetManager.LoadAssetBundle(shaderEntry,true);
                UmaViewerBuilder.Instance.ShaderList=shaders.LoadAllAssets<Shader>().ToList(); Gallop.ShaderManager.InitManager();
                DesktopCamera.enabled=false;
                foreach(string kind in new[]{"01","02","03"})
                {
                    string prefix="3d/motion/mini/set/body/type"+kind+"/anm_min_set_type"+kind+"_004_01_";
                    var a=catalog.Find(prefix+"a"); var b=catalog.Find(prefix+"b");
                    if(a!=null && b!=null) pairMotions.Add(new PairMotion { Name="set_type"+kind+"_004_01", A=a,B=b });
                }
                File.WriteAllText(Path.Combine(logDirectory,"costume-catalog.json"),JsonUtility.ToJson(new CatalogExport { characters=catalog.Characters.Select(c=>new CharacterExport {
                    id=c.Entry.Id,name=c.Entry.Name,costumes=c.Costumes.Select(d=>new CostumeExport {id=d.Id,name=d.Name,category=d.Category}).ToArray() }).ToArray() },true));
                Record("Catalog V3: "+catalog.Characters.Count+" characters, "+pairMotions.Count+" paired A/B sets; MSAA="+settings.MsaaSamples);
                try{ExportAssetPlan();}catch(Exception ex){Record("Extraction plan unavailable: "+ex.Message);}
            }
            catch(Exception ex) { Fail(ex); }
            if(error!=null) {WindowsOverlay.HideUnityWindow();yield break;}
            yield return null; yield return null;
            WindowsOverlay.HideUnityWindow(); ready=true;
            RefreshDesktopVisibility(true);
            if(pairSpacingAudit) {paused=true;yield return RunPairSpacingAudit();yield break;}
            if(expressionAudit) {paused=true;yield return RunExpressionAudit();yield break;}
            if(visualAudit) { paused=true; yield return RunVisualAudit(); yield break; }
            if(pairAudit)
            {
                yield return Spawn(catalog.Characters.First(c=>c.Entry.Id==1001),"00");
                yield return Spawn(catalog.Characters.First(c=>c.Entry.Id==1002),"00");
                ArrangeAuditPair(); nextPairAudit=now+1;
            }
            else yield return Spawn();
            WriteStatus();
        }
        [Serializable] private sealed class VisualAuditSample
        { public int characterId; public string costumeId, clip, key; public float time, yaw; }
        [Serializable] private sealed class VisualAuditReport
        { public bool Passed; public string error; public VisualAuditSample[] samples; }
        private IEnumerator RunVisualAudit()
        {
            string directory=Path.Combine(logDirectory,"VisualAudit");Directory.CreateDirectory(directory);
            var samples=new List<VisualAuditSample>();
            int[] ids={1071,1058,1001,1028};string[] costumes={"00","40","00","0016_01_00"};
            for(int i=0;i<ids.Length;i++)
            {
                var character=catalog.Characters.First(c=>c.Entry.Id==ids[i]);
                yield return Spawn(character,costumes[i]);
                var actor=actors.FirstOrDefault(a=>a.Character.Entry.Id==ids[i]);
                if(actor==null) {Fail(new InvalidOperationException("Visual audit character load failed: "+ids[i]));break;}
                actor.X=actor.Overlay.ScreenArea.center.x;actor.Phase=DesktopPhase.Active;
                var clips=catalog.Walks.Take(1).Concat(catalog.Runs.Take(2)).Concat(catalog.CostumeSpecials(character,actor.Costume).Take(3)).ToArray();
                int clipIndex=0;
                foreach(var entry in clips)
                {
                    float duration=actor.Player.GetDuration(entry);
                    foreach(float fraction in new[] {0.15f,0.5f,0.85f})
                        foreach(float yaw in new[] {0f,65f})
                        {
                            string key=ids[i]+"-"+costumes[i]+"-"+(clipIndex++)+"-"+((int)yaw);
                            try
                            {
                                actor.CaptureAuditPose(entry,duration*fraction,yaw,directory,key);
                                samples.Add(new VisualAuditSample {characterId=ids[i],costumeId=costumes[i],clip=entry.Name,key=key,time=duration*fraction,yaw=yaw});
                            }
                            catch(Exception ex) {Fail(ex);}
                            yield return null;
                            if(error!=null) break;
                        }
                    if(error!=null) break;
                }
                Remove(actor);yield return null;
                if(error!=null) break;
            }
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(new VisualAuditReport {Passed=error==null,error=error,samples=samples.ToArray()},true));
            Application.Quit();
        }
        private void StartController()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if(controllerDisabled) return;
            string executable=Path.Combine(root,"UmaDesktopController.exe");
            if(!File.Exists(executable)) { message="控制面板程序缺失；可使用 Ctrl+Alt+Q 退出。"; return; }
            controller=Process.Start(new ProcessStartInfo(executable,"--parent "+Process.GetCurrentProcess().Id+" --root \""+root+"\""+(testMode?" --show":"")) { UseShellExecute=false,WorkingDirectory=root });
#endif
        }
        private List<MiniMotionCatalog.Character> Eligible()
        {
            if(catalog==null) return new List<MiniMotionCatalog.Character>();
            int[] testIds={1001,1002,1003,1004,1005,1006,1007,1008,1028,1041};
            return catalog.Characters.Where(c=>(!testMode || testIds.Contains(c.Entry.Id)) &&
                DesktopRules.Allowed(settings.SelectionMode,c.Entry.Id,settings.IncludeIds,settings.ExcludeIds)).ToList();
        }
        private int TargetCount { get { return DesiredPopulation; } }
        private MiniMotionCatalog.Character PickCharacter()
        {
            var pool=Eligible().Where(c=>CanSpawnCharacter(c) && !manualEntries.ContainsName(c.Entry.Name)).ToList();
            if(IncludeVariants && pool.Count>0)
            {int least=pool.Min(c=>actors.Count(a=>SameCharacter(a,c)));pool=pool.Where(c=>actors.Count(a=>SameCharacter(a,c))==least).ToList();}
            if(pool.Count==0) return null;
            if(preferredCharacter.HasValue)
            { var requested=pool.FirstOrDefault(c=>c.Entry.Id==preferredCharacter.Value); preferredCharacter=null; if(requested!=null) return requested; }
            // Keep a stable fixture only in explicit test mode. Normal startup uses the shuffle bag.
            if(testMode && loads==0) {var first=pool.FirstOrDefault(c=>c.Entry.Id==1001);if(first!=null)return first;}
            drawBag.RemoveAll(c=>!pool.Contains(c));
            if(drawBag.Count==0)
            {
                drawBag.AddRange(pool);
                for(int i=drawBag.Count-1;i>0;i--) {int j=random.Next(i+1);var t=drawBag[i];drawBag[i]=drawBag[j];drawBag[j]=t;}
            }
            var result=drawBag[drawBag.Count-1];drawBag.RemoveAt(drawBag.Count-1);return result;
        }
        private IEnumerator Spawn(MiniMotionCatalog.Character requested=null,string outfit=null,DesktopActor replacing=null,bool manualEntry=false)
        {
            if(busy || actors.Count>=settings.MaxActors && replacing==null) yield break;
            busy=true;
            while(EffectivePaused && !visualAudit && !expressionAudit && !pairSpacingAudit) yield return null;
            if(replacing==null && actors.Count>=settings.MaxActors){busy=false;yield break;}
            var selected=requested ?? PickCharacter();
            if(selected==null) { busy=false;population.RetryAfter(now,2);yield break; }
            if(!CanSpawnCharacter(selected,replacing))
            {busy=false;population.RetryAfter(now,2);yield break;}
            var costume=outfit==null?PickFreeCostume(selected,replacing):FreeCostumes(selected,replacing).FirstOrDefault(c=>c.Id==outfit);
            if(costume==null){message="该服装已被场上同名角色使用，或素材不可用，请选择其他服装。";busy=false;yield break;}
            float previousWalkFactor=replacing!=null?replacing.WalkSpeedFactor:0,previousRunFactor=replacing!=null?replacing.RunSpeedFactor:0;
            float previousX=0;int previousDirection=1,ordinal=++nextOrdinal,layer=replacing!=null?replacing.Layer:DesktopLimits.FreeLayer(actors.Select(a=>a.Layer));
            DesktopPhase oldPhase=DesktopPhase.Entering;
            if(replacing!=null)
            {
                CancelPair(replacing); previousX=replacing.X;previousDirection=replacing.Direction;ordinal=replacing.Ordinal;layer=replacing.Layer;oldPhase=replacing.Phase;
                float fade=0;
                while(fade<settings.FadeSeconds) {fade+=frameDelta;replacing.Opacity=1-Mathf.Clamp01(fade/settings.FadeSeconds);yield return null;}
                oldPhase=replacing.Phase;actors.Remove(replacing);replacing.Dispose();yield return null;
            }
            DesktopActor created=null;
            try
            {
                IEnumerator loader=UmaViewerBuilder.Instance.LoadUma(selected.Entry,costume.Id,true);
                while(loader.MoveNext()){}
                var model=UmaViewerBuilder.Instance.CurrentUMAContainer;
                if(model==null || model.UmaAnimator==null || model.Hair==null || model.Head==null) throw new InvalidDataException("Incomplete mini model "+selected.Entry.Id+"/"+costume.Id);
                created=new DesktopActor(++nextToken,ordinal,layer,model,selected,costume,DesktopCamera,settings,catalog,random,Record);
                created.ManualEntry=manualEntry || replacing!=null && replacing.ManualEntry;
                if(replacing!=null) {created.WalkSpeedFactor=previousWalkFactor;created.RunSpeedFactor=previousRunFactor;created.BeginGait();}
                UmaViewerBuilder.Instance.CurrentUMAContainer=null;
                created.Direction=replacing!=null ? previousDirection : random.Next(2)==0 ? 1 : -1;
                created.Phase=replacing!=null ? oldPhase : DesktopPhase.Entering;
                if(created.Phase==DesktopPhase.Exiting)created.BeginGait(true);
                created.X=replacing!=null ? previousX : created.Direction>0 ? created.Overlay.ScreenArea.xMin-created.SurfaceSize*0.5f : created.Overlay.ScreenArea.xMax+created.SurfaceSize*0.5f;
                created.AssignGround(settings.Ground,replacing!=null?replacing.GroundIndex:settings.Ground.mode=="Planes"?random.Next(settings.Ground.surfaces.Length):0,random,replacing==null?null:replacing.Ground);
                created.Overlay.SetDesktopVisible(!desktopPaused);
                created.PreviousX=created.X;created.Opacity=0;created.PlaceWindow(); actors.Add(created);loads++;
                if(!created.ManualEntry&&!DesktopRules.Allowed(settings.SelectionMode,selected.Entry.Id,settings.IncludeIds,settings.ExcludeIds))Retire(created);
                ReconcileDuplicateNames();
                if(replacing==null)
                {
                    if(manualEntry){manualEntries.Complete(selected.Entry.Id);population.ManualAdded(now,actors.Count,TargetCount,actors.Any(a=>a.Phase==DesktopPhase.Exiting));}
                    else population.Added(now,actors.Count,TargetCount);
                }
                message="已加载 "+selected.Entry.Name+" · "+costume.Name;
                Record("Spawn token="+created.Token+" ordinal="+ordinal+" character="+selected.Entry.Id+" costume="+costume.Id+" x="+created.X+" count="+actors.Count);
                created.CaptureFile=Path.Combine(logDirectory,"actor-"+created.Token+".png");
            }
            catch(Exception ex)
            {
                if(created!=null) {actors.Remove(created);created.Dispose();}
                else if(UmaViewerBuilder.Instance.CurrentUMAContainer!=null) Destroy(UmaViewerBuilder.Instance.CurrentUMAContainer.gameObject);
                UmaViewerBuilder.Instance.CurrentUMAContainer=null;
                if(manualEntry)manualRetryAt=now+3;
                Record("Load failed "+selected.Entry.Id+": "+ex); message="该角色加载失败，稍后重试。";population.RetryAfter(now,3);
            }
            // Unload bundle handles without destroying objects still referenced by any live actor.
            UmaAssetManager.ReleaseDesktopTransientBundles();
            if(created!=null)
            {
                float fade=0;
                while(fade<settings.FadeSeconds)
                { fade+=(visualAudit || expressionAudit || pairSpacingAudit)?Time.unscaledDeltaTime:frameDelta;created.Opacity=Mathf.Clamp01(fade/settings.FadeSeconds);yield return null; }
                created.Opacity=1;
            }
            busy=false;WriteStatus();
        }
        private void Update()
        {
            if(WindowsOverlay.Shortcut('Q')) {Application.Quit();return;}
            if(WindowsOverlay.Shortcut('P')) {paused=!paused;message=paused?"已暂停":"继续播放";}
            if(WindowsOverlay.Shortcut('N') && actors.Count>0) Retire(actors.OrderBy(a=>a.Ordinal).First());
            if(exitSeconds>0 && Time.realtimeSinceStartup>=exitSeconds) {WriteStatus();Application.Quit();return;}
            if(Time.realtimeSinceStartup>=nextCommands && commandDirectory!=null) {nextCommands=Time.realtimeSinceStartup+0.1f;ReadCommands();}
            if(!ready || error!=null) {if(Time.realtimeSinceStartup>=nextStatus){nextStatus=Time.realtimeSinceStartup+.5f;WriteStatus();}return;}
            if(groundEditing && controller!=null && controller.HasExited)groundEditing=false;
            RefreshDesktopVisibility(false);
            bool stopped=EffectivePaused;
            WindowsOverlay.InputEnabled=!stopped;
            frameDelta=stopped || wasEffectivelyPaused ? 0 : Mathf.Min(Time.unscaledDeltaTime,0.1f);
            wasEffectivelyPaused=stopped;now+=frameDelta;
            ProcessPointerEvents();
            if(stopped)
            {
                if(Time.realtimeSinceStartup>=nextStatus) {nextStatus=Time.realtimeSinceStartup+0.5f;WriteStatus();}
                return;
            }
            try
            {
                ReconcilePopulationLimit();
                if(!pairAudit)DetectEncounters(true);
                PlanGroundAvoidance(frameDelta);
                foreach(var a in actors.ToArray())
                {
                    if(a.Overlay.TakeClicks()>0) ClickSpecial(a);
                    if(!a.MotionHeld && a.AutomaticSpecial && !CanStartAutomaticSpecial(a))
                    {a.Resume();Record("Automatic special released for clearance token="+a.Token);}
                    float travel=a.MotionHeld?0:SafeTravel(a,frameDelta);
                    a.Step(frameDelta,CanStartAutomaticSpecial(a),travel);
                    if(a.Phase==DesktopPhase.Exiting && a.FullyOutside) Remove(a);
                }
                foreach(var pair in pairs.ToArray()) if(now>=pair.Until) EndPair(pair);
                if(!EffectivePaused && !pairAudit) DetectEncounters();
                if(pairAudit && !busy && now>=nextPairAudit && actors.Count==2 && pairMotions.Count>0)
                {ArrangeAuditPair();StartPair(actors[0],actors[1],pairMotions[auditPairIndex++%pairMotions.Count]);nextPairAudit=now+11;}
                if(!busy && !EffectivePaused && !pairAudit && !TryStartManualEntry())
                {
                    int target=TargetCount;
                    // Already-retiring actors still occupy slots, but are not counted
                    // a second time when selecting excess active actors.
                    if(!actors.Any(a=>a.Phase==DesktopPhase.Exiting))
                    {
                        PopulationAction action=population.Decide(now,actors.Count,target);
                        if(action==PopulationAction.Add) StartCoroutine(Spawn());
                        else if(action==PopulationAction.RetireOldest && actors.Count>0) Retire(actors.OrderBy(a=>a.Ordinal).First());
                    }
                }
                ReconcileDuplicateNames();
                if(!OccupancyValid)throw new InvalidOperationException("Population invariant violated: cap, character or outfit collision.");
                if(Time.realtimeSinceStartup>=nextStatus) {nextStatus=Time.realtimeSinceStartup+0.5f;WriteStatus();}
            }
            catch(Exception ex) {Fail(ex);}
        }
        private void LateUpdate()
        {
            if(!ready || error!=null || EffectivePaused) return;
            SortGroundWindows();
            foreach(var actor in actors) { actor.TickPose(frameDelta); actor.RenderFrame(); }

        }
        private void RefreshDesktopVisibility(bool force)
        {
            float realtime=Time.realtimeSinceStartup;
            if(!force && realtime<nextVisibility) return;
            nextVisibility=realtime+0.25f;
            DesktopEnvironment.Poll();
            bool stop=!visualAudit && (!DesktopEnvironment.Available || DesktopEnvironment.Covered && !testIgnoreOcclusion);
            bool changed=!visibilityKnown || stop!=desktopPaused;
            desktopPaused=stop;visibilityKnown=true;
            foreach(var actor in actors) actor.Overlay.SetDesktopVisible(!desktopPaused);
            Application.targetFrameRate=EffectivePaused && !expressionAudit ? 2 : settings.FrameRate;
            if(changed) Record("Desktop visibility: paused="+desktopPaused+" covered="+DesktopEnvironment.Covered+" host="+DesktopEnvironment.Host.ToInt64());
        }
        private bool CanStartAutomaticSpecial(DesktopActor actor)
        {
            if(actor.CustomGround)return GroundSpecialClear(actor);
            if(actor.ClearanceX-actor.ClearanceHalfWidth<actor.Overlay.ScreenArea.xMin || actor.ClearanceX+actor.ClearanceHalfWidth>actor.Overlay.ScreenArea.xMax) return false;
            return actors.All(other=>other==actor || DesktopSpacing.Clear(actor.ClearanceX,actor.ClearanceHalfWidth,other.ClearanceX,other.ClearanceHalfWidth,settings.SpecialClearancePixels));
        }
        private float TravelLimit(DesktopActor actor,float distance)
        {
            foreach(var other in actors)
                if(other!=actor && (other.Interaction || other.AutomaticSpecial && other.Behavior==MiniBehavior.Special))
                    distance=(float)DesktopSpacing.TravelLimit(actor.ClearanceX,actor.Direction,distance,actor.ClearanceHalfWidth,other.ClearanceX,other.ClearanceHalfWidth,settings.SpecialClearancePixels);
            return distance;
        }
        private float SafeTravel(DesktopActor actor,float delta)
        {
            float requested=actor.MoveSpeed*delta;
            if(actor.CustomGround)return GroundSafeTravel(actor,requested);
            if(actor.Interaction) return requested;
            float allowed=TravelLimit(actor,requested);
            if(allowed+0.01f<requested && actor.Phase==DesktopPhase.Active)
            {
                actor.Direction*=-1;
                float reverse=TravelLimit(actor,requested);
                float endpoint=actor.X+actor.Direction*reverse,margin=actor.Height*0.32f;
                if(actor.Phase==DesktopPhase.Active && (endpoint<actor.Overlay.ScreenArea.xMin+margin || endpoint>actor.Overlay.ScreenArea.xMax-margin))
                {actor.Direction*=-1;return allowed;}
                return reverse;
            }
            return allowed;
        }
        private void ClickSpecial(DesktopActor actor)
        {
            CancelPair(actor); if(!actor.HasSpecial) {message="该角色没有可用专属动作。";return;}
            actor.BeginSpecial();clickedSpecials++;message="点击互动："+actor.Character.Entry.Name;Record("Click token="+actor.Token);
        }
        private void Retire(DesktopActor actor)
        {
            if(actor.Phase==DesktopPhase.Exiting || actor.MotionHeld) return;
            CancelPair(actor);actor.BeginExit();population.Retiring();
            Record("Exit requested token="+actor.Token+" ordinal="+actor.Ordinal+" count="+actors.Count);
        }
        private void Remove(DesktopActor actor)
        {
            CancelPair(actor);actors.Remove(actor);actor.Dispose();departures++;
            if(!actors.Any(a=>a.Phase==DesktopPhase.Exiting)) population.Removed(now,actors.Count,TargetCount);
            Record("Removed token="+actor.Token+" count="+actors.Count+" next="+population.NextAt.ToString("F2"));
            StartCoroutine(ReclaimUnused());
        }
        private IEnumerator ReclaimUnused() {yield return null;UmaAssetManager.ReleaseDesktopTransientBundles();yield return Resources.UnloadUnusedAssets();}
        private void DetectEncounters(bool planesOnly=false)
        {
            if(pairMotions.Count==0) return;
            var sorted=actors.OrderBy(a=>a.GroundIndex).ThenBy(a=>a.X).ToArray();
            for(int i=0;i<sorted.Length-1;i++)
            {
                var a=sorted[i];var b=sorted[i+1];
                if(planesOnly && (a.Ground==null || a.Ground.IsLine || b.Ground==null || b.Ground.IsLine))continue;
                if(a.CustomGround || b.CustomGround)
                {if(!a.CustomGround || !b.CustomGround || a.GroundIndex!=b.GroundIndex || Mathf.Abs(a.GroundY-b.GroundY)>Mathf.Min(a.Height,b.Height)*.12f)continue;}
                float threshold=MeetingDistance(a,b);
                if(a.MotionHeld || b.MotionHeld || a.Phase!=DesktopPhase.Active || b.Phase!=DesktopPhase.Active || a.Interaction || b.Interaction || now<a.CooldownUntil || now<b.CooldownUntil) continue;
                if(!DesktopRules.CanMeet(a.Behavior==MiniBehavior.Walk,b.Behavior==MiniBehavior.Walk,a.Direction,b.Direction,
                    b.PreviousX-a.PreviousX,b.X-a.X,threshold)) continue;
                encounterRolls++;a.CooldownUntil=b.CooldownUntil=now+settings.InteractionCooldownSeconds;
                bool accept=random.NextDouble()<settings.InteractionProbability;
                Record("Encounter "+a.Token+"/"+b.Token+" accepted="+accept);
                if(accept) {drawsAccepted++;StartPair(a,b,pairMotions[random.Next(pairMotions.Count)]);}
            }
        }
        private float MeetingDistance(DesktopActor a,DesktopActor b)
        {
            float distance=(a.Height+b.Height)*0.5f;
            foreach(var motion in pairMotions)
                distance=Mathf.Max(distance,a.PairFootprint(motion.A,1).y-b.PairFootprint(motion.B,-1).x+Mathf.Max(28,(a.Height+b.Height)*0.045f));
            return distance;
        }
        private void StartPair(DesktopActor a,DesktopActor b,PairMotion motion)
        {
            Vector2 left=a.PairFootprint(motion.A,1),right=b.PairFootprint(motion.B,-1);
            float gap=Mathf.Max(28,(a.Height+b.Height)*0.045f);
            float center=(a.X+(left.x+left.y)*0.5f+b.X+(right.x+right.y)*0.5f)*0.5f;
            float ax=center-gap*0.5f-left.y,bx=center+gap*0.5f-right.x;
            // Keep the entire animated pair on screen, including unequal model sizes.
            float min=ax+left.x,max=bx+right.y;
            float screenLeft=a.Overlay.ScreenArea.xMin+8,screenRight=a.Overlay.ScreenArea.xMax-8;
            if(max-min>screenRight-screenLeft) return;
            float shift=Mathf.Max(0,screenLeft-min)-Mathf.Max(0,max-screenRight);ax+=shift;bx+=shift;
            foreach(var other in actors.Where(x=>x!=a && x!=b))
                if(!DesktopSpacing.Clear(ax+(left.x+left.y)*0.5f,(left.y-left.x)*0.5f,other.ClearanceX,other.ClearanceHalfWidth,settings.SpecialClearancePixels) ||
                   !DesktopSpacing.Clear(bx+(right.x+right.y)*0.5f,(right.y-right.x)*0.5f,other.ClearanceX,other.ClearanceHalfWidth,settings.SpecialClearancePixels)) return;
            if(a.CustomGround || b.CustomGround)
            {
                float y=(a.GroundY+b.GroundY)*.5f;
                GroundVector oldA=a.Ground.Foot,oldB=b.Ground.Foot;
                if(!a.SetGroundScreen(ax,y) || !b.SetGroundScreen(bx,y))
                {a.SetGroundScreen((float)oldA.x+a.Overlay.ScreenArea.xMin,(float)oldA.y+a.Overlay.ScreenArea.yMin);b.SetGroundScreen((float)oldB.x+b.Overlay.ScreenArea.xMin,(float)oldB.y+b.Overlay.ScreenArea.yMin);return;}
                ax=a.X;bx=b.X;
            }
            CancelPair(a);CancelPair(b);
            a.X=ax;b.X=bx;a.Direction=1;b.Direction=-1;
            a.BeginInteraction(motion.A,b.Token);b.BeginInteraction(motion.B,a.Token);a.PlaceWindow();b.PlaceWindow();
            float duration=Mathf.Max(0.2f,Mathf.Min(a.SpecialDuration,b.SpecialDuration)-0.25f);
            pairs.Add(new PairSession {A=a,B=b,Until=now+duration});interactions++;
            a.CooldownUntil=b.CooldownUntil=now+duration+settings.InteractionCooldownSeconds;
            message=a.Character.Entry.Name+" 与 "+b.Character.Entry.Name+" 互动中";
            Record("Pair "+motion.Name+" tokens="+a.Token+"/"+b.Token+" duration="+duration.ToString("F2")+" gap="+gap.ToString("F1")+" separation="+(bx-ax).ToString("F1"));
            if((testMode || pairAudit) && !pairSpacingAudit) StartCoroutine(CapturePair(a,b));
        }
        private IEnumerator CapturePair(DesktopActor a,DesktopActor b)
        {
            yield return new WaitForSecondsRealtime(2);
            if(!a.Disposed && !b.Disposed) {a.CaptureFile=Path.Combine(logDirectory,"pair-"+interactions+"-A.png");b.CaptureFile=Path.Combine(logDirectory,"pair-"+interactions+"-B.png");}
        }
        private void EndPair(PairSession pair)
        {
            pairs.Remove(pair);
            if(!pair.A.Disposed) pair.A.Resume();if(!pair.B.Disposed) pair.B.Resume();
            Record("Pair finished tokens="+pair.A.Token+"/"+pair.B.Token);
        }
        private void CancelPair(DesktopActor actor)
        {foreach(var pair in pairs.Where(p=>p.A==actor || p.B==actor).ToArray()) EndPair(pair);}
        private void ArrangeAuditPair()
        {
            if(actors.Count<2) return;
            float center=actors[0].Overlay.ScreenArea.center.x;
            actors[0].X=center-130;actors[0].Direction=1;actors[0].Phase=DesktopPhase.Active;
            actors[1].X=center+130;actors[1].Direction=-1;actors[1].Phase=DesktopPhase.Active;
        }
        private void ReadCommands()
        {
            foreach(string file in Directory.GetFiles(commandDirectory,"*.json").OrderBy(f=>f,StringComparer.Ordinal))
            {
                try {var command=JsonUtility.FromJson<Command>(File.ReadAllText(file));File.Delete(file);HandleCommand(command);}
                catch(Exception ex) {Record("Command failed: "+ex.Message);try{if(!(ex is IOException) && File.Exists(file))File.Delete(file);}catch(IOException){}}
            }
            try{ApplyPendingModelSize();}catch(Exception ex){Fail(ex);}
        }
        private void ApplyPendingModelSize()
        {
            if(!pendingModelSize.HasValue)return;
            float value=pendingModelSize.Value;pendingModelSize=null;
            var captures=actors.Select(a=>a.CaptureFile).ToArray();
            AsyncGPUReadback.WaitAllRequests();
            for(int i=0;i<actors.Count;i++)if(captures[i]!=null)actors[i].CaptureFile=captures[i];
            foreach(var pair in pairs.ToArray())EndPair(pair);
            foreach(var actor in actors) {actor.SetModelSize(value);if(ready && !desktopPaused){actor.TickPose(0);actor.RenderFrame();}}
            if(EffectivePaused && !desktopPaused)AsyncGPUReadback.WaitAllRequests();
            settings.ModelSizeScale=persisted.ModelSizeScale=value;DesktopSettings.Save(settingsPath,persisted);
            message="模型大小："+Mathf.RoundToInt(value*100)+"%";Record("Model size="+value.ToString("F2"));
        }
        private void HandleCommand(Command c)
        {
            var actor=actors.FirstOrDefault(a=>a.Token==c.token) ?? actors.FirstOrDefault();
            switch(c.action)
            {
                case "actorLimit": case "avoidance": case "performance": case "language": case "dataPath": ApplyOptions(c);break;
                case "groundEditBegin": groundEditing=true;break;
                case "groundEditEnd": groundEditing=false;break;
                case "ground": ApplyGround(c.ground);break;
                case "exit": WriteStatus();Application.Quit();break;
                case "pause": paused=!paused;break;
                case "scale": pendingModelSize=float.IsNaN(c.modelSizeScale) || float.IsInfinity(c.modelSizeScale)?1:Mathf.Clamp(c.modelSizeScale,0.5f,1.5f);break;
                case "special": if(actor!=null)ClickSpecial(actor);break;
                case "next": if(actor!=null)Retire(actor);break;
                case "retire": RequestManualExit(actors.FirstOrDefault(a=>a.Token==c.token));break;
                case "enter": RequestManualEntry(c.characterId);break;
                case "costume":
                    if(actor!=null && !busy && actor.Phase!=DesktopPhase.Exiting)
                    {
                        StartCoroutine(Spawn(actor.Character,c.costumeId,actor));
                    }
                    break;
                case "filter":
                    settings.SelectionMode=c.mode=="Include" || c.mode=="Exclude" ? c.mode : "All";
                    settings.IncludeIds=c.includeIds ?? settings.IncludeIds;settings.ExcludeIds=c.excludeIds ?? settings.ExcludeIds;
                    persisted.SelectionMode=settings.SelectionMode;persisted.IncludeIds=settings.IncludeIds;persisted.ExcludeIds=settings.ExcludeIds;
                    DesktopSettings.Save(settingsPath,persisted);RefreshIncludeVariantPolicy();drawBag.Clear();
                    foreach(var a in actors.Where(a=>!a.ManualEntry && !DesktopRules.Allowed(settings.SelectionMode,a.Character.Entry.Id,settings.IncludeIds,settings.ExcludeIds)).ToArray())Retire(a);
                    ReconcileDuplicateNames();
                    message="角色池已更新；可出场 "+TargetCount+" 只。";Record("Filter "+settings.SelectionMode+" eligible="+Eligible().Count);break;
                case "capture": foreach(var a in actors)a.CaptureFile=Path.Combine(logDirectory,"capture-"+a.Token+".png");break;
                case "testPointerDown": if(testMode && actor!=null)actor.Overlay.RoutePointer(DesktopPointerKind.Down,(int)c.groundU,(int)c.groundV,false);break;
                case "testPointerMove": if(testMode && actor!=null)actor.Overlay.RoutePointer(DesktopPointerKind.Move,(int)c.groundU,(int)c.groundV,true);break;
                case "testPointerUp": if(testMode && actor!=null)actor.Overlay.RoutePointer(DesktopPointerKind.Up,(int)c.groundU,(int)c.groundV,true);break;
                case "testGait":
                    if(testMode && actor!=null){CancelPair(actor);actor.Schedule.ForceGait(c.mode=="Run"?MiniBehavior.Run:MiniBehavior.Walk);actor.BeginGait();
                        if(c.costumeId!=null){var entry=catalog.Find(c.costumeId);if(entry!=null){actor.Player.Play(entry,true);actor.Player.Seek(0);}}}
                    break;
                case "testCharacter":
                    if(testMode && !busy){var ch=catalog.Characters.FirstOrDefault(x=>x.Entry.Id==c.characterId);if(ch!=null)StartCoroutine(Spawn(ch,c.costumeId??"00",actor));}
                    break;
                case "testNavigation":
                    if(testMode && actor!=null && settings.Ground.mode=="Planes" && c.navigationTarget!=null && c.groundIndex>=0 && c.groundIndex<settings.Ground.surfaces.Length)
                    {
                        CancelPair(actor);actor.AssignGround(settings.Ground,c.groundIndex,random);
                        actor.Ground.PlaceForDrag(new GroundVector(c.groundU,c.groundV));
                        actor.Ground.Target=new GroundVector(c.navigationTarget.x,c.navigationTarget.y);
                        actor.Phase=DesktopPhase.Active;actor.Schedule.ForceGait(c.mode=="Run"?MiniBehavior.Run:MiniBehavior.Walk);actor.BeginGait();
                        if(c.navigationSpeed>0)actor.MoveSpeed=Mathf.Clamp(c.navigationSpeed,10,600);
                        actor.CooldownUntil=now+120;actor.TickPose(0);actor.PoseTestVersion++;
                    }
                    break;
                case "testGroundPose":
                    if(testMode && actor!=null && actor.CustomGround)
                    {
                        string pendingCapture=actor.CaptureFile;actor.CaptureFile=null;AsyncGPUReadback.WaitAllRequests();actor.CaptureFile=pendingCapture;
                        GroundVector point=actor.Ground.IsLine?actor.Ground.Screen(new GroundVector(c.groundU,0)):actor.Ground.Map.Project(c.groundU,c.groundV);
                        actor.SetGroundScreen(actor.Overlay.ScreenArea.xMin+(float)point.x,actor.Overlay.ScreenArea.yMin+(float)point.y);
                        if(c.mode=="proportions"){actor.Player.Play(catalog.Walks[0],true);actor.Player.Tick(.25f);}
                        actor.TickPose(0);
                        if(c.mode=="footing")actor.SampleGroundContact(c.poseYaw);
                        actor.PoseTestVersion++;
                        if(c.mode=="proportions")actor.Model.transform.rotation=Quaternion.Euler(0,-65,0);
                        actor.PlaceWindow();actor.RenderFrame();AsyncGPUReadback.WaitAllRequests();
                    }
                    break;
                case "testApproachSpecial":
                    if(testMode)
                    {
                        var performing=actors.FirstOrDefault(a=>a.AutomaticSpecial);
                        var walker=actors.FirstOrDefault(a=>a!=performing);
                        if(performing!=null && walker!=null)
                        {
                            CancelPair(walker);walker.Schedule.ForceGait(MiniBehavior.Run);walker.BeginGait();walker.Phase=DesktopPhase.Active;
                            walker.X=performing.ClearanceX-walker.ClearanceOffset+performing.ClearanceHalfWidth+walker.ClearanceHalfWidth+settings.SpecialClearancePixels+30;walker.Direction=-1;
                        }
                    }
                    break;
                case "testSpecialDue": if(testMode && actor!=null)actor.Schedule.MakeSpecialDue();break;
                case "testSpacing":
                    if(testMode && actors.Count>=2)
                    {
                        foreach(var a in actors) {CancelPair(a);a.Resume();a.Phase=DesktopPhase.Active;a.CooldownUntil=now+120;}
                        var first=actors[0];var second=actors[1];first.X=first.Overlay.ScreenArea.center.x;second.X=first.X+10;first.Direction=-1;second.Direction=1;
                        first.Schedule.MakeSpecialDue();
                    }
                    break;
                case "testHang": if(testMode){Record("Test blocking Unity main thread");System.Threading.Thread.Sleep(30000);}break;
                case "testNativeClick": if(testMode && actor!=null) actor.Overlay.TestClick();break;
                case "testEncounter":
                    var meetingActors=actors.Where(a=>a.Phase==DesktopPhase.Active && !a.Interaction).Take(2).ToArray();
                    if(testMode && meetingActors.Length==2)
                    {
                        var a=meetingActors[0];var b=meetingActors[1];CancelPair(a);CancelPair(b);
                        a.Schedule.ForceGait(MiniBehavior.Walk);b.Schedule.ForceGait(MiniBehavior.Walk);
                        a.BeginGait();b.BeginGait();a.Direction=1;b.Direction=-1;a.CooldownUntil=b.CooldownUntil=0;
                        float center=a.Overlay.ScreenArea.center.x, distance=MeetingDistance(a,b)+4;
                        a.X=center-distance/2;b.X=center+distance/2;
                    }
                    break;
            }
            WriteStatus();
        }
        private void WriteStatus()
        {
            if(logDirectory==null) return;
            var state=new Snapshot {includeVariants=IncludeVariants,occupancyValid=OccupancyValid,costumesUnique=actors.GroupBy(a=>DesktopRules.NameKey(a.Character.Entry.Name)+"\n"+a.Costume.Id).All(g=>g.Count()==1),activeDataPath=settings==null?"":settings.DataPath,configuredDataPath=persisted==null?"":persisted.DataPath,language=settings==null?"zh":settings.UiLanguage,optionsAck=optionsAck,frameRate=settings==null?30:settings.FrameRate,useFxaa=settings!=null && settings.UseFxaa,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),operatingSystem=SystemInfo.operatingSystem,applicationVersion=DesktopProduct.Version,maxActors=settings==null?6:settings.MaxActors,activeCount=actors.Count(a=>a.Phase!=DesktopPhase.Exiting),retiringCount=actors.Count(a=>a.Phase==DesktopPhase.Exiting),overLimitCount=settings==null?0:Math.Max(0,actors.Count-settings.MaxActors),avoid3DCollisions=settings!=null&&settings.Avoid3DCollisions,pendingEntries=ExportPendingEntries(),ground=settings==null?new DesktopGroundConfig():settings.Ground,groundEditing=groundEditing,processId=Process.GetCurrentProcess().Id,modelSizeScale=settings==null?1:settings.ModelSizeScale,utc=DateTime.UtcNow.ToString("o"),error=error,message=message,paused=EffectivePaused,testIgnoresOcclusion=testIgnoreOcclusion,manualPaused=paused,desktopPaused=desktopPaused,desktopCovered=DesktopEnvironment.Covered,desktopAvailable=DesktopEnvironment.Available,desktopHost=DesktopEnvironment.Host.ToInt64(),coveringWindows=DesktopEnvironment.CoveringWindows,loading=busy,mouseInputInstalled=WindowsOverlay.InputInstalled,windowManagementActive=DesktopEnvironment.WindowManagementActive,switchEventHookInstalled=DesktopEnvironment.SwitchEventHookInstalled,
                count=actors.Count,targetCount=catalog==null?0:TargetCount,charactersLoaded=loads,departures=departures,clickSpecials=clickedSpecials,
                specialCount=specials,trimmedSpecials=trimmed,gaitCount=gaits,interactions=interactions,encounterRolls=encounterRolls,interactionAccepted=drawsAccepted,
                mode=settings==null?"All":settings.SelectionMode,includeIds=settings==null?new int[0]:settings.IncludeIds,excludeIds=settings==null?new int[0]:settings.ExcludeIds,
                namesUnique=actors.Select(a=>DesktopRules.NameKey(a.Character.Entry.Name)).Distinct(StringComparer.Ordinal).Count()==actors.Count,
                controllerRunning=controller!=null && !controller.HasExited,fps=1/Mathf.Max(0.001f,Time.unscaledDeltaTime),managedBytes=GC.GetTotalMemory(false),simulationSeconds=now,
                populationState=busy?"Loading":population!=null && population.AwaitingExit?"WaitingForExit":actors.Count>0 && actors.Count>=TargetCount?"FullHold":"WaitingForEntry",
                nextEventIn=population==null?0:Mathf.Max(0,(float)population.NextAt-now),groundBottomPadding=settings==null?0:settings.GroundBottomPaddingPixels,
                specialTrim=settings==null?0:settings.SpecialEndTrimSeconds,outlineWidth=settings==null?0:settings.OutlineWidth,msaaSamples=settings==null?0:settings.MsaaSamples,
                actors=actors.Select(a=>new ActorState {avoidanceState=a.AvoidanceState,avoidanceFrames=a.AvoidanceFrames,avoidanceReversals=a.AvoidanceReversals,avoidanceRadius=a.AvoidanceRadius,manualEntry=a.ManualEntry,boundaryReplans=a.Ground==null?0:a.Ground.BoundaryReplans,groundTargetX=a.Ground==null?0:(float)a.Ground.Screen(a.Ground.Target).x,groundTargetY=a.Ground==null?0:(float)a.Ground.Screen(a.Ground.Target).y,actualShoePixels=a.ActualShoePixels().Select(p=>new GroundPoint((float)p.x,(float)p.y)).ToArray(),poseVersion=a.PoseTestVersion,groundFitScale=a.GroundFitScale,canvasFootViewport=a.CanvasFootViewport,calibratedFloorY=a.CalibratedFloorY,soleClearancePixels=a.SoleClearancePixels,footprintRadiusX=a.FootprintRadiusX,footprintRadiusY=a.FootprintRadiusY,footContacts=a.ProjectedFootContacts().Select(p=>new GroundPoint((float)p.x,(float)p.y)).ToArray(),pointerHeld=a.PointerHeld,dragging=a.Dragging,motionHeld=a.MotionHeld,animationTime=a.Player.TimeSeconds,groundIndex=a.GroundIndex,groundStage=a.Ground==null?-1:a.Ground.Stage,groundU=a.Ground==null?0:(float)a.Ground.Position.x,groundV=a.Ground==null?0:(float)a.Ground.Position.y,groundDepthScale=a.GroundDepthScale,portalOpacity=a.PortalOpacity,token=a.Token,ordinal=a.Ordinal,characterId=a.Character.Entry.Id,name=a.Character.Entry.Name,displayName=DesktopCharacterNames.Get(a.Character.Entry.Id,a.Character.Entry.Name,settings.UiLanguage),
                    costumeId=a.Costume.Id,costumeName=a.Costume.Name,costumeCategory=a.Costume.Category,phase=a.Phase.ToString(),state=a.Interaction?"Interaction":a.Behavior.ToString(),
                    expression=a.Expressions.Label,expressionAction=a.Expressions.Action.ToString(),expressionSelections=a.Expressions.Selections,expressionOverride=a.Expressions.OverrideActive,expressionMatchesPreset=a.Expressions.MatchesPreset,expressionIndices=a.Expressions.ActualIndices,clip=a.Player.Name,screenX=a.X,groundY=a.GroundY,height=a.Height,direction=a.Direction,speed=a.MoveSpeed,playbackRate=a.PlaybackRate,opacity=a.Opacity,
                    specialElapsed=a.SpecialElapsed,specialDuration=a.SpecialDuration,specialCutoff=a.SpecialCutoff,presentedFrames=a.Overlay.FramesPresented,
                    specialDeferrals=a.SpecialDeferrals,automaticSpecials=a.AutomaticSpecials,automaticSpecial=a.AutomaticSpecial,specialPending=a.Schedule.SpecialPending,walkSpeedFactor=a.WalkSpeedFactor,runSpeedFactor=a.RunSpeedFactor,clearanceHalfWidth=a.ClearanceHalfWidth,clearanceX=a.ClearanceX,clearanceOffset=a.ClearanceOffset,specialRetryDistance=a.SpecialRetryDistance,movementFraction=a.MovementFraction,facialEvaluations=a.Player.FacialEvaluations,nativeWindow=a.Overlay.Handle.ToInt64(),desktopParent=a.Overlay.DesktopParent,desktopAttached=a.Overlay.DesktopAttached,windowVisible=a.Overlay.DesktopVisible}).ToArray()};
            string target=Path.Combine(logDirectory,"state.json"), temporary=target+".tmp";
            try
            {
                File.WriteAllText(temporary,JsonUtility.ToJson(state,true));
                if(File.Exists(target)) File.Replace(temporary,target,null); else File.Move(temporary,target);
            }
            catch(IOException) { /* A reader without delete-sharing may briefly hold the old snapshot. Retry next tick. */ }
        }
        private void Record(string text)
        {
            if(text.StartsWith("Special token="))specials++;
            if(text.StartsWith("Special cutoff"))trimmed++;
            if(text.StartsWith("Gait token="))gaits++;
            Debug.Log("[UmaDesktop] "+text);
            if(logDirectory!=null)try{File.AppendAllText(Path.Combine(logDirectory,"events.log"),DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine);}catch(IOException){}
        }
        private void Fail(Exception ex) {error=ex.ToString();Debug.LogException(ex);try{Record("ERROR "+error);WriteStatus();}catch{}}
        private void OnApplicationQuit()
        {
            quitting=true;DesktopEnvironment.Shutdown();WindowsOverlay.InputEnabled=false;AsyncGPUReadback.WaitAllRequests();
            foreach(var a in actors.ToArray())a.Dispose();actors.Clear();
            if(controller!=null && !controller.HasExited)controller.CloseMainWindow();
            Environment.SetEnvironmentVariable("UMA_DESKTOP_CONFIG",null);
        }
        private void OnDestroy() {if(!quitting)OnApplicationQuit();}
    }
}
