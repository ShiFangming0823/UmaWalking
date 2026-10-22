using System;
using UmaDesktop;
class GroundDragTests
{
    static int checks;
    static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
    static GroundSurface Floor(float left,float right)
    {return new GroundSurface {points=new[]{new GroundPoint(left+.02f,.4f),new GroundPoint(right-.02f,.38f),new GroundPoint(right,.9f),new GroundPoint(left,.8f)}};}
    static void Main(){try{Run();}catch(Exception e){Console.WriteLine(e.Message);Environment.Exit(1);}}
    static void Run()
    {
        var config=new DesktopGroundConfig {mode="Planes",surfaces=new[]{Floor(.05f,.4f),Floor(.6f,.95f)}};
        var random=new Random(333);int index;double w=3840,h=2160;
        for(int i=0;i<3000;i++)
        {
            var desired=new GroundVector(random.Next(-1000,5000),random.Next(-1000,4000));
            GroundVector p=DesktopGroundDrag.Place(config,w,h,desired,i%2,out index);var map=new GroundMap(config.surfaces[index],w,h);var uv=map.Unproject(p.x,p.y);
            Check(uv.x>=.04-1e-7&&uv.x<=.96+1e-7&&uv.y>=.04-1e-7&&uv.y<=.96+1e-7,"Dragged foot escaped a floor");
            Check(GroundMap.Distance(p,desired)<=GroundMap.Distance(map.Project(.5,.5),desired)+.001,"Closest projection worse than interior point");
        }
        var target=new GroundMap(config.surfaces[1],w,h).Project(.5,.5);
        var actual=DesktopGroundDrag.Place(config,w,h,target,0,out index);Check(index==1&&GroundMap.Distance(actual,target)<1e-6,"Cross-floor dragging failed");
        config.surfaces=new[]{Floor(.1f,.9f),Floor(.1f,.9f)};
        actual=DesktopGroundDrag.Place(config,w,h,new GroundVector(w*.5,h*.6),1,out index);Check(index==1,"Overlapping ground changed unnecessarily");
        config.mode="Line";config.line=new[]{new GroundPoint(.1f,.8f),new GroundPoint(.9f,.4f)};
        for(int i=0;i<1000;i++)
        {
            var p=DesktopGroundDrag.Place(config,w,h,new GroundVector(random.Next(-1000,5000),random.Next(-1000,4000)),0,out index);
            var route=new GroundRoute(config,0,w,h,random);route.PlaceScreenForDrag(p.x,p.y);
            Check(route.Stage==1&&route.Opacity==1&&route.Position.x>=.04&&route.Position.x<=.96,"Line drag not clamped/visible");
            Check(GroundMap.Distance(route.Foot,p)<.001,"Line drag anchor mismatch");
        }
        Console.WriteLine("PASS: "+checks+" drag placement assertions.");
    }
}
