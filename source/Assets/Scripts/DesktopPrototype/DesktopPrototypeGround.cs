using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private bool groundEditing;
        private string groundOrder="";
        private void ApplyGround(DesktopGroundConfig config)
        {
            string invalid=GroundMap.Validate(config);
            if(invalid!=null){message=invalid;Record("Ground rejected: "+invalid);return;}
            config=config??new DesktopGroundConfig();
            string copy=JsonUtility.ToJson(config);
            settings.Ground=JsonUtility.FromJson<DesktopGroundConfig>(copy);persisted.Ground=JsonUtility.FromJson<DesktopGroundConfig>(copy);
            DesktopSettings.Save(settingsPath,persisted);
            AsyncGPUReadback.WaitAllRequests();foreach(var pair in pairs.ToArray())EndPair(pair);
            foreach(var a in actors)
            {
                a.AssignGround(settings.Ground,settings.Ground.mode=="Planes"?random.Next(settings.Ground.surfaces.Length):0,random);
                a.Phase=DesktopPhase.Entering;a.Resume();
                if(!a.CustomGround){a.X=a.Direction>0?a.Overlay.ScreenArea.xMin-a.SurfaceSize*.5f:a.Overlay.ScreenArea.xMax+a.SurfaceSize*.5f;}
                a.TickPose(0);a.PlaceWindow();if(!desktopPaused)a.RenderFrame();
            }
            groundOrder="";message=config.mode=="Default"?"已恢复屏幕底部地面。":config.mode=="Line"?"已应用二维直线地面。":"已应用 "+config.surfaces.Length+" 块三维地面。";
            Record("Ground applied mode="+config.mode+" surfaces="+config.surfaces.Length);
        }
        private bool GroundSpecialClear(DesktopActor a)
        {
            Rect r=a.GroundBounds();
            if(r.xMin<a.Overlay.ScreenArea.xMin || r.xMax>a.Overlay.ScreenArea.xMax || a.PortalOpacity<.99f)return false;
            return actors.All(b=>b==a || !r.Overlaps(b.GroundBounds()));
        }
        private float GroundSafeTravel(DesktopActor a,float requested)
        {
            if(!a.Ground.IsLine) return settings.Avoid3DCollisions?Mathf.Min(requested,a.GroundTravelLimit):requested;
            if(a.Interaction)return requested;
            float allowed=requested;
            foreach(var b in actors)
            {
                if(b==a || !(b.Interaction || b.AutomaticSpecial && b.Behavior==MiniBehavior.Special))continue;
                Rect other=b.GroundBounds();Rect start=a.GroundBounds();
                Rect end=a.GroundBounds(a.Ground.Preview(allowed));
                Rect swept=Rect.MinMaxRect(Mathf.Min(start.xMin,end.xMin),Mathf.Min(start.yMin,end.yMin),Mathf.Max(start.xMax,end.xMax),Mathf.Max(start.yMax,end.yMax));
                if(!swept.Overlaps(other))continue;
                if(start.Overlaps(other))
                {if(Vector2.Distance(end.center,other.center)>Vector2.Distance(start.center,other.center))continue;}
                allowed=0;a.Ground.Avoid();
            }
            return allowed;
        }
        private void SortGroundWindows()
        {
            if(settings.Ground.mode=="Default")return;
            var ordered=actors.OrderBy(a=>a.GroundY).ThenBy(a=>a.Ordinal).ToArray();
            string key=string.Join(",",ordered.Select(a=>a.Token.ToString()).ToArray());
            if(key==groundOrder)return;groundOrder=key;
            foreach(var a in ordered)a.Overlay.RaiseWithinDesktop();
        }
    }
}
