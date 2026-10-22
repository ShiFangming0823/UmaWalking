using System;
using System.Linq;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopActor
    {
        public bool PointerHeld,Dragging,PairHeld;
        public bool MotionHeld {get{return PointerHeld||PairHeld;}}
        public float GrabX,GrabY,DetachedInteractionRemaining;
        public void DragTo(DesktopGroundConfig config,float mouseX,float mouseY,System.Random random)
        {
            float desiredX=mouseX+GrabX,desiredY=mouseY+GrabY;
            if(config.mode=="Default")X=Mathf.Clamp(desiredX,Overlay.ScreenArea.xMin+Height*.32f,Overlay.ScreenArea.xMax-Height*.32f);
            else
            {
                int index;GroundVector point=DesktopGroundDrag.Place(config,Overlay.ScreenArea.width,Overlay.ScreenArea.height,
                    new GroundVector(desiredX-Overlay.ScreenArea.xMin,desiredY-Overlay.ScreenArea.yMin),GroundIndex,out index);
                if(Ground==null || index!=GroundIndex)AssignGround(config,index,random);
                Ground.PlaceScreenForDrag(point.x,point.y);
                UpdateGroundPosition();
            }
            Opacity=1;Overlay.Opacity=1;ConstrainGroundFootprint();ApplyGroundProjection();PlaceWindow();
        }
        public void FinishDrag()
        {
            if(Ground!=null)
            {if(Phase==DesktopPhase.Exiting)Ground.Retire();else {Phase=DesktopPhase.Active;Ground.Avoid();}}
            else if(Phase==DesktopPhase.Entering)Phase=DesktopPhase.Active;
            if(Ground==null && Phase==DesktopPhase.Exiting)Direction=X<Overlay.ScreenArea.center.x?-1:1;
        }
    }
    public sealed partial class DesktopPrototype
    {
        private void ProcessPointerEvents()
        {
            foreach(var a in actors.ToArray())foreach(var e in a.Overlay.TakePointerEvents())
            {
                if(e.Kind==DesktopPointerKind.Down)
                {a.PointerHeld=true;a.Dragging=false;a.GrabX=a.X-e.X;a.GrabY=a.GroundY-e.Y;}
                else if(e.Kind==DesktopPointerKind.Move && a.PointerHeld && e.Dragged)
                {
                    if(!a.Dragging)
                    {
                        a.Dragging=true;a.AutomaticSpecial=false;
                        foreach(var pair in pairs.Where(p=>p.A==a||p.B==a).ToArray())
                        {
                            // Keep the grabbed animation; the other actor can walk away.
                            a.DetachedInteractionRemaining=Mathf.Max(.1f,pair.Until-now);pairs.Remove(pair);
                            var other=pair.A==a?pair.B:pair.A;other.Resume();a.PartnerToken=-1;
                        }
                        Record("Drag begin token="+a.Token);
                    }
                    if(!EffectivePaused)a.DragTo(settings.Ground,e.X,e.Y,random);
                }
                else if(e.Kind==DesktopPointerKind.Up || e.Kind==DesktopPointerKind.Cancel)
                {
                    if(a.Dragging){a.FinishDrag();Record("Drag end token="+a.Token+" floor="+a.GroundIndex);}
                    a.PointerHeld=false;a.Dragging=false;
                }
            }
            foreach(var a in actors)a.PairHeld=false;
            foreach(var pair in pairs)if(pair.A.PointerHeld||pair.B.PointerHeld)
            {pair.A.PairHeld=pair.B.PairHeld=true;pair.Until+=frameDelta;}
        }
    }
}
