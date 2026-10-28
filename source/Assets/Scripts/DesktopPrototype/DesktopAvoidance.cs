using System;

namespace UmaDesktop
{
    // Display-only hysteresis for sub-step avoidance noise. Movement is always
    // governed by the collision-validated solver; this never changes a path.
    public sealed class DesktopHeadingFilter
    {
        private bool initialized;
        private double accepted;
        public static double Delta(double from,double to)
        {double d=(to-from)%360;return d>180?d-360:d< -180?d+360:d;}
        public void Reset(double yaw){accepted=yaw;initialized=true;}
        public double Filter(double yaw)
        {
            if(!initialized)Reset(yaw);
            if(Math.Abs(Delta(accepted,yaw))>=6)accepted=yaw;
            return accepted;
        }
    }

    // Local, predictive disc avoidance on the horizontal floor. Coordinates are
    // screen X and screen Y / sin(camera elevation), never sprite rectangles.
    // At most ten actors: bounded angular samples need neither a navmesh nor threads.
    public static class DesktopAvoidance
    {
        public sealed class Agent
        {
            public int Id, Ground, Partner=-1;
            public GroundVector Position, Desired, Previous;
            public GroundVector[] Region;
            public double Radius;
            public bool Movable, Steer, Backtrack, StableSteering;
        }
        public static GroundVector Add(GroundVector a,GroundVector b){return new GroundVector(a.x+b.x,a.y+b.y);}
        public static GroundVector Sub(GroundVector a,GroundVector b){return new GroundVector(a.x-b.x,a.y-b.y);}
        public static GroundVector Mul(GroundVector a,double s){return new GroundVector(a.x*s,a.y*s);}
        public static double Dot(GroundVector a,GroundVector b){return a.x*b.x+a.y*b.y;}
        public static double Length(GroundVector a){return Math.Sqrt(Dot(a,a));}
        private static bool Related(Agent a,Agent b){return a.Ground==b.Ground && a.Id!=b.Id && !(a.Partner==b.Id&&b.Partner==a.Id);}
        public static double SweptDistance(GroundVector relativePosition,GroundVector relativeVelocity,double seconds)
        {
            double v=Dot(relativeVelocity,relativeVelocity);
            double t=v<1e-12?0:Math.Max(0,Math.Min(seconds,-Dot(relativePosition,relativeVelocity)/v));
            return Length(Add(relativePosition,Mul(relativeVelocity,t)));
        }
        private static bool Safe(Agent a,GroundVector velocity,Agent b,GroundVector other,double horizon,double margin)
        {
            var p=Sub(a.Position,b.Position);var v=Sub(velocity,other);
            double distance=Length(p),required=a.Radius+b.Radius+margin;
            // A mouse drop can overlap actors. Allow continuous separation, but
            // never solve it by teleporting or pushing the held actor away.
            if(distance<required-.01)return Dot(p,v)>=-1e-6 && Length(Add(p,Mul(v,horizon)))>=distance-.001;
            return SweptDistance(p,v,horizon)>=required-.001;
        }
        public static GroundVector[] Plan(Agent[] agents,double delta)
        {
            var result=new GroundVector[agents.Length];
            if(delta<=0)return result;
            for(int i=0;i<agents.Length;i++)result[i]=agents[i].Movable?agents[i].Desired:new GroundVector();
            // Predict against the other actors' chosen velocities, with stable
            // token order and a shared right-hand preference for head-on meetings.
            for(int pass=0;pass<3;pass++)for(int i=0;i<agents.Length;i++)
            {
                var a=agents[i];if(!a.Movable)continue;
                double speed=Length(a.Desired);if(speed<1e-8){result[i]=new GroundVector();continue;}
                GroundVector best=new GroundVector();double score=double.MaxValue;
                int bank=a.Steer?19:a.Backtrack?2:1;
                double previousSpeed=Length(a.Previous);
                bool history=a.StableSteering && a.Steer && previousSpeed>Math.Max(1,speed*.02);
                double oldAngle=history?Math.Atan2(a.Desired.x*a.Previous.y-a.Desired.y*a.Previous.x,Dot(a.Desired,a.Previous)):0;
                double turn=Math.Min(Math.PI/4,120*Math.PI/180*delta);
                int samples=bank+(history?4:0);
                for(int k=0;k<=samples;k++)
                {
                    double angle=k==samples?0:k==0?0:!a.Steer?Math.PI:(k%2==1?1:-1)*((k+1)/2)*20*Math.PI/180;
                    // The old bank only offered angles relative to the goal. A
                    // feasible heading could disappear on the next frame, making
                    // actors alternate between adjacent 20-degree candidates.
                    // Offer history and small continuous turns, then validate them
                    // with exactly the same collision checks as every other step.
                    if(history && k>=bank && k<samples)
                        angle=k==bank?oldAngle:k==bank+1?oldAngle+Math.Max(-turn,Math.Min(turn,-oldAngle)):
                            k==bank+2?oldAngle+turn:oldAngle-turn;
                    for(int slow=0;slow<(k==samples?1:2);slow++)
                    {
                        double factor=k==samples?0:slow==0?1:.5;
                        var candidate=new GroundVector((a.Desired.x*Math.Cos(angle)-a.Desired.y*Math.Sin(angle))*factor,(a.Desired.x*Math.Sin(angle)+a.Desired.y*Math.Cos(angle))*factor);
                        if(a.Region!=null && a.Region.Length>=3 && factor>0)
                        {
                            double look=delta;var end=Add(a.Position,Mul(candidate,look));
                            var clamped=DesktopFootprintMath.Closest(a.Region,end);
                            // Portals are handled by non-steering agents; the
                            // convex inset keeps detours inside the same floor.
                            candidate=Mul(Sub(clamped,a.Position),1/look);
                        }
                        bool safe=true;
                        for(int j=0;j<agents.Length;j++)if(Related(a,agents[j]) && (!Safe(a,candidate,agents[j],result[j],Math.Max(delta,1.1),8) || !Safe(a,candidate,agents[j],new GroundVector(),delta,0))){safe=false;break;}
                        if(!safe)continue;
                        double candidateSpeed=Length(candidate);
                        double cost=Length(Sub(candidate,a.Desired))/speed;
                        if(history && candidateSpeed>1)
                        {
                            double directionChange=Length(Sub(Mul(candidate,1/candidateSpeed),Mul(a.Previous,1/previousSpeed)));
                            cost+=1.2*directionChange;
                            // Keep converging toward the goal when that turn is
                            // safe, rather than preserving a detour forever.
                            if(directionChange<turn*.45 && Math.Abs(oldAngle)>turn*1.1)cost+=.04;
                        }
                        else cost+=.12*Length(Sub(candidate,a.Previous))/speed;
                        if(angle<0)cost+=.015;
                        // Waiting is a fallback, not cheaper than turning around.
                        if(Length(candidate)<speed*.05)cost+=1.4;
                        if(cost<score){score=cost;best=candidate;}
                    }
                }
                result[i]=best;
            }
            // Validate the simultaneous swept step as a whole. A stopped actor
            // becomes an obstacle too; propagate stops until all pairs are safe.
            // This prevents tunnelling at 15 FPS and sequential-update bias.
            for(int pass=0;pass<agents.Length*agents.Length;pass++)
            {
                bool changed=false;
                for(int i=0;i<agents.Length;i++)for(int j=i+1;j<agents.Length;j++)
                {
                    if(!Related(agents[i],agents[j]))continue;
                    if(Safe(agents[i],result[i],agents[j],result[j],delta,0) &&
                       Safe(agents[i],result[i],agents[j],new GroundVector(),delta,0) &&
                       Safe(agents[j],result[j],agents[i],new GroundVector(),delta,0))continue;
                    if(Length(result[i])+Length(result[j])<1e-8)continue;
                    result[i]=result[j]=new GroundVector();changed=true;
                }
                if(!changed)break;
            }
            return result;
        }
    }
}
