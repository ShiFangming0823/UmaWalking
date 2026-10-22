using System;
using UmaDesktop;
internal static class GroundTests
{
    private static int checks;
    private static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    private static GroundSurface Surface(float left=0)
    {return new GroundSurface {points=new[]{new GroundPoint(.25f+left,.4f),new GroundPoint(.65f+left,.4f),new GroundPoint(.75f+left,.9f),new GroundPoint(.15f+left,.9f)},entrance=new GroundGate {edge=3,start=.15f,end=.55f},exit=new GroundGate {edge=1,start=.3f,end=.7f}};}
    public static void Main(){try{Run();}catch(Exception ex){Console.WriteLine("FAIL "+ex.Message+" check="+checks);Environment.Exit(1);}}
    private static void Run()
    {
        var config=new DesktopGroundConfig {mode="Planes",surfaces=new[]{Surface()}};
        Check(GroundMap.Validate(config)==null,"Valid quad");
        var map=new GroundMap(config.surfaces[0],3840,2160);var random=new Random(42);
        for(int i=0;i<1000;i++)
        {
            double u=random.NextDouble(),v=random.NextDouble();var p=map.Project(u,v);var uv=map.Unproject(p.x,p.y);
            Check(Math.Abs(uv.x-u)<1e-8&&Math.Abs(uv.y-v)<1e-8,"Homography round trip");
        }
        foreach(int edge in new[]{0,1,2,3})for(int run=0;run<50;run++)
        {
            config.surfaces[0].entrance.edge=edge;config.surfaces[0].exit.edge=(edge+run)%4;
            var route=new GroundRoute(config,0,3840,2160,random);
            Check(route.Opacity==0,"Entry starts hidden");
            for(int i=0;i<2000 && route.Stage==0;i++)route.Step(10);
            Check(route.Stage==1,"Entered");
            for(int i=0;i<400;i++)
            {
                route.Step(30);Check(route.Position.x>=0&&route.Position.x<=1&&route.Position.y>=0&&route.Position.y<=1,"Inside quad");
            }
            route.Retire();GroundVector prev=route.Position;bool crossed=false;
            for(int i=0;i<2000&&route.Stage!=4;i++)
            {
                prev=route.Position;route.Step(10);
                if(route.Stage==3 && (route.Position.x<0||route.Position.x>1||route.Position.y<0||route.Position.y>1))
                {
                    var gate=map.Gate(config.surfaces[0].exit,.5);int e=config.surfaces[0].exit.edge;
                    double t=e==0||e==2?(0-prev.y+(e==2?1:0))/(route.Position.y-prev.y):((e==1?1:0)-prev.x)/(route.Position.x-prev.x);
                    if(!crossed)
                    {
                        var uv=GroundVector.Lerp(prev,route.Position,t);var screen=map.Project(uv.x,uv.y);
                        var a=map.Gate(config.surfaces[0].exit,0);var b=map.Gate(config.surfaces[0].exit,1);a=map.Project(a.x,a.y);b=map.Project(b.x,b.y);
                        Check(GroundMap.Distance(a,screen)+GroundMap.Distance(screen,b)-GroundMap.Distance(a,b)<.01,"Crossed selected exit width edge="+e+" prev="+prev.x+","+prev.y+" pos="+route.Position.x+","+route.Position.y+" t="+t+" d="+(GroundMap.Distance(a,screen)+GroundMap.Distance(screen,b)-GroundMap.Distance(a,b)));crossed=true;
                    }
                }
            }
            Check(route.Stage==4&&route.Opacity==0&&crossed,"Exited via edge");
        }
        config.surfaces[0].points[2]=new GroundPoint(.1f,.2f);Check(GroundMap.Validate(config)!=null,"Reject crossed quad");
        config.surfaces=new[]{Surface()};config.surfaces[0].exit.end=config.surfaces[0].exit.start;Check(GroundMap.Validate(config)!=null,"Reject zero width gate");
        config.surfaces=new[]{Surface(),Surface(),Surface(),Surface()};Check(GroundMap.Validate(config)!=null,"Reject fourth surface");
        config.mode="Line";config.line=new[]{new GroundPoint(.1f,.8f),new GroundPoint(.9f,.7f)};
        Check(GroundMap.Validate(config)==null,"Line valid");
        var line=new GroundRoute(config,0,3840,2160,random);
        for(int i=0;i<1000;i++)line.Step(10);Check(line.Stage==1,"Line active");
        line.Retire();for(int i=0;i<1000&&line.Stage!=4;i++)line.Step(10);Check(line.Stage==4,"Line exit");
        Console.WriteLine("Ground tests passed: "+checks);
    }
}
