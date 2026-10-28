using System;
using System.Collections.Generic;
using UmaDesktop;

internal static class DesktopAvoidanceStabilityTests
{
    private static int checks;
    private static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    private static GroundVector V(double x,double y){return new GroundVector(x,y);}
    private static double Length(GroundVector v){return DesktopAvoidance.Length(v);}
    private static DesktopAvoidance.Agent Actor(int id,double x,double y,bool stable)
    {return new DesktopAvoidance.Agent {Id=id,Ground=0,Radius=300,Position=V(x,y),Movable=true,Steer=true,StableSteering=stable,
        Region=new[]{V(378,1040),V(3462,1040),V(3462,3920),V(378,3920)}};}
    private static int Scenario(string name,int fps,bool stable)
    {
        var aa=name=="crowd"?new[]{Actor(1,700,1400,stable),Actor(2,1400,1400,stable),Actor(3,2800,1400,stable),Actor(4,700,3400,stable),Actor(5,2000,3400,stable),Actor(6,2900,3400,stable)}:
            new[]{Actor(1,800,2000,stable),Actor(2,2200,2000,stable)};
        var goals=name=="crowd"?new[]{V(3100,3500),V(1500,3600),V(500,3400),V(3200,1500),V(1300,1500),V(500,1400)}:new[]{V(3200,2000),V(500,2000)};
        if(name=="obstacle")aa[1].Movable=false;
        var headings=new DesktopHeadingFilter[aa.Length];var before=new double[aa.Length];var lastTurn=new double[aa.Length];
        var initialized=new bool[aa.Length];var windows=new Queue<int>[aa.Length];var cooldown=new double[aa.Length];
        for(int i=0;i<aa.Length;i++){headings[i]=new DesktopHeadingFilter();windows[i]=new Queue<int>();}
        var rng=new Random(2929);double moved=0;int peak=0;
        for(int frame=0;frame<fps*25;frame++)
        {
            double delta=1.0/fps;
            for(int i=0;i<aa.Length;i++)
            {
                var d=DesktopAvoidance.Sub(goals[i],aa[i].Position);double n=Length(d),speed=aa[i].Id%2==0?70:260;
                if(n<1){goals[i]=V(500+rng.NextDouble()*2800,1300+rng.NextDouble()*2300);d=DesktopAvoidance.Sub(goals[i],aa[i].Position);n=Length(d);}
                aa[i].Desired=aa[i].Movable?DesktopAvoidance.Mul(d,Math.Min(speed,n/delta)/n):V(0,0);
            }
            var velocities=DesktopAvoidance.Plan(aa,delta);
            for(int i=0;i<aa.Length;i++)for(int j=i+1;j<aa.Length;j++)
                Check(DesktopAvoidance.SweptDistance(DesktopAvoidance.Sub(aa[i].Position,aa[j].Position),DesktopAvoidance.Sub(velocities[i],velocities[j]),delta)>=aa[i].Radius+aa[j].Radius-.01,name+" simultaneous swept collision at "+fps+" FPS");
            for(int i=0;i<aa.Length;i++)
            {
                var a=aa[i];var v=velocities[i];
                if(Length(v)>2)
                {
                    double yaw=Math.Atan2(-v.x,v.y)*180/Math.PI;
                    if(stable)yaw=headings[i].Filter(yaw);
                    double turn=initialized[i]?DesktopHeadingFilter.Delta(before[i],yaw):0;
                    if(Math.Abs(turn)>2&&Math.Abs(turn)<100&&turn*lastTurn[i]<0)windows[i].Enqueue(frame);
                    if(Math.Abs(turn)>2)lastTurn[i]=turn;
                    before[i]=yaw;initialized[i]=true;
                }
                while(windows[i].Count>0&&windows[i].Peek()<frame-fps)windows[i].Dequeue();peak=Math.Max(peak,windows[i].Count);
                if(frame*delta>=cooldown[i]&&Length(v)>1&&Length(a.Desired)>1&&DesktopAvoidance.Dot(v,a.Desired)<-.25*Length(v)*Length(a.Desired))
                {
                    goals[i]=DesktopFootprintMath.Closest(a.Region,DesktopAvoidance.Add(a.Position,DesktopAvoidance.Mul(v,Math.Max(1.5,a.Radius*3/Length(v)))));cooldown[i]=frame*delta+1.5;
                }
                var next=DesktopAvoidance.Add(a.Position,DesktopAvoidance.Mul(v,delta));
                Check(DesktopFootprintMath.Contains(a.Region,next),name+" stays in its full-body desktop region");
                if(!a.Movable)Check(GroundMap.Distance(a.Position,next)<1e-8,"A held/special actor must remain stationary");
                moved+=GroundMap.Distance(a.Position,next);a.Position=next;a.Previous=v;
            }
        }
        Check(moved>2000,name+" continues moving instead of hiding jitter by stopping");
        Console.WriteLine(name+" "+fps+" FPS stable="+stable+" peak reversals/sec="+peak+" distance="+moved.ToString("F0"));
        return peak;
    }
    private static void Run()
    {
        foreach(int fps in new[]{15,30,60,120})
        foreach(string name in new[]{"head-on","obstacle","crowd"})
        {
            int original=Scenario(name,fps,false),fixedPeak=Scenario(name,fps,true);
            Check(fixedPeak<=5,name+" still has high-frequency heading reversals at "+fps+" FPS: "+fixedPeak);
            Check(fixedPeak<=original*.3+1,name+" heading stability did not improve sufficiently");
        }
        var filter=new DesktopHeadingFilter();filter.Reset(179);
        Check(Math.Abs(DesktopHeadingFilter.Delta(179,filter.Filter(-179)))<.001,"Yaw wrap must not cause a 358 degree correction");
        Check(Math.Abs(DesktopHeadingFilter.Delta(-170,filter.Filter(-170)))<.001,"A real turn must still be followed immediately");
        filter.Reset(30);for(int i=0;i<1000;i++)Check(filter.Filter(i%2==0?28:32)==30,"Sub-step oscillation must not alternate facing");
        filter.Reset(90);Check(filter.Filter(90)==90,"Mode/ground transition resets facing history");
        // Changing gait speed cannot leave a history penalty holding half speed.
        var single=Actor(1,1500,2000,true);single.Desired=V(260,0);single.Previous=V(65,0);
        Check(DesktopAvoidance.Plan(new[]{single},1.0/30)[0].x>250,"Speed changes are independent of heading continuity");
        Console.WriteLine("Desktop avoidance stability assertions passed: "+checks);
    }
    public static void Main(){try{Run();}catch(Exception ex){Console.WriteLine(ex);Environment.Exit(1);}}
}
