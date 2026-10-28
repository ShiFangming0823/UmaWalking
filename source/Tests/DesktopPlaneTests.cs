using System;
using System.Collections.Generic;
using UmaDesktop;

internal static class DesktopPlaneTests
{
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void Main(){try{Run();}catch(Exception ex){Console.WriteLine("FAIL "+ex.Message+" check="+checks);Environment.Exit(1);}}
    private static void Run()
    {
        var config=new DesktopGroundConfig {mode="Desktop"};
        Check(GroundMap.Validate(config)==null,"Fullscreen needs neither drawn points nor gates");
        var entries=new HashSet<int>();var exits=new HashSet<int>();var rng=new Random(22024);
        foreach(var dimensions in new[]{new[]{3840.0,2160},new[]{1920.0,1080},new[]{1080.0,1920}})
        for(int run=0;run<100;run++)
        {
            double width=dimensions[0],height=dimensions[1];
            var route=new GroundRoute(config,0,width,height,rng);
            const double left=150,top=450,right=150,bottom=100;
            route.SetDesktopBounds(left,top,right,bottom);
            entries.Add(route.EntranceEdge);
            var p=route.Foot;
            Check(p.x+right<0 || p.x-left>width || p.y+bottom<0 || p.y-top>height,"Entire body starts beyond a screen edge");
            Check(route.IsDesktop&&!route.IsLine&&route.Opacity==0,"Hidden orthographic entry");
            Check(Math.Abs(route.Map.Denominator(.1,.1)-route.Map.Denominator(.9,.9))<1e-9,"No perspective division with depth");
            var at=route.Map.Project(.5,.5);var uv=route.Map.Unproject(at.x,at.y);
            Check(Math.Abs(uv.x-.5)+Math.Abs(uv.y-.5)<1e-9,"Fullscreen coordinates round trip");
            Check(Math.Abs(route.Map.Depth-height/.5)<1e-7,"Travel metric agrees with 30 degree camera");
            double opacity=0;int step=0;
            while(route.Stage==0 && step++<10000)
            {
                route.Step(8,.03,.5);
                Check(route.Opacity+1e-9>=opacity,"Entry opacity is monotonic");opacity=route.Opacity;
            }
            Check(route.Stage==1&&route.Opacity==1,"Entry reaches the safe interior without a snap");
            for(int i=0;i<600;i++)
            {
                route.Step(35);route.ConstrainFootprint(route.DesktopRegion);
                Check(DesktopFootprintMath.Contains(route.DesktopRegion,route.Foot),"Wandering keeps the full body inside");
            }
            // Offscreen dragging is clamped by the same asymmetric whole-body bounds.
            int index;var desired=DesktopGroundDrag.Place(config,width,height,new GroundVector(1e5,-1e5),0,out index);
            route.PlaceScreenForDrag(desired.x,desired.y);route.ConstrainFootprint(route.DesktopRegion);
            Check(index==0 && route.Stage==1 && route.Opacity==1 && DesktopFootprintMath.Contains(route.DesktopRegion,route.Foot),"Offscreen drag clamps and cancels entry/exit fade");
            route.Retire();exits.Add(route.ExitEdge);step=0;bool fading=false;opacity=1;
            while(route.Stage!=4 && step++<10000)
            {
                route.Step(12,.03,.5);
                if(route.Stage==2)Check(DesktopFootprintMath.Contains(route.DesktopRegion,route.Foot),"Whole body stays visible on approach to exit");
                if(route.Stage==3){Check(route.Opacity<=opacity+1e-9,"Exit opacity is monotonic");opacity=route.Opacity;fading|=opacity<.99&&opacity>.01;}
            }
            p=route.Foot;
            Check(route.Stage==4&&route.Opacity==0&&fading,"Exit completes with visible fade");
            Check(p.x+right<0 || p.x-left>width || p.y+bottom<0 || p.y-top>height,"Whole body leaves the desktop before disposal");
        }
        Check(entries.Count==4 && exits.Count==4,"All four screen edges can be used for entry and exit");
        Check(config.surfaces.Length==0 && config.line.Length==0,"Generated boundaries do not modify the user's saved grounds");
        Console.WriteLine("Desktop plane tests passed: "+checks);
    }
}
