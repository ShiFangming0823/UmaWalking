using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UmaDesktop
{
    public enum DesktopPhase { Entering, Active, Exiting }

    public sealed partial class DesktopActor : IDisposable
    {
        private float modelSizeScale;
        public int SurfaceSize {get{return Mathf.RoundToInt(settings.WindowSize*modelSizeScale);}}
        public int Token, Ordinal, Layer, Direction;
        public float X, Height, MoveSpeed, PlaybackRate = 1, SpecialElapsed, SpecialDuration, SpecialCutoff, CooldownUntil;
        public float PreviousX, Opacity = 1;
        public float WalkSpeedFactor, RunSpeedFactor, SoloHalfWidth, InteractionHalfWidth, SpecialRetryDistance, MovementFraction=1;
        public float SoloOffset, InteractionOffset;
        public float ClearanceHalfWidth { get { return Interaction ? InteractionHalfWidth : SoloHalfWidth; } }
        public float ClearanceOffset { get { return Interaction ? InteractionOffset : SoloOffset; } }
        public float ClearanceX { get { return X+ClearanceOffset; } }
        public bool AutomaticSpecial;
        public int SpecialDeferrals, AutomaticSpecials;
        private Mesh envelopeMesh;
        private float envelopeRadius,envelopeTop=float.NegativeInfinity,envelopeBottom=float.PositiveInfinity;
        public bool Interaction, SpecialDone, Disposed;
        public int PartnerToken = -1, SpecialsStarted, SpecialsTrimmed, GaitsStarted;
        public DesktopPhase Phase;
        public MiniMotionCatalog.Character Character;
        public MiniMotionCatalog.Costume Costume;
        public UmaContainerCharacter Model;
        public Camera Camera;
        public MiniClipPlayer Player;
        public MiniExpressionController Expressions;
        public MiniFaceSurface FaceSurface;
        public DesktopSchedule Schedule;
        public WindowsOverlay Overlay = new WindowsOverlay();
        public string CaptureFile;
        public readonly Dictionary<string, float> GaitGroundSpeed = new Dictionary<string, float>();
        private readonly DesktopSettings settings;
        private readonly MiniMotionCatalog catalog;
        private readonly ShuffleBag<UmaDatabaseEntry> walks, runs;
        private readonly ShuffleBag<UmaDatabaseEntry> specials;
        private readonly Action<string> log;
        private readonly List<UmaDatabaseEntry> sequence = new List<UmaDatabaseEntry>();
        private int part;
        private Vector3 origin;
        private RenderTexture renderTarget;
        private readonly RenderTexture[] readTargets = new RenderTexture[3];
        private readonly bool[] inFlight = new bool[3];
        private int pendingReads, submittedFrame, presentedFrame;
        private readonly DesktopHeadingFilter desktopHeading=new DesktopHeadingFilter();
        public float FacingYaw {get{return facingYaw;}}
        public float RequestedGroundYaw {get{return GroundYaw;}}
        private float facingYaw, lowestSolePixel = float.PositiveInfinity;
        private Bounds[] footBounds;
        private Material presentationMaterial;
        private readonly UniversalRenderPipeline.SingleCameraRequest renderRequest = new UniversalRenderPipeline.SingleCameraRequest();
        private bool cleaned;
        public MiniBehavior Behavior { get { return Schedule.Behavior; } }
        public bool HasSpecial { get { return specials != null; } }
        public float GroundY { get { if(Ground!=null)return Overlay.ScreenArea.yMin+(float)Ground.Foot.y;return (settings.GroundAtPhysicalScreenBottom ? Overlay.ScreenArea.yMax : Overlay.WorkArea.yMax) - settings.GroundBottomPaddingPixels; } }
        public const float FootViewport = 0.004f;

        public DesktopActor(int token, int ordinal, int layer, UmaContainerCharacter model,
            MiniMotionCatalog.Character character, MiniMotionCatalog.Costume costume, Camera template,
            DesktopSettings settings, MiniMotionCatalog catalog, System.Random random, Action<string> log)
        {
            Token = token; Ordinal = ordinal; Layer = layer; Model = model; Character = character; Costume = costume;
            this.settings = settings; this.catalog = catalog; this.log = log;modelSizeScale=settings.ModelSizeScale;
            WalkSpeedFactor=(float)DesktopSpacing.SampleSpeedFactor(random,settings.SpeedFactorMin,settings.SpeedFactorMax);
            RunSpeedFactor=(float)DesktopSpacing.SampleSpeedFactor(random,settings.SpeedFactorMin,settings.SpeedFactorMax);
            Model.EnableEyeTracking = false; Model.EnablePhysics = false;
            Model.transform.SetPositionAndRotation(new Vector3((layer - 25) * 10, 0, 0), Quaternion.identity);
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material == null) continue;
                    // The desktop uses only the thin shell, never the game's extra bold pass.
                    material.SetShaderPassEnabled("BoldOutline", false);
                    material.SetShaderPassEnabled("MiniCharaBodyBoldOutline", false);
                    if (material.HasProperty("_BoldOutlineWidth")) material.SetFloat("_BoldOutlineWidth", 0);
                    if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", Mathf.Clamp01(material.GetFloat("_OutlineWidth")));
                }
            }
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Explicit rendering must use the same current pose as rigid eye/mouth planes.
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
                skin.quality = SkinQuality.Bone4;
            }
            foreach (LineRenderer line in model.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
            foreach (UIHandle handle in model.GetComponentsInChildren<UIHandle>(true)) handle.enabled = false;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) if (animator != model.UmaAnimator) animator.enabled = false;
            foreach (var material in model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                foreach (string property in material.GetTexturePropertyNames())
                    if (material.GetTexture(property) is Texture2D texture && texture.name.StartsWith("tex_mbdy", StringComparison.OrdinalIgnoreCase))
                    { texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 4; if (texture.mipmapCount > 1) texture.mipMapBias = 0.15f; }
            FaceSurface=new MiniFaceSurface(model);
            Expressions=new MiniExpressionController(model,character.Entry.Id,random);
            origin = model.Position == null ? Vector3.zero : model.Position.localPosition;
            walks = new ShuffleBag<UmaDatabaseEntry>(catalog.Walks, random);
            runs = new ShuffleBag<UmaDatabaseEntry>(catalog.Runs, random);
            var ownSpecials = catalog.CostumeSpecials(character, costume);
            if (ownSpecials.Count > 0) specials = new ShuffleBag<UmaDatabaseEntry>(ownSpecials, random);
            Schedule = new DesktopSchedule(random, settings.LocomotionMinSeconds, settings.LocomotionMaxSeconds, 1e9, 1e9, settings.WalkProbability);
            Player = new MiniClipPlayer(model);
            // Cache all source clips and facial tracks before the actor enters.
            foreach (var entry in catalog.Walks.Concat(catalog.Runs).Concat(ownSpecials))
            {
                Player.Preload(entry);
                if (ownSpecials.Contains(entry))
                    foreach (string suffix in new[] { "_s", "_e" }) { var e = catalog.Companion(entry, suffix); if (e != null) Player.Preload(e); }
            }
            Player.Play(catalog.Walks[0], true);
            facingYaw = 0;
            Camera = UnityEngine.Object.Instantiate(template);
            Camera.name = "DesktopCamera_" + token; Camera.tag = "Untagged";
            Camera.cullingMask = 1 << layer; Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Color.clear; Camera.allowHDR = false; Camera.allowMSAA = settings.MsaaSamples > 1;
            FrameCharacter();
            MeasureShoeBounds();
            var calibrationEntries = catalog.Walks.Concat(catalog.Runs).Concat(ownSpecials.SelectMany(e => new[] { e, catalog.Companion(e,"_s"), catalog.Companion(e,"_e") }).Where(e => e != null)).Distinct();
            var pairedEntries=new List<UmaDatabaseEntry>();
            foreach(string kind in new[]{"01","02","03"}) foreach(string role in new[]{"a","b"})
            {
                var entry=catalog.Find("3d/motion/mini/set/body/type"+kind+"/anm_min_set_type"+kind+"_004_01_"+role);
                if(entry!=null) {Player.Preload(entry);pairedEntries.Add(entry);}
            }
            envelopeMesh=new Mesh();
            try
            {
                foreach (var entry in calibrationEntries.Distinct()) CalibrateStride(entry);
                MeasureEnvelopeInterval(out SoloHalfWidth,out SoloOffset);
                foreach (var entry in pairedEntries.Distinct()) CalibrateStride(entry);
                MeasureEnvelopeInterval(out InteractionHalfWidth,out InteractionOffset);
            }
            finally {UnityEngine.Object.Destroy(envelopeMesh);envelopeMesh=null;}
            log("Clearance token="+Token+" halfWidth="+SoloHalfWidth.ToString("F1")+" offset="+SoloOffset.ToString("F1")+" pairHalfWidth="+InteractionHalfWidth.ToString("F1")+" walkFactor="+WalkSpeedFactor.ToString("F3")+" runFactor="+RunSpeedFactor.ToString("F3"));
            if (!float.IsInfinity(lowestSolePixel))
            {
                float pixelsPerUnit = SurfaceSize / (2 * Camera.orthographicSize);
                Camera.transform.position += Camera.transform.up * ((lowestSolePixel - FootViewport * SurfaceSize) / pixelsPerUnit);
            }
            Schedule.ResetCharacter(); BeginGait();
            int pixels = Mathf.Max(128, Mathf.RoundToInt(SurfaceSize * settings.RenderScale));
            renderTarget = new RenderTexture(pixels, pixels, 24, RenderTextureFormat.ARGB32) { name = "MiniMSAA_" + token,
                antiAliasing = settings.MsaaSamples, filterMode = FilterMode.Bilinear };
            renderTarget.Create();
            for (int i = 0; i < readTargets.Length; i++)
            {
                readTargets[i] = new RenderTexture(SurfaceSize, SurfaceSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = "MiniResolved_" + token + "_" + i, filterMode = FilterMode.Bilinear };
                readTargets[i].Create();
            } Camera.targetTexture = renderTarget; Camera.enabled = false;
            presentationMaterial = new Material(Resources.Load<Shader>("UmaDesktop/LayeredAlpha"));
            presentationMaterial.SetFloat("_Fxaa", settings.UseFxaa ? 1 : 0);
            renderRequest.destination = renderTarget;
            Overlay.Initialize(SurfaceSize, settings.AlwaysOnTop, "UmaDesktop · " + DesktopCharacterNames.Get(character.Entry.Id,character.Entry.Name,settings.UiLanguage) + " · " + token);
        }

        public void BeginGait(bool forceRun = false)
        {
            if (forceRun) Schedule.ForceGait(MiniBehavior.Run);
            AutomaticSpecial=false;SpecialRetryDistance=0;DetachedInteractionRemaining=0;
            Interaction = false; PartnerToken = -1; SpecialDone = false;
            var entry = Schedule.Behavior == MiniBehavior.Run ? runs.Next() : walks.Next();
            Expressions.Select(Schedule.Behavior);Player.Play(entry, true);Expressions.Apply(); GaitsStarted++;
            if(gaitFloors.ContainsKey(entry.Name))groundWorldY=gaitFloors[entry.Name];
            MoveSpeed = (Schedule.Behavior == MiniBehavior.Run ? settings.RunPixelsPerSecond : settings.WalkPixelsPerSecond) * Height / (settings.CharacterHeightPixels * settings.CharacterScale) * (Schedule.Behavior==MiniBehavior.Run ? RunSpeedFactor : WalkSpeedFactor);
            PlaybackRate = GaitGroundSpeed.TryGetValue(entry.Name, out float groundSpeed) && groundSpeed > 5 ? Mathf.Clamp(MoveSpeed / groundSpeed, 0.25f, 3) : 1;
            log("Gait token=" + Token + " " + Schedule.Behavior + " speed=" + MoveSpeed.ToString("F1") + " animationRate=" + PlaybackRate.ToString("F2") + " " + entry.Name);
        }
        public void Resume() { Schedule.ResumeLocomotion(); BeginGait(Phase == DesktopPhase.Exiting); }
        public void BeginSpecial(bool automatic=false)
        {
            if (specials == null) return;
            DetachedInteractionRemaining=0;Interaction=false;PartnerToken=-1;
            AutomaticSpecial=automatic;SpecialRetryDistance=0;if(automatic)AutomaticSpecials++;
            var main = specials.Next(); var start = catalog.Companion(main, "_s"); var end = catalog.Companion(main, "_e");
            sequence.Clear(); if (start != null) sequence.Add(start); sequence.Add(main); if (end != null) sequence.Add(end);
            Schedule.RequestSpecial(); Interaction = false; PartnerToken = -1; SpecialElapsed = 0;
            SpecialDuration = sequence.Sum(e => Player.GetDuration(e));
            SpecialCutoff = (float)DesktopChoice.SpecialCutoff(SpecialDuration, settings.SpecialEndTrimSeconds);
            part = 0; SpecialDone = false; PlaybackRate = 1; SpecialsStarted++;
            Expressions.Select(MiniBehavior.Special);Player.Play(sequence[0], false);Expressions.Apply();
            log("Special token=" + Token + " total=" + SpecialDuration.ToString("F3") + " cutoff=" + SpecialCutoff.ToString("F3") + " " + main.Name);
        }
        public void BeginInteraction(UmaDatabaseEntry clip, int partner)
        {
            AutomaticSpecial=false;SpecialRetryDistance=0;
            DetachedInteractionRemaining=0;Interaction = true; PartnerToken = partner; PlaybackRate = 1; SpecialElapsed = 0;
            Vector2 interval=PairFootprint(clip,Direction);InteractionHalfWidth=(interval.y-interval.x)*0.5f;InteractionOffset=(interval.y+interval.x)*0.5f;
            facingYaw=GroundYaw;Model.transform.rotation=Quaternion.Euler(0,facingYaw,0);
            Expressions.Select(MiniBehavior.Special);Player.Play(clip, false);Expressions.Apply(); SpecialDuration = Player.Duration;
        }
        public void Step(float delta, bool allowAutomaticSpecial=true, float travelLimit=float.PositiveInfinity)
        {
            PreviousX = X;MovementFraction=1;
            if(MotionHeld){MovementFraction=0;ConstrainGroundFootprint();ApplyGroundProjection();PlaceWindow();return;}
            if(DetachedInteractionRemaining>0){DetachedInteractionRemaining-=delta;if(DetachedInteractionRemaining<=0)Resume();}
            if (!Interaction && Behavior == MiniBehavior.Special)
            {
                SpecialElapsed += delta;
                if (SpecialElapsed >= SpecialCutoff)
                {
                    SpecialDone = true; SpecialsTrimmed++;
                    log("Special cutoff token=" + Token + " elapsed=" + SpecialElapsed.ToString("F3") + " total=" + SpecialDuration.ToString("F3"));
                    Resume();
                }
                else if (Player.Finished && ++part < sequence.Count) {Player.Play(sequence[part], false);Expressions.Apply();}
            }
            else if (!Interaction && Phase == DesktopPhase.Active)
            {
                MiniBehavior before = Behavior;
                Schedule.Tick(delta, HasSpecial, false, allowAutomaticSpecial && SpecialRetryDistance<=0);
                if(Schedule.SpecialPending && HasSpecial && SpecialRetryDistance<=0 && !allowAutomaticSpecial)
                {SpecialRetryDistance=Mathf.Max(40,Height*0.2f);SpecialDeferrals++;log("Special deferred token="+Token+" retryDistance="+SpecialRetryDistance.ToString("F1"));}
                if (Behavior != before) { if (Behavior == MiniBehavior.Special) BeginSpecial(true); else BeginGait(); }
            }
            if (!Interaction && Behavior != MiniBehavior.Special)
            {
                float distance=Mathf.Min(MoveSpeed*delta,Mathf.Max(0,travelLimit));
                MovementFraction=MoveSpeed*delta>0 ? distance/(MoveSpeed*delta) : 1;
                if(Ground!=null){float actual=MoveGround(distance,delta);if(distance>0)MovementFraction*=actual/distance;distance=actual;}else X += Direction * distance;
                SpecialRetryDistance=Mathf.Max(0,SpecialRetryDistance-distance);
                float margin = Height * 0.32f;
                if (Ground==null && Phase == DesktopPhase.Entering && X >= Overlay.ScreenArea.xMin + margin && X <= Overlay.ScreenArea.xMax - margin)
                { Phase = DesktopPhase.Active; log("Entered token=" + Token); }
                if (Ground==null && Phase == DesktopPhase.Active)
                {
                    float left = Overlay.ScreenArea.xMin + margin, right = Overlay.ScreenArea.xMax - margin;
                    if (X < left || X > right) { X = Mathf.Clamp(X, left, right); Direction *= -1; }
                }
            }
            ConstrainGroundFootprint();ApplyGroundProjection();
            Overlay.Opacity = Opacity*PortalOpacity;
            PlaceWindow();
        }
        public void TickPose(float delta)
        {
            if (Disposed) return;
            if(MotionHeld){ConstrainGroundFootprint();ApplyGroundProjection();return;}
            Player.Tick(delta * ((Behavior == MiniBehavior.Special || Interaction) ? 1 : PlaybackRate*MovementFraction));
            Expressions.Apply();
            FreezePosition();
            float yaw = !Interaction && Behavior == MiniBehavior.Special ? 0 : GroundYaw;
            if(DesktopPlane && settings.Avoid3DCollisions && !Interaction && Behavior!=MiniBehavior.Special)
                yaw=(float)desktopHeading.Filter(yaw);
            else desktopHeading.Reset(yaw);
            facingYaw = Mathf.LerpAngle(facingYaw, yaw, 1 - Mathf.Exp(-12 * delta));
            Model.transform.rotation = Quaternion.Euler(0, facingYaw, 0);
            ConstrainGroundFootprint();ApplyGroundProjection();
        }
        public void BeginExit()
        {
            Phase = DesktopPhase.Exiting; Direction = X < Overlay.ScreenArea.center.x ? -1 : 1;
            Interaction = false; PartnerToken = -1; if(Ground!=null)Ground.Retire();BeginGait(true);
        }
        public bool FullyOutside { get { if(Ground!=null)return Ground.Stage==4;return X < Overlay.ScreenArea.xMin - SurfaceSize * 0.5f - 8 || X > Overlay.ScreenArea.xMax + SurfaceSize * 0.5f + 8; } }
        public void PlaceWindow()
        {
            Overlay.Move(Mathf.RoundToInt(X - SurfaceSize * 0.5f), Mathf.RoundToInt(GroundY - SurfaceSize * (1 - CanvasFootViewport)));
        }
        private void FreezePosition()
        {
            if (Model.Position == null) return;
            Vector3 p = Model.Position.localPosition; p.x = origin.x; p.z = origin.z; Model.Position.localPosition = p;
        }
        private void FrameCharacter()
        {
            var renderers = Model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is LineRenderer) && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) throw new InvalidDataException("Character has no visible renderer.");
            Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Height = Mathf.Min(settings.CharacterHeightPixels * (Character.Entry.Id == 1028 ? settings.HishiAkebonoScale : settings.CharacterScale), settings.WindowSize * 0.78f)*SurfaceSize/settings.WindowSize;
            Camera.orthographic = true; Camera.orthographicSize = Mathf.Max(0.2f, bounds.size.y * SurfaceSize / (2 * Height));
            Camera.nearClipPlane = 0.01f; Camera.farClipPlane = 30;
            Vector3 offset = Quaternion.Euler(settings.CameraElevation, 0, 0) * new Vector3(0, 0, 5); offset.y = Mathf.Abs(offset.y);
            Camera.transform.position = bounds.center + offset; Camera.transform.LookAt(bounds.center);
            float correction = (Camera.WorldToViewportPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y - FootViewport) * Camera.orthographicSize * 2;
            Camera.transform.position += Camera.transform.up * correction;
        }
        private void MeasureShoeBounds()
        {
            var feet = Model.GetComponentsInChildren<Transform>().Where(t => t.name == "Ankle_L" || t.name == "Ankle_R").ToArray();
            var body = Model.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name == "M_Body");
            groundFeet=feet;
            if (feet.Length != 2 || body == null) return;
            var baked = new Mesh();
            body.BakeMesh(baked);
            var vertices = baked.vertices;
            Matrix4x4 toModel = Model.transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
            var points = vertices.Select(v => toModel.MultiplyPoint3x4(v)).ToArray();
            if (points.Length == 0) { UnityEngine.Object.Destroy(baked); return; }
            float low = points.Min(v => v.y), high = points.Max(v => v.y);
            var shoes = points.Where(v => v.y <= low + (high - low) * 0.14f).ToArray();
            var bounds = new Bounds[2]; var initialized = new bool[2];
            foreach (Vector3 point in shoes)
            {
                Vector3 world = Model.transform.TransformPoint(point);
                int f = (world - feet[0].position).sqrMagnitude <= (world - feet[1].position).sqrMagnitude ? 0 : 1;
                Vector3 local = feet[f].InverseTransformPoint(world);
                if (!initialized[f]) { bounds[f] = new Bounds(local, Vector3.zero); initialized[f] = true; }
                else bounds[f].Encapsulate(local);
            }
            if (initialized[0] && initialized[1]) footBounds = bounds;
            UnityEngine.Object.Destroy(baked);
        }

        private void CalibrateStride(UmaDatabaseEntry entry)
        {
            var feet = Model.GetComponentsInChildren<Transform>().Where(t => t.name == "Ankle_L" || t.name == "Ankle_R").ToArray();
            if (feet.Length != 2) return;
            bool isGroundGait=catalog.Walks.Contains(entry)||catalog.Runs.Contains(entry);
            Player.Play(entry, false);Player.Seek(0); const int samples = 60;
            float step = Player.Duration / samples;
            var positions = new Vector3[2, samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                if (i > 0) Player.Tick(step); FreezePosition();
                if(isGroundGait){sampledPoseFloor=float.PositiveInfinity;gaitFootRadius=Mathf.Max(gaitFootRadius,FootRadiusNow(true));
                    if(!gaitFloors.ContainsKey(entry.Name))gaitFloors[entry.Name]=sampledPoseFloor;else gaitFloors[entry.Name]=Mathf.Min(gaitFloors[entry.Name],sampledPoseFloor);}
                if(i%5==0 || entry.Name.Contains("_004_01_")) MeasureMotionEnvelope(entry);
                for (int f = 0; f < 2; f++)
                {
                    positions[f, i] = Model.transform.InverseTransformPoint(feet[f].position);
                    Matrix4x4 ankleToModel = Model.transform.worldToLocalMatrix * feet[f].localToWorldMatrix;
                    if (footBounds != null)
                    {
                        Bounds shoe = footBounds[f];
                        Vector3 cameraUp = Camera.transform.up, cameraPosition = Camera.transform.position;
                        float shoePixelsPerUnit = SurfaceSize / (2 * Camera.orthographicSize);
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 local = shoe.center + Vector3.Scale(shoe.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                            Vector3 modelPoint = ankleToModel.MultiplyPoint3x4(local);
                            for (int facing = -1; facing <= 1; facing += 2)
                            {
                                Vector3 point = Model.transform.position + Quaternion.Euler(0, facing * settings.FacingAngle, 0) * modelPoint;
                                float pixel = SurfaceSize * 0.5f + Vector3.Dot(cameraUp, point - cameraPosition) * shoePixelsPerUnit;
                                lowestSolePixel = Mathf.Min(lowestSolePixel, pixel - 4);
                            }
                        }
                    }
                }
            }
            var speeds = new List<float>();
            for (int f = 0; f < 2; f++)
            {
                float floor = Enumerable.Range(0, samples + 1).Min(i => positions[f, i].y);
                float ceiling = Enumerable.Range(0, samples + 1).Max(i => positions[f, i].y);
                for (int i = 1; i <= samples; i++)
                    if (positions[f, i].y < floor + Mathf.Max(0.005f, (ceiling - floor) * 0.2f) && positions[f, i - 1].y < floor + Mathf.Max(0.005f, (ceiling - floor) * 0.2f))
                    {
                        float velocity = Mathf.Abs(positions[f, i].z - positions[f, i - 1].z) / step;
                        if (velocity > 0.001f) speeds.Add(velocity);
                    }
            }
            log("Envelope token="+Token+" radius="+envelopeRadius.ToString("F3")+" clip="+entry.Name);
            if (speeds.Count == 0) return;
            speeds.Sort();
            float pixelsPerUnit = SurfaceSize / (2 * Camera.orthographicSize);
            GaitGroundSpeed[entry.Name] = speeds[speeds.Count / 2] * pixelsPerUnit * Mathf.Sin(settings.FacingAngle * Mathf.Deg2Rad);
        }
        private void MeasureEnvelopeInterval(out float halfWidth,out float offset)
        {
            float pivot=(Camera.WorldToViewportPoint(Model.transform.position).x-0.5f)*SurfaceSize;
            float radius=Mathf.Max(Height*0.28f,envelopeRadius*SurfaceSize/(2*Camera.orthographicSize))*1.15f+16;
            float left=Mathf.Max(-SurfaceSize*0.5f,pivot-radius),right=Mathf.Min(SurfaceSize*0.5f,pivot+radius);
            if(left>=right) {halfWidth=SurfaceSize*0.5f;offset=0;return;}
            halfWidth=(right-left)*0.5f;offset=(right+left)*0.5f;
        }
        private void MeasureMotionEnvelope(UmaDatabaseEntry entry)
        {
            // Bake only during loading. Include body, hair and rigid facial/accessory
            // meshes at sampled poses; radial bounds cover all facing/turn angles.
            foreach(var renderer in Model.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled || renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                Bounds bounds;Matrix4x4 matrix=renderer.localToWorldMatrix;
                var skin=renderer as SkinnedMeshRenderer;
                if(skin!=null) {skin.BakeMesh(envelopeMesh);envelopeMesh.RecalculateBounds();bounds=envelopeMesh.bounds;}
                else {var filter=renderer.GetComponent<MeshFilter>();if(filter==null || filter.sharedMesh==null)continue;bounds=filter.sharedMesh.bounds;}
                for(int corner=0;corner<8;corner++)
                {
                    var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    var relative=matrix.MultiplyPoint3x4(point)-Model.transform.position;
                    envelopeRadius=Mathf.Max(envelopeRadius,Mathf.Sqrt(relative.x*relative.x+relative.z*relative.z));
                    envelopeTop=Mathf.Max(envelopeTop,relative.y);envelopeBottom=Mathf.Min(envelopeBottom,relative.y);
                    if(entry.Name.Contains("_004_01_")) MeasurePairPoint(entry.Name,relative);
                }
            }
        }
        public void CaptureExpressionPortrait(string path)
        {
            Vector3 oldPosition=Camera.transform.position;Quaternion oldRotation=Camera.transform.rotation,oldFacing=Model.transform.rotation;
            float oldSize=Camera.orthographicSize;
            RenderTexture resolved=null;Texture2D image=null;Mesh mesh=new Mesh();
            try
            {
                Model.transform.rotation=Quaternion.identity;
                var face=Model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(renderer=>renderer.name=="M_Face");
                face.BakeMesh(mesh);var points=mesh.vertices.Select(v=>face.transform.TransformPoint(v)).ToArray();
                var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
                Vector3 target=bounds.center+Vector3.up*bounds.size.y*.1f;
                Camera.orthographicSize=bounds.size.y*1.1f;Camera.transform.position=target+Vector3.forward*5;Camera.transform.LookAt(target);
                Expressions.Apply();ApplyOutlineParameters();RenderPipeline.SubmitRenderRequest(Camera,renderRequest);
                resolved=new RenderTexture(SurfaceSize,SurfaceSize,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);resolved.Create();
                Graphics.Blit(renderTarget,resolved,presentationMaterial);
                var request=AsyncGPUReadback.Request(resolved,0,TextureFormat.BGRA32);request.WaitForCompletion();
                if(request.hasError)throw new InvalidOperationException("Expression portrait readback failed.");
                var pixels=request.GetData<byte>().ToArray();
                for(int i=0;i<pixels.Length;i+=4)
                {
                    int alpha=pixels[i+3];if(alpha==0){pixels[i]=pixels[i+1]=pixels[i+2]=0;continue;}
                    if(alpha<255)for(int channel=0;channel<3;channel++)pixels[i+channel]=(byte)Mathf.Min(255,(pixels[i+channel]*255+alpha/2)/alpha);
                }
                image=new Texture2D(SurfaceSize,SurfaceSize,TextureFormat.BGRA32,false);image.LoadRawTextureData(pixels);image.Apply(false);
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                Camera.transform.SetPositionAndRotation(oldPosition,oldRotation);Camera.orthographicSize=oldSize;Model.transform.rotation=oldFacing;
                if(resolved!=null){resolved.Release();UnityEngine.Object.Destroy(resolved);}if(image!=null)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(mesh);
            }
        }
        public void ApplyOutlineParameters()
        {
            // MiniCharaBody's DX11 vertex program expands clip XY by:
            // projection(tangent) * 0.0028 * width * (0.5 * fov + 0.75).
            // Convert a desired OUTPUT-pixel width for this orthographic camera.
            // Its depth offset is independent of width and used to pull backfaces
            // over the face/clothes when the perspective-viewer default was reused.
            Shader.SetGlobalFloat("_GlobalCameraFov", 0);
            Shader.SetGlobalFloat("_GlobalOutlineOffset", 0);
            Shader.SetGlobalFloat("_GlobalOutlineWidth", settings.OutlineWidth * 2 * Camera.orthographicSize / (SurfaceSize * 0.0028f * 0.75f));
        }
        public void CaptureAuditPose(UmaDatabaseEntry entry, float time, float yaw, string directory, string key)
        {
            Player.Play(entry, false); Player.Seek(time); FreezePosition();
            Model.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var resolved = new RenderTexture(SurfaceSize, SurfaceSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            resolved.Create();
            var image = new Texture2D(SurfaceSize, SurfaceSize, TextureFormat.BGRA32, false);
            try
            {
                foreach (string variant in new[] { "legacy", "fixed", "none" })
                {
                    if (variant == "legacy")
                    { Shader.SetGlobalFloat("_GlobalCameraFov", 30); Shader.SetGlobalFloat("_GlobalOutlineOffset", 1); Shader.SetGlobalFloat("_GlobalOutlineWidth", 0.45f); }
                    else
                    { ApplyOutlineParameters(); if (variant == "none") Shader.SetGlobalFloat("_GlobalOutlineWidth", 0); }
                    RenderPipeline.SubmitRenderRequest(Camera, renderRequest);
                    Graphics.Blit(renderTarget, resolved, presentationMaterial);
                    var readback = AsyncGPUReadback.Request(resolved, 0, TextureFormat.BGRA32);
                    readback.WaitForCompletion();
                    if (readback.hasError) throw new InvalidOperationException("Visual audit GPU readback failed.");
                    image.LoadRawTextureData(readback.GetData<byte>()); image.Apply(false);
                    File.WriteAllBytes(Path.Combine(directory, key + "-" + variant + ".png"), image.EncodeToPNG());
                }
            }
            finally { ApplyOutlineParameters(); resolved.Release(); UnityEngine.Object.Destroy(resolved); UnityEngine.Object.Destroy(image); }
        }
        public void RenderFrame()
        {
            if (Disposed || pendingReads >= readTargets.Length) return;
            int slot = Array.FindIndex(inFlight, v => !v);
            int serial = ++submittedFrame;
            ApplyOutlineParameters();
            RenderPipeline.SubmitRenderRequest(Camera, renderRequest);
            Graphics.Blit(renderTarget, readTargets[slot], presentationMaterial);
            inFlight[slot] = true; pendingReads++;
            AsyncGPUReadback.Request(readTargets[slot], 0, TextureFormat.BGRA32, request => {
                inFlight[slot] = false; pendingReads--;
                if (Disposed) { if (pendingReads == 0) CleanupTargets(); return; }
                if (serial <= presentedFrame) return;
                presentedFrame = serial;
                if (request.hasError) { log("ERROR GPU readback token=" + Token); return; }
                try
                {
                    var data = request.GetData<byte>();
                    Overlay.Opacity = Opacity*PortalOpacity; Overlay.Present(data, settings.FlipReadbackY);
                    if (CaptureFile != null)
                    {
                        var image = new Texture2D(SurfaceSize, SurfaceSize, TextureFormat.BGRA32, false);
                        image.LoadRawTextureData(data); image.Apply(false);
                        File.WriteAllBytes(CaptureFile, image.EncodeToPNG()); UnityEngine.Object.Destroy(image); CaptureFile = null;
                    }
                }
                catch (Exception exception) { log("ERROR present token=" + Token + " " + exception); }
            });
        }
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (Camera != null) { Camera.enabled = false; UnityEngine.Object.Destroy(Camera.gameObject); }
            Overlay.Dispose(); Player.Dispose();
            var materials = Model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var avatar = Model.UmaAnimator == null ? null : Model.UmaAnimator.avatar; var controller = Model.OverrideController;
            UnityEngine.Object.Destroy(Model.gameObject);
            foreach (var material in materials) UnityEngine.Object.Destroy(material);
            if (avatar != null) UnityEngine.Object.Destroy(avatar);
            if (controller != null) UnityEngine.Object.Destroy(controller);
            if (pendingReads == 0) CleanupTargets();
        }
        private void CleanupTargets()
        {
            if (cleaned) return; cleaned = true;
            if (presentationMaterial != null) UnityEngine.Object.Destroy(presentationMaterial);
            foreach (var target in new[] { renderTarget }.Concat(readTargets)) if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
        }
    }
}
