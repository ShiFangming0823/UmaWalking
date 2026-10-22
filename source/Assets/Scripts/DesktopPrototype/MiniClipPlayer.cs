using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UmaDesktop
{
    // Manual time makes a looping source clip play exactly once for a special.
    // Clones strip game AnimationEvents so they cannot invoke viewer UI/actions.
    public sealed class MiniClipPlayer : IDisposable
    {
        private readonly Animator animator;
        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationLayerMixerPlayable layers;
        private AnimationClipPlayable facePlayable, earPlayable;
        public int FacialEvaluations { get; private set; }
        private readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        private AnimationClipPlayable current, previous;
        private AnimationClip currentClip, previousClip;
        private AnimationClip faceClip, earClip;
        private float time, previousTime, blendTime, blendDuration = 0.10f;
        private bool loop, previousLoop, disposed;
        public string Name { get; private set; }
        public float TimeSeconds {get{return time;}}
        public float Duration { get { return currentClip == null ? 0 : currentClip.length; } }
        public bool Finished { get { return !loop && time >= Duration; } }

        public MiniClipPlayer(UmaContainerCharacter character)
        {
            animator = character.UmaAnimator;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            graph = PlayableGraph.Create("UmaDesktopMini");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            layers = AnimationLayerMixerPlayable.Create(graph, 3);
            graph.Connect(mixer, 0, layers, 0);
            layers.SetInputWeight(0, 1);
            AnimationPlayableOutput.Create(graph, "Mini", animator).SetSourcePlayable(layers);
            graph.Play();
        }

        private AnimationClip Load(UmaDatabaseEntry entry)
        {
            if (clips.TryGetValue(entry.Name, out AnimationClip cached)) return cached;
            AnimationClip source = entry.Get<AnimationClip>();
            if (source == null || source.length <= 0) throw new InvalidOperationException("Invalid animation: " + entry.Name);
            var clip = UnityEngine.Object.Instantiate(source);
            clip.name = entry.Name;
            clip.events = new AnimationEvent[0];
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            clips.Add(entry.Name, clip);
            return clip;
        }

        public float GetDuration(UmaDatabaseEntry entry) { return Load(entry).length; }

        public void Preload(UmaDatabaseEntry entry)
        {
            Load(entry); OptionalTrack(entry, "_face"); OptionalTrack(entry, "_ear");
        }

        public void Play(UmaDatabaseEntry entry, bool repeat)
        {
            // Load first so a broken clip does not destroy a working animation.
            AnimationClip next = Load(entry);
            if (previous.IsValid())
            {
                graph.Disconnect(mixer, 0);
                graph.DestroyPlayable(previous);
            }
            previous = current;
            previousClip = currentClip;
            previousTime = time;
            previousLoop = loop;
            if (previous.IsValid())
            {
                graph.Disconnect(mixer, 1);
                graph.Connect(previous, 0, mixer, 0);
            }
            currentClip = next;
            current = AnimationClipPlayable.Create(graph, next);
            current.SetApplyFootIK(false);
            current.SetApplyPlayableIK(false);
            graph.Connect(current, 0, mixer, 1);
            time = 0;
            blendTime = previous.IsValid() ? 0 : blendDuration;
            loop = repeat;
            Name = entry.Name;
            faceClip = OptionalTrack(entry, "_face");
            earClip = OptionalTrack(entry, "_ear");
            SetTrack(ref facePlayable, faceClip, 1);
            SetTrack(ref earPlayable, earClip, 2);
            Tick(0);
        }

        private void SetTrack(ref AnimationClipPlayable playable, AnimationClip clip, int slot)
        {
            if (playable.IsValid())
            {
                graph.Disconnect(layers, slot);
                graph.DestroyPlayable(playable);
            }
            layers.SetInputWeight(slot, clip == null ? 0 : 1);
            if (clip == null) { playable = default(AnimationClipPlayable); return; }
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            graph.Connect(playable, 0, layers, slot);
        }

        private AnimationClip OptionalTrack(UmaDatabaseEntry body, string suffix)
        {
            string name = body.Name.Replace("/body/", "/facial/") + suffix;
            if (!UmaViewerMain.Instance.AbList.TryGetValue(name, out UmaDatabaseEntry entry) || !System.IO.File.Exists(entry.Path))
                return null;
            try { return Load(entry); }
            catch (Exception exception) { Debug.LogWarning("[UmaDesktop] Optional facial track skipped: " + exception.Message); return null; }
        }

        public void Seek(float seconds)
        {
            time = Mathf.Max(0, seconds); blendTime = blendDuration; Tick(0);
        }

        public void Tick(float delta)
        {
            if (!current.IsValid() || disposed) return;
            time += delta;
            blendTime += delta;
            current.SetTime(ClipTime(time, currentClip, loop));
            if (previous.IsValid())
            {
                previousTime += delta;
                previous.SetTime(ClipTime(previousTime, previousClip, previousLoop));
            }
            float weight = Mathf.Clamp01(blendTime / blendDuration);
            mixer.SetInputWeight(0, previous.IsValid() ? 1 - weight : 0);
            mixer.SetInputWeight(1, weight);
            if (facePlayable.IsValid()) facePlayable.SetTime(ClipTime(time, faceClip, loop));
            if (earPlayable.IsValid()) earPlayable.SetTime(ClipTime(time, earClip, loop));
            // Non-legacy game tracks share the Animator stream with the body.
            // Direct SampleAnimation needs a controller and can emit a warning every frame.
            graph.Evaluate(0);
            if (facePlayable.IsValid() || earPlayable.IsValid()) FacialEvaluations++;
        }

        private static float ClipTime(float t, AnimationClip clip, bool repeat)
        {
            return repeat ? t % clip.length : Mathf.Min(t, Mathf.Max(0, clip.length - 0.0001f));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (graph.IsValid()) graph.Destroy();
            foreach (AnimationClip clip in clips.Values) UnityEngine.Object.Destroy(clip);
            clips.Clear();
        }
    }
}
