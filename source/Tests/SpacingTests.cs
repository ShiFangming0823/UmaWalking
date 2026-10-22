using System;
using UmaDesktop;
class SpacingTests
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static void Main()
    {
        var s=new DesktopSchedule(new Random(12),1,1,100,100);s.ResetCharacter();var gait=s.Behavior;
        s.Tick(1.1,true,false,false);Check(s.SpecialPending && s.Behavior==gait,"Blocked automatic special keeps the same gait");
        s.Tick(2,true,false,false);Check(s.SpecialPending && s.Behavior==gait,"Pending special must not reset its timer or reroll gait");
        s.Tick(0,true,false,true);Check(s.Behavior==gait,"Pausing must not start a pending action");
        s.Tick(.01,true,false,true);Check(s.Behavior==MiniBehavior.Special,"Pending special begins when room is available");
        s.ResumeLocomotion();s.MakeSpecialDue();s.Tick(.01,true,false,false);s.RequestSpecial();Check(s.Behavior==MiniBehavior.Special,"User request bypasses automatic clearance");
        Check(!DesktopSpacing.Clear(100,100,290,100,20),"Both actor outlines and padding matter");
        Check(DesktopSpacing.Clear(100,100,320,100,20),"Boundary is allowed");
        Check(DesktopSpacing.TravelLimit(0,1,1000,50,200,50,20)==80,"Fast actor cannot tunnel through reservation");
        Check(DesktopSpacing.TravelLimit(400,-1,1000,50,200,50,20)==80,"Symmetric leftward travel");
        Check(DesktopSpacing.TravelLimit(0,-1,40,50,200,50,20)==40,"Moving away is unrestricted");
        Check(DesktopSpacing.TravelLimit(150,1,40,50,200,50,20)==0,"Already touching reservation cannot move inward");
        var rng=new Random(34);double min=2,max=0,total=0;
        for(int i=0;i<10000;i++){double n=DesktopSpacing.SampleSpeedFactor(rng,.8,1.2);Check(n>=.8 && n<=1.2,"Sample must stay in configured bounds");min=Math.Min(min,n);max=Math.Max(max,n);total+=n;}
        Check(min<.81 && max>1.19 && Math.Abs(total/10000-1)<.01,"Sampling spans the range without bias");
        // Independent interval invariant over arbitrary speed, distance and scale.
        for(int i=0;i<10000;i++)
        {
            double a=rng.NextDouble()*300,b=rng.NextDouble()*300,gap=8+rng.NextDouble()*30,distance=rng.NextDouble()*1000;
            double separation=a+b+gap+rng.NextDouble()*1000;
            double allowed=DesktopSpacing.TravelLimit(0,1,distance,a,separation,b,gap);
            Check(allowed>=0 && allowed<=distance && DesktopSpacing.Clear(allowed,a,separation,b,gap),"Swept interval preserves clearance");
        }
        Console.WriteLine("PASS: "+checks+" speed/spacing/deferred-action assertions.");
    }
}
