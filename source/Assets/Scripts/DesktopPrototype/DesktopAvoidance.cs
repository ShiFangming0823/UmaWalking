using System;

namespace UmaDesktop
{
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
            public bool Movable, Steer, Backtrack;
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
                int samples=a.Steer?19:a.Backtrack?2:1;
                for(int k=0;k<=samples;k++)
                {
                    double angle=k==samples?0:k==0?0:!a.Steer?Math.PI:(k%2==1?1:-1)*((k+1)/2)*20*Math.PI/180;
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
                        double cost=Length(Sub(candidate,a.Desired))/speed+.12*Length(Sub(candidate,a.Previous))/speed;
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
