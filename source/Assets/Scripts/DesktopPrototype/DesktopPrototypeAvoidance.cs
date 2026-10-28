using System;
using System.Linq;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        [Serializable] private sealed class HeadingAuditRow
        {
            public int token,stage;public float time,yaw,requestedYaw,x,y,radius,movementFraction;
            public string behavior;
        }
        [Serializable] private sealed class HeadingAuditReport
        {public HeadingAuditRow[] samples;}
        private bool headingAuditEnabled;
        private readonly System.Collections.Generic.List<HeadingAuditRow> headingAuditRows=new System.Collections.Generic.List<HeadingAuditRow>();
        private void ControlHeadingAudit(string mode)
        {
            if(!testMode)return;
            if(mode=="start"){headingAuditRows.Clear();headingAuditEnabled=true;}
            else
            {
                headingAuditEnabled=false;
                System.IO.File.WriteAllText(System.IO.Path.Combine(logDirectory,"heading-audit.json"),JsonUtility.ToJson(new HeadingAuditReport {samples=headingAuditRows.ToArray()}));
            }
        }
        private void RecordHeadingAudit()
        {
            if(!testMode || !headingAuditEnabled || frameDelta<=0 || headingAuditRows.Count>60000)return;
            foreach(var a in actors)headingAuditRows.Add(new HeadingAuditRow {token=a.Token,stage=a.Ground==null?-1:a.Ground.Stage,time=now,yaw=a.FacingYaw,requestedYaw=a.RequestedGroundYaw,x=a.X,y=a.GroundY,radius=a.AvoidanceRadius,movementFraction=a.MovementFraction,behavior=a.Behavior.ToString()});
        }
        private void PlanGroundAvoidance(float delta)
        {
            foreach(var actor in actors){actor.GroundTravelLimit=float.PositiveInfinity;actor.AvoidanceState="off";if(actor.Ground!=null)actor.Ground.ClearSteering();}
            if(!settings.Avoid3DCollisions || delta<=0)return;
            var fleet=actors.Where(a=>a.Ground!=null&&!a.Ground.IsLine&&a.Ground.Stage<4).OrderBy(a=>a.Token).ToArray();
            double tilt=Math.Max(.05,Math.Sin((settings.Ground.mode=="Desktop"?GroundRoute.DesktopElevation:settings.CameraElevation)*Math.PI/180));
            Func<GroundVector,GroundVector> plane=p=>new GroundVector(p.x,p.y/tilt);
            var input=new DesktopAvoidance.Agent[fleet.Length];
            for(int i=0;i<fleet.Length;i++)
            {
                var a=fleet[i];var route=a.Ground;var foot=route.Foot;
                var goal=route.Screen(route.Preview(a.MoveSpeed*delta));
                var velocity=DesktopAvoidance.Mul(DesktopAvoidance.Sub(plane(goal),plane(foot)),1/delta);
                bool movable=!a.MotionHeld&&!a.Interaction&&a.Behavior!=MiniBehavior.Special;
                var safe=route.IsDesktop?route.DesktopRegion:DesktopFootprintMath.Region(route.Map,Math.Max(1,a.FootprintRadiusX),Math.Max(1,a.FootprintRadiusY),2);
                input[i]=new DesktopAvoidance.Agent {Id=a.Token,Ground=a.GroundIndex,Partner=a.PartnerToken,Position=plane(foot),Desired=movable?velocity:new GroundVector(),Previous=a.AvoidanceVelocity,Radius=Math.Max(a.FootprintRadiusX,a.Height*.24*a.GroundDepthScale)+6,Movable=movable,Backtrack=route.Stage==0,Steer=route.Stage==1||route.Stage==2,StableSteering=route.IsDesktop,Region=route.Stage==1||route.Stage==2?safe.Select(plane).ToArray():null};
                a.AvoidanceRadius=(float)input[i].Radius;
            }
            var velocities=DesktopAvoidance.Plan(input,delta);
            for(int i=0;i<fleet.Length;i++)
            {
                var a=fleet[i];var agent=input[i];var velocity=velocities[i];a.AvoidanceVelocity=velocity;
                if(!agent.Movable){a.GroundTravelLimit=0;a.AvoidanceState="stationary";continue;}
                var next=DesktopAvoidance.Add(agent.Position,DesktopAvoidance.Mul(velocity,delta));
                var uv=a.Ground.Map.Unproject(next.x,next.y*tilt);
                a.GroundTravelLimit=(float)a.Ground.Metric(a.Ground.Position,uv);
                double speed=DesktopAvoidance.Length(agent.Desired),actual=DesktopAvoidance.Length(velocity);
                bool changed=DesktopAvoidance.Length(DesktopAvoidance.Sub(velocity,agent.Desired))>Math.Max(1,speed*.08);
                a.AvoidanceState=actual<1?"yield":changed?"detour":"clear";
                if(DesktopAvoidance.Length(DesktopAvoidance.Sub(velocity,agent.Desired))<.001)
                {a.GroundTravelLimit=float.PositiveInfinity;continue;}
                if(changed){a.AvoidanceFrames++;}
                // A roaming actor may reverse when the floor is too narrow to
                // pass. Commit a reachable new goal so it does not turn back
                // into the same actor on the following frame. Exits keep theirs.
                if(a.Ground.Stage==1 && speed>1 && actual>1 && now>=a.AvoidanceReverseUntil && DesktopAvoidance.Dot(velocity,agent.Desired)<-.25*speed*actual)
                {
                    var end=DesktopAvoidance.Add(agent.Position,DesktopAvoidance.Mul(velocity,Math.Max(1.5,agent.Radius*3/actual)));
                    if(agent.Region.Length>=3)end=DesktopFootprintMath.Closest(agent.Region,end);
                    a.Ground.Target=a.Ground.Map.Unproject(end.x,end.y*tilt);
                    a.AvoidanceReverseUntil=now+1.5f;a.AvoidanceReversals++;a.AvoidanceState="reverse";
                }
                a.Ground.SetSteering(uv);
            }
        }
    }
    public sealed partial class DesktopActor
    {
        public float GroundTravelLimit=float.PositiveInfinity,AvoidanceRadius,AvoidanceReverseUntil;
        public string AvoidanceState="off";
        public int AvoidanceFrames,AvoidanceReversals;
        public GroundVector AvoidanceVelocity;
    }
}
