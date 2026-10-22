using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UmaDesktop
{
    public sealed partial class DesktopActor
    {
        public void SetModelSize(float value)
        {
            int oldSide=SurfaceSize;modelSizeScale=value;int side=SurfaceSize;
            if(oldSide==side)return;
            float ratio=(float)side/oldSide;
            // Preserve the visible horizontal centre and the physical ground anchor.
            if(Ground==null)X+=SoloOffset*(1-ratio);Height*=ratio;SoloHalfWidth*=ratio;SoloOffset*=ratio;
            InteractionHalfWidth*=ratio;InteractionOffset*=ratio;SpecialRetryDistance*=ratio;
            MoveSpeed*=ratio;
            foreach(string key in GaitGroundSpeed.Keys.ToArray())GaitGroundSpeed[key]*=ratio;
            foreach(var ranges in pairFootprints.Values)
                for(int i=0;i<ranges.Length;i++)ranges[i]*=ratio;
            if(Ground==null && Phase==DesktopPhase.Active)
                X=Mathf.Clamp(X,Overlay.ScreenArea.xMin+Height*0.32f,Overlay.ScreenArea.xMax-Height*0.32f);
            Camera.targetTexture=null;renderTarget.Release();UnityEngine.Object.Destroy(renderTarget);
            for(int i=0;i<readTargets.Length;i++)
            {
                readTargets[i].Release();UnityEngine.Object.Destroy(readTargets[i]);inFlight[i]=false;
                readTargets[i]=new RenderTexture(side,side,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                    {name="MiniResolved_"+Token+"_"+i,filterMode=FilterMode.Bilinear};readTargets[i].Create();
            }
            int pixels=Mathf.Max(128,Mathf.RoundToInt(side*settings.RenderScale));
            renderTarget=new RenderTexture(pixels,pixels,24,RenderTextureFormat.ARGB32)
                {name="MiniMSAA_"+Token,antiAliasing=settings.MsaaSamples,filterMode=FilterMode.Bilinear};renderTarget.Create();
            Camera.targetTexture=renderTarget;renderRequest.destination=renderTarget;
            Overlay.Resize(side);PlaceWindow();
        }
    }
}
