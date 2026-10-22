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
        private bool pairSpacingAudit;
        [Serializable] private sealed class PairSpacingCase
        {public int a,b,samples;public string motion;public float separation,requiredGap,minimumRenderedGap;}
        [Serializable] private sealed class PairSpacingReport
        {public bool Passed;public List<PairSpacingCase> cases=new List<PairSpacingCase>();public string failure;}
        private IEnumerator RunPairSpacingAudit()
        {
            string directory=Path.Combine(logDirectory,"PairSpacingAudit");Directory.CreateDirectory(directory);
            var report=new PairSpacingReport {Passed=true};
            foreach(int secondId in new[]{1002,1028})
            {
                foreach(var existing in actors.ToArray()) {actors.Remove(existing);existing.Dispose();}
                foreach(int id in new[]{1001,secondId})
                {
                    var character=catalog.Characters.First(c=>c.Entry.Id==id);
                    var outfit=character.Costumes.FirstOrDefault(c=>c.Id=="00") ?? character.Costumes.First(c=>c.Category==0);
                    yield return Spawn(character,outfit.Id);
                }
                AsyncGPUReadback.WaitAllRequests();
                foreach(var motion in pairMotions)
                {
                    try
                    {
                        ArrangeAuditPair();var a=actors[0];var b=actors[1];StartPair(a,b,motion);
                        if(!a.Interaction || !b.Interaction) throw new InvalidOperationException("Pair failed to reserve standing positions.");
                        var sample=new PairSpacingCase {a=a.Character.Entry.Id,b=b.Character.Entry.Id,motion=motion.Name,
                            separation=b.X-a.X,requiredGap=Mathf.Max(28,(a.Height+b.Height)*0.045f),minimumRenderedGap=float.PositiveInfinity};
                        float duration=Mathf.Min(a.SpecialDuration,b.SpecialDuration);
                        for(int frame=0;frame<=60;frame++)
                        {
                            float time=duration*frame/60;
                            string key=secondId+"-"+motion.Name+"-"+frame.ToString("D2");
                            var left=a.CapturePairHorizontal(time,frame%15==0?Path.Combine(directory,key+"-A.png"):null);
                            var right=b.CapturePairHorizontal(time,frame%15==0?Path.Combine(directory,key+"-B.png"):null);
                            sample.minimumRenderedGap=Mathf.Min(sample.minimumRenderedGap,right.x-left.y);sample.samples++;
                        }
                        if(sample.minimumRenderedGap<sample.requiredGap-2) throw new InvalidOperationException("Rendered pair clearance too small: "+sample.minimumRenderedGap);
                        report.cases.Add(sample);CancelPair(a);
                    }
                    catch(Exception ex) {report.Passed=false;report.failure=ex.ToString();break;}
                    yield return null;
                }
                if(!report.Passed) break;
            }
            File.WriteAllText(Path.Combine(directory,"pair-spacing-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.Passed?0:1);
        }
    }
    public sealed partial class DesktopActor
    {
        public Vector2 CapturePairHorizontal(float time,string file)
        {
            Player.Seek(time);FreezePosition();Expressions.Apply();ApplyOutlineParameters();
            Model.transform.rotation=Quaternion.Euler(0,-Direction*settings.FacingAngle,0);
            RenderPipeline.SubmitRenderRequest(Camera,renderRequest);Graphics.Blit(renderTarget,readTargets[0],presentationMaterial);
            var request=AsyncGPUReadback.Request(readTargets[0],0,TextureFormat.BGRA32);request.WaitForCompletion();
            if(request.hasError) throw new InvalidOperationException("Pair audit GPU readback failed.");
            var pixels=request.GetData<byte>();int min=SurfaceSize,max=-1;
            for(int y=0;y<SurfaceSize;y++) for(int x=0;x<SurfaceSize;x++)
                if(pixels[(y*SurfaceSize+x)*4+3]>=8) {min=Mathf.Min(min,x);max=Mathf.Max(max,x);}
            if(max<min) throw new InvalidOperationException("Pair audit rendered an empty character.");
            if(file!=null)
            {
                var image=new Texture2D(SurfaceSize,SurfaceSize,TextureFormat.BGRA32,false);
                image.LoadRawTextureData(pixels);image.Apply(false);File.WriteAllBytes(file,image.EncodeToPNG());UnityEngine.Object.Destroy(image);
            }
            return new Vector2(X-SurfaceSize*0.5f+min,X-SurfaceSize*0.5f+max+1);
        }
    }
}
