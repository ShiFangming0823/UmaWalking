using System;
using UmaDesktop;
class AvoidanceTests
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static GroundVector V(double x,double y){return new GroundVector(x,y);}
    static DesktopAvoidance.Agent A(int id,double x,double y,double dx,double dy,bool movable=true)
    {return new DesktopAvoidance.Agent {Id=id,Ground=0,Radius=32,Position=V(x,y),Desired=V(dx,dy),Movable=movable,Steer=true,Region=new[]{V(0,0),V(1200,0),V(1200,800),V(0,800)}};}
    static void Scenario(string name,DesktopAvoidance.Agent[] agents,int fps,int frames,bool requireProgress=true)
    {
        var start=new GroundVector[agents.Length];for(int i=0;i<agents.Length;i++)start[i]=agents[i].Position;
        double moved=0;bool detour=false;
        for(int f=0;f<frames;f++)
        {
            double delta=1.0/fps;var velocity=DesktopAvoidance.Plan(agents,delta);
            for(int i=0;i<agents.Length;i++)for(int j=i+1;j<agents.Length;j++)if(agents[i].Ground==agents[j].Ground)
                Check(DesktopAvoidance.SweptDistance(DesktopAvoidance.Sub(agents[i].Position,agents[j].Position),DesktopAvoidance.Sub(velocity[i],velocity[j]),delta)>=agents[i].Radius+agents[j].Radius-.01,name+" swept collision at frame "+f);
            for(int i=0;i<agents.Length;i++)
            {
                var a=agents[i];var next=DesktopAvoidance.Add(a.Position,DesktopAvoidance.Mul(velocity[i],delta));
                Check(DesktopFootprintMath.Contains(a.Region,next),name+" floor bounds");
                if(!a.Movable)Check(GroundMap.Distance(a.Position,next)<.001,name+" stationary moved");
                moved+=GroundMap.Distance(a.Position,next);a.Position=next;a.Previous=velocity[i];
                if(DesktopAvoidance.Length(DesktopAvoidance.Sub(velocity[i],a.Desired))>5)detour=true;
            }
        }
        Check(!requireProgress||moved>100,name+" no progress");Check(detour,name+" avoidance never engaged");
    }
    static void Main()
    {
        try
        {
            foreach(int fps in new[]{15,30,60,120})
            {
                Scenario("head-on "+fps,new[]{A(1,300,400,150,0),A(2,900,400,-150,0)},fps,fps*3);
                Scenario("crossing "+fps,new[]{A(1,300,400,180,0),A(2,600,100,0,180)},fps,fps*3);
                Scenario("overtake "+fps,new[]{A(1,250,400,230,0),A(2,480,400,75,0)},fps,fps*2);
                Scenario("special/drag obstacle "+fps,new[]{A(1,250,400,200,0),A(2,600,400,0,0,false)},fps,fps*3);
                var narrow=new[]{A(1,300,400,180,0),A(2,900,400,-180,0)};
                foreach(var a in narrow)a.Region=new[]{V(0,385),V(1200,385),V(1200,415),V(0,415)};
                Scenario("narrow reverse/yield "+fps,narrow,fps,fps*3);
                Scenario("six crossing "+fps,new[]{A(1,200,400,200,0),A(2,1000,400,-200,0),A(3,600,80,0,180),A(4,600,720,0,-180),A(5,250,100,110,100),A(6,950,700,-110,-100)},fps,fps*3);
            }
            var separate=new[]{A(1,300,400,150,0),A(2,310,400,-150,0)};separate[1].Ground=1;
            var v=DesktopAvoidance.Plan(separate,1.0/15);Check(v[0].x>140&&v[1].x< -140,"Separate grounds must not collide");
            separate[1].Ground=0;separate[0].Partner=2;separate[1].Partner=1;
            v=DesktopAvoidance.Plan(separate,1.0/15);Check(v[0].x>140&&v[1].x< -140,"Scripted interaction partners exempt");
            var overlap=new[]{A(1,500,400,150,0),A(2,510,400,0,0,false)};
            v=DesktopAvoidance.Plan(overlap,1.0/30);Check(v[0].x<0,"Overlapping mouse drop must separate continuously");
            v=DesktopAvoidance.Plan(overlap,0);Check(DesktopAvoidance.Length(v[0])==0,"Paused simulation does not move");
            // An exit waypoint can lie exactly on the eroded floor boundary.
            // Clamp only the actual step, not a longer look-ahead segment, or
            // the actor asymptotically stops just short of the exit transition.
            var edge=A(1,90,400,-300,0);edge.Region=new[]{V(25,0),V(1200,0),V(1200,800),V(25,800)};
            for(int frame=0;frame<60;frame++)
            {edge.Desired=V(-Math.Min(300,(edge.Position.x-25)*30),0);var planned=DesktopAvoidance.Plan(new[]{edge},1.0/30);edge.Position=DesktopAvoidance.Add(edge.Position,DesktopAvoidance.Mul(planned[0],1.0/30));edge.Previous=planned[0];}
            Check(Math.Abs(edge.Position.x-25)<.0001,"Boundary exit waypoint must be reached in finite time");
            // New arrivals may share the same small gate while still invisible.
            // Let its outside queue separate along the portal approach rather
            // than freezing every overlapping arrival at the spawn position.
            var queue=new[]{A(1,500,400,100,0),A(2,510,400,100,0),A(3,520,400,100,0)};
            foreach(var q in queue){q.Steer=false;q.Backtrack=true;q.Region=null;}
            for(int frame=0;frame<120;frame++)
            {var planned=DesktopAvoidance.Plan(queue,1.0/30);for(int i=0;i<queue.Length;i++){queue[i].Position=DesktopAvoidance.Add(queue[i].Position,DesktopAvoidance.Mul(planned[i],1.0/30));queue[i].Previous=planned[i];}}
            Check(GroundMap.Distance(queue[0].Position,queue[1].Position)>=63.9&&GroundMap.Distance(queue[1].Position,queue[2].Position)>=63.9,"Overlapping portal queue must recover separation");
            // Steering must not overwrite entry/exit route destinations or stages.
            var config=new DesktopGroundConfig {mode="Planes",surfaces=new[]{new GroundSurface {points=new[]{new GroundPoint(.1f,.1f),new GroundPoint(.9f,.1f),new GroundPoint(.9f,.9f),new GroundPoint(.1f,.9f)}}}};
            var route=new GroundRoute(config,0,1200,800,new Random(4));route.PlaceForDrag(V(.5,.5));route.Target=V(.8,.5);
            route.SetSteering(V(.5,.6));route.Step(10);Check(route.Position.y>.5&&route.Target.x==.8&&route.Stage==1,"Detour preserves roaming destination");
            route.SetSteering(V(route.Position.x+.005,route.Position.y+.01));route.Step(100);Check(route.FacingPreview(5).x>route.Position.x&&route.FacingPreview(5).y>route.Position.y,"Facing tangent survives reaching the short steering waypoint");
            route.ClearSteering();Check(route.Preview(20).x>route.Position.x,"Disabling immediately restores original heading");
            route.Retire();var target=route.Target;route.SetSteering(V(.3,.3));route.Step(10);Check(route.Stage==2&&route.Target.x==target.x&&route.Target.y==target.y,"Exit destination retained");
            route.PlaceForDrag(V(.4,.4));Check(route.Stage==1&&route.Preview(10).x!=.3,"Drag clears obsolete steering");
            Console.WriteLine("AvoidanceTests passed: "+checks);
        }
        catch(Exception e){Console.WriteLine(e);Environment.Exit(1);}
    }
}
