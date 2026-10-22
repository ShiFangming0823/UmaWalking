using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private IEnumerator RunFaceSamplingAudit()
        {
            string output=Path.Combine(logDirectory,"FaceSamplingAudit");Directory.CreateDirectory(output);int count=0;
            foreach(int id in new[]{1076,1001,1105})
            {
                var character=catalog.Characters.First(c=>c.Entry.Id==id);
                var costume=character.Costumes.FirstOrDefault(c=>c.Id=="00")??character.Costumes[0];
                yield return Spawn(character,costume.Id);
                var actor=actors.Single();actor.Phase=DesktopPhase.Active;actor.X=actor.Overlay.ScreenArea.center.x;
                actor.Schedule.ForceGait(MiniBehavior.Walk);actor.BeginGait();actor.Player.Seek(.4f);actor.TickPose(0);
                foreach(string preset in new[]{"WaraiA","WaraiB","WaraiC","WaraiD","WaraiE","SleepA","SleepB","KiriA","KiriB","KiriC","GuruguruA","FightA"})
                {actor.Expressions.SetAuditPreset(preset);actor.TickPose(0);actor.CaptureFaceSampling(output,id+"-"+preset);count++;}
                Remove(actor);yield return null;
            }
            File.WriteAllText(Path.Combine(output,"face-sampling-report.json"),JsonUtility.ToJson(new FaceSamplingResult {Passed=count==36,Cases=count},true));Application.Quit();
        }
        [Serializable] private sealed class FaceSamplingResult {public bool Passed;public int Cases;}
    }
    public sealed partial class DesktopActor
    {
        [Serializable] private sealed class SamplingTexture {public string name,property,filter,wrap,format;public int width,height,mips;public float bias;}
        [Serializable] private sealed class SamplingMaterial {public string renderer,shader;public string[] passes;public SamplingTexture[] textures;}
        [Serializable] private sealed class SamplingReport {public SamplingMaterial[] materials;}
        public void CaptureFaceSampling(string output,string key)
        {
            var renderers=Model.GetComponentsInChildren<Renderer>(true).Where(r=>new[]{"M_Face","M_Eye","M_Mouth","M_Mayu_L","M_Mayu_R","M_Cheek"}.Contains(r.name)).ToArray();
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
            var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).OfType<Texture2D>().Distinct().ToArray();
            var filters=textures.Select(t=>t.filterMode).ToArray();var anis=textures.Select(t=>t.anisoLevel).ToArray();
            var passStates=materials.ToDictionary(m=>m,m=>Enumerable.Range(0,m.passCount).ToDictionary(i=>m.GetPassName(i),i=>m.GetShaderPassEnabled(m.GetPassName(i))));
            var data=renderers.Select(r=>new SamplingMaterial {renderer=r.name,shader=r.sharedMaterial.shader.name,passes=Enumerable.Range(0,r.sharedMaterial.passCount).Select(i=>r.sharedMaterial.GetPassName(i)).ToArray(),textures=r.sharedMaterial.GetTexturePropertyNames().Select(p=>new {p,t=r.sharedMaterial.GetTexture(p) as Texture2D}).Where(x=>x.t!=null).Select(x=>new SamplingTexture {name=x.t.name,property=x.p,filter=x.t.filterMode.ToString(),wrap=x.t.wrapMode.ToString(),format=x.t.format.ToString(),width=x.t.width,height=x.t.height,mips=x.t.mipmapCount,bias=x.t.mipMapBias}).ToArray()}).ToArray();
            File.WriteAllText(Path.Combine(output,key+"-materials.json"),JsonUtility.ToJson(new SamplingReport {materials=data},true));
            try
            {
                foreach(string variant in new[]{"baseline","fixed"})
                {
                    FaceSurface.SetMode(variant);
                    for(int i=0;i<textures.Length;i++){textures[i].filterMode=variant=="bilinear"||variant=="both"?FilterMode.Bilinear:filters[i];textures[i].anisoLevel=variant=="bilinear"||variant=="both"?0:anis[i];}
                    foreach(var renderer in renderers.Where(r=>r.name!="M_Face"))foreach(var material in renderer.sharedMaterials)
                        foreach(var pass in passStates[material])if(pass.Key.IndexOf("Outline",StringComparison.OrdinalIgnoreCase)>=0)material.SetShaderPassEnabled(pass.Key,variant=="no-patch-outline"||variant=="both"?false:pass.Value);
                    Model.transform.rotation=Quaternion.identity;CaptureSamplingFrame(Path.Combine(output,key+"-"+variant+"-native.png"));
                    CaptureExpressionPortrait(Path.Combine(output,key+"-"+variant+"-portrait.png"));
                }
            }
            finally
            {
                FaceSurface.SetMode("fixed");
                for(int i=0;i<textures.Length;i++){textures[i].filterMode=filters[i];textures[i].anisoLevel=anis[i];}
                foreach(var material in materials)foreach(var pass in passStates[material])material.SetShaderPassEnabled(pass.Key,pass.Value);
            }
        }
        private void CaptureSamplingFrame(string path)
        {
            var resolved=RenderTexture.GetTemporary(SurfaceSize,SurfaceSize,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);Texture2D image=null;
            try
            {
                Expressions.Apply();ApplyOutlineParameters();RenderPipeline.SubmitRenderRequest(Camera,renderRequest);Graphics.Blit(renderTarget,resolved,presentationMaterial);
                var readback=AsyncGPUReadback.Request(resolved,0,TextureFormat.BGRA32);readback.WaitForCompletion();if(readback.hasError)throw new InvalidOperationException("Face audit GPU readback failed.");
                image=new Texture2D(SurfaceSize,SurfaceSize,TextureFormat.BGRA32,false);image.LoadRawTextureData(readback.GetData<byte>());image.Apply(false);File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{if(image!=null)UnityEngine.Object.Destroy(image);RenderTexture.ReleaseTemporary(resolved);}
        }
    }
}
