using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private string optionsAck;
        private void ApplyOptions(Command c)
        {
            if(c.action=="actorLimit")ApplyActorLimit(c.maxActors);
            if(c.action=="avoidance")
            {settings.Avoid3DCollisions=persisted.Avoid3DCollisions=c.avoid3DCollisions;foreach(var a in actors)if(a.Ground!=null)a.Ground.ClearSteering();}
            if(c.action=="performance")
            {
                if(!new[]{15,30,60,120}.Contains(c.frameRate))throw new ArgumentException("Unsupported frame rate");
                AsyncGPUReadback.WaitAllRequests();
                settings.FrameRate=persisted.FrameRate=c.frameRate;
                settings.UseFxaa=persisted.UseFxaa=c.antiAliasing;
                settings.MsaaSamples=persisted.MsaaSamples=c.antiAliasing?4:1;
                var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if(pipeline!=null)pipeline.msaaSampleCount=settings.MsaaSamples;
                foreach(var actor in actors)actor.ApplyAntiAliasing();
                Application.targetFrameRate=EffectivePaused?2:settings.FrameRate;
            }
            if(c.action=="language")
            {
                settings.UiLanguage=persisted.UiLanguage=c.language=="ja"||c.language=="en"?c.language:"zh";
                foreach(var actor in actors)actor.Overlay.SetTitle("UmaDesktop · "+DesktopCharacterNames.Get(actor.Character.Entry.Id,actor.Character.Entry.Name,settings.UiLanguage)+" · "+actor.Token);
            }
            if(c.action=="dataPath")
            {
                string path=Path.GetFullPath(c.dataPath);
                if(!File.Exists(Path.Combine(path,"meta")) || !File.Exists(Path.Combine(path,"master","master.mdb")) || !Directory.Exists(Path.Combine(path,"dat")))
                    throw new DirectoryNotFoundException("Select the folder containing meta, master/master.mdb and dat.");
                // Keep the active resource index on its original directory until restart.
                persisted.DataPath=path;
            }
            DesktopSettings.Save(settingsPath,persisted);if(!string.IsNullOrEmpty(c.requestId))optionsAck=c.requestId;
            Record("Options saved "+c.action+" request="+c.requestId);
        }
        private static string FileHash(string path)
        {using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
        private void ExportAssetPlan()
        {
            var entries=UmaViewerMain.Instance.AbList;
            var selected=new HashSet<string>(StringComparer.Ordinal);
            Action<string> include=null;
            include=name=>{
                if(selected.Contains(name))return;
                if(!entries.TryGetValue(name,out var entry) || !File.Exists(entry.Path))throw new FileNotFoundException("Required mini dependency missing: "+name);
                selected.Add(name);
                foreach(string dependency in (entry.Prerequisites??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))include(dependency);
            };
            foreach(var entry in entries.Values.Where(e=>(e.Name.StartsWith("3d/chara/mini/",StringComparison.Ordinal)||e.Name.StartsWith("3d/motion/mini/",StringComparison.Ordinal)||e.Name=="shader") && File.Exists(e.Path)))include(entry.Name);
            var files=selected.Select(n=>entries[n]).GroupBy(e=>e.Url,StringComparer.Ordinal).Select(g=>g.First()).Select(e=>new DesktopAssetFile {path="dat/"+e.Url.Substring(0,2)+"/"+e.Url,bytes=new FileInfo(e.Path).Length}).ToList();
            foreach(string relative in new[]{"meta","master/master.mdb"})files.Add(new DesktopAssetFile {path=relative,bytes=new FileInfo(Path.Combine(settings.DataPath,relative)).Length});
            var plan=new DesktopAssetPlan {source=settings.DataPath,createdUtc=DateTime.UtcNow.ToString("o"),characters=catalog.Characters.Count,costumes=catalog.Characters.Sum(c=>c.Costumes.Count),files=files.OrderBy(f=>f.path,StringComparer.Ordinal).ToArray(),bytes=files.Sum(f=>f.bytes),metaSha256=FileHash(Path.Combine(settings.DataPath,"meta")),masterSha256=FileHash(Path.Combine(settings.DataPath,"master","master.mdb"))};
            string target=Path.Combine(logDirectory,"asset-plan.json"),temp=target+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(plan,true));if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
            Record("Mini extraction plan: files="+files.Count+" bytes="+plan.bytes);
        }
    }
    public sealed partial class DesktopActor
    {
        public void ApplyAntiAliasing()
        {
            Camera.targetTexture=null;renderTarget.Release();UnityEngine.Object.Destroy(renderTarget);
            int pixels=Mathf.Max(128,Mathf.RoundToInt(SurfaceSize*settings.RenderScale));
            renderTarget=new RenderTexture(pixels,pixels,24,RenderTextureFormat.ARGB32) {name="MiniMSAA_"+Token,antiAliasing=settings.MsaaSamples,filterMode=FilterMode.Bilinear};
            renderTarget.Create();Camera.targetTexture=renderTarget;renderRequest.destination=renderTarget;
            Camera.allowMSAA=settings.MsaaSamples>1;presentationMaterial.SetFloat("_Fxaa",settings.UseFxaa?1:0);
        }
    }
}
