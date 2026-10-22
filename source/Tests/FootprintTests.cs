using System;
using UmaDesktop;
class FootprintTests
{
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Main(){try{Run();}catch(Exception e){Console.WriteLine(e.Message);Environment.Exit(1);}}
    static void Run()
    {
        var random=new Random(3322);
        foreach(bool reversed in new[]{false,true})
        {
            var points=new[]{new GroundPoint(.46f,.69f),new GroundPoint(.54f,.68f),new GroundPoint(.99f,.83f),new GroundPoint(.99f,.91f)};
            if(reversed)Array.Reverse(points);
            var map=new GroundMap(new GroundSurface {points=points},3840,2160);var ground=DesktopFootprintMath.Corners(map);
            var region=DesktopFootprintMath.Region(map,30,6,2);Check(region.Length>=3,"Narrow diagonal ground cannot hold ordinary feet");
            for(int i=0;i<250;i++)
            {
                var point=new GroundVector(random.Next(0,4000),random.Next(0,2400));var anchor=DesktopFootprintMath.Closest(region,point);
                Check(DesktopFootprintMath.Contains(region,anchor),"Anchor escaped eroded region");
                for(int angle=0;angle<64;angle++)
                {double t=angle*Math.PI/32;var sole=new GroundVector(anchor.x+30*Math.Cos(t),anchor.y+6*Math.Sin(t));Check(DesktopFootprintMath.Contains(ground,sole),"Projected sole escaped original ground");}
            }
            Check(DesktopFootprintMath.Region(map,100000,100000,2).Length==0,"Impossible footprint must be recognized");
        }
        var narrow=new GroundSurface {points=new[]{new GroundPoint(.4690104f,.6893519f),new GroundPoint(.5385417f,.6907408f),new GroundPoint(.9997396f,.8203704f),new GroundPoint(.9997396f,.8731481f)}};
        var config=new DesktopGroundConfig {mode="Planes",surfaces=new[]{narrow}};
        for(int trial=0;trial<20;trial++)
        {
            var route=new GroundRoute(config,0,3840,2160,new Random(trial));
            route.PlaceForDrag(new GroundVector(.17,.98));route.Target=new GroundVector(.17,1.1);
            var start=route.Foot;double escaped=0;
            for(int frame=0;frame<240;frame++)
            {
                route.Step(10);
                var region=DesktopFootprintMath.Region(route.Map,28+4*Math.Sin(frame*.3),7+Math.Sin(frame*.7),2);
                route.ConstrainFootprint(region);
                Check(route.Opacity==1,"Active edge drag still uses portal fading");
                Check(DesktopFootprintMath.Contains(region,route.Foot),"Recovery escaped safe ground");
                escaped=Math.Max(escaped,GroundMap.Distance(start,route.Foot));
            }
            Check(escaped>150,"Edge drop never recovered meaningful movement");
            Check(route.BoundaryReplans>0,"Unreachable boundary target was never replaced");
            route.Retire();
            for(int frame=0;frame<4000 && route.Stage!=4;frame++)
            {route.Step(10);route.ConstrainFootprint(DesktopFootprintMath.Region(route.Map,30,7,2));}
            Check(route.Stage==4,"Boundary recovery broke exit routing");
        }
        var line=new GroundRoute(new DesktopGroundConfig {mode="Line",line=new[]{new GroundPoint(0,.9f),new GroundPoint(1,.9f)}},0,3840,2160,random);
        line.PlaceForDrag(new GroundVector(.99,0));line.Target=new GroundVector(.99,0);line.Step(0);
        Check(line.Opacity==1,"Active line endpoint fades after dragging");
        foreach(bool isLine in new[]{false,true})
        {
            var fadeConfig=isLine?new DesktopGroundConfig {mode="Line",line=new[]{new GroundPoint(.2f,.8f),new GroundPoint(.8f,.8f)}}:config;
            var route=new GroundRoute(fadeConfig,0,3840,2160,new Random(3));int visibleEntry=0,visibleExit=0;
            for(int i=0;i<4000 && route.Stage==0;i++) {route.Step(200,.02,.5);if(route.Opacity>0 && route.Opacity<1)visibleEntry++;}
            Check(visibleEntry>=20 && route.Stage==1,"Fast actor entry lacks a gradual fade.");
            route.Retire();
            for(int i=0;i<4000 && route.Stage!=4;i++){route.Step(200,.02,.5);if(route.Stage==3 && route.Opacity>0 && route.Opacity<1)visibleExit++;}
            Check(visibleExit>=20 && route.Stage==4,"Fast actor exit lacks a gradual fade.");
        }
        narrow.exit=new GroundGate {edge=1,start=.205f,end=.26f};
        for(int trial=0;trial<20;trial++)
        {
            var route=new GroundRoute(config,0,3840,2160,new Random(trial));route.PlaceForDrag(new GroundVector(.5,.5));route.Retire();
            int frames=0;
            for(;frames<2000 && route.Stage!=4;frames++)
            {route.Step(200,.02,.5);route.ConstrainFootprint(DesktopFootprintMath.Region(route.Map,60,11,2));}
            Check(route.Stage==4 && frames<200,"Diagonal shoe inset applies slow fading to the whole exit approach.");
        }
        Console.WriteLine("PASS: "+checks+" footprint boundary/recovery/fade assertions.");
    }
}
