using System;

namespace UmaDesktop
{
    [Serializable] public sealed class GroundPoint
    {
        public float x,y;
        public GroundPoint() { }
        public GroundPoint(float x,float y) {this.x=x;this.y=y;}
    }
    [Serializable] public sealed class GroundGate
    {
        public int edge;
        public float start=0.35f,end=0.65f;
    }
    [Serializable] public sealed class GroundSurface
    {
        // Clockwise perimeter, beginning at far left: far left/right, near right/left.
        public GroundPoint[] points=new GroundPoint[0];
        public GroundGate entrance=new GroundGate(),exit=new GroundGate {edge=2};
    }
    [Serializable] public sealed class DesktopGroundConfig
    {
        public string mode="Default";
        public GroundPoint[] line=new GroundPoint[0];
        public GroundSurface[] surfaces=new GroundSurface[0];
    }
    public struct GroundVector
    {
        public double x,y;
        public GroundVector(double x,double y) {this.x=x;this.y=y;}
        public static GroundVector Lerp(GroundVector a,GroundVector b,double t) {return new GroundVector(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);}
    }
    // Pure geometry shared by the Unity player, Windows editor and regression tests.
    public sealed class GroundMap
    {
        public readonly double[] h=new double[9];
        private readonly double[] inverse=new double[9];
        public readonly double Width,Depth;
        public GroundMap(GroundSurface surface,double width,double height)
        {
            double[,] a=new double[8,9];
            for(int i=0;i<4;i++)
            {
                double u=i==1 || i==2?1:0,v=i>=2?1:0;
                double x=surface.points[i].x*width,y=surface.points[i].y*height;
                int r=i*2;a[r,0]=u;a[r,1]=v;a[r,2]=1;a[r,6]=-x*u;a[r,7]=-x*v;a[r,8]=x;
                a[r+1,3]=u;a[r+1,4]=v;a[r+1,5]=1;a[r+1,6]=-y*u;a[r+1,7]=-y*v;a[r+1,8]=y;
            }
            for(int c=0;c<8;c++)
            {
                int p=c;for(int r=c+1;r<8;r++)if(Math.Abs(a[r,c])>Math.Abs(a[p,c]))p=r;
                if(Math.Abs(a[p,c])<1e-10)throw new ArgumentException("地面四点无法构成有效平面。");
                for(int k=c;k<9;k++){double t=a[c,k];a[c,k]=a[p,k];a[p,k]=t;}
                double d=a[c,c];for(int k=c;k<9;k++)a[c,k]/=d;
                for(int r=0;r<8;r++)if(r!=c){double f=a[r,c];for(int k=c;k<9;k++)a[r,k]-=f*a[c,k];}
            }
            for(int i=0;i<8;i++)h[i]=a[i,8];h[8]=1;
            inverse[0]=h[4]*h[8]-h[5]*h[7];inverse[1]=h[2]*h[7]-h[1]*h[8];inverse[2]=h[1]*h[5]-h[2]*h[4];
            inverse[3]=h[5]*h[6]-h[3]*h[8];inverse[4]=h[0]*h[8]-h[2]*h[6];inverse[5]=h[2]*h[3]-h[0]*h[5];
            inverse[6]=h[3]*h[7]-h[4]*h[6];inverse[7]=h[1]*h[6]-h[0]*h[7];inverse[8]=h[0]*h[4]-h[1]*h[3];
            GroundVector n0=Project(0,1),n1=Project(1,1),far=Project(.5,0),near=Project(.5,1);
            Width=Math.Max(100,Distance(n0,n1));
            Depth=Math.Max(100,Distance(far,near)/Math.Sin(10*Math.PI/180));
        }
        public static double Distance(GroundVector a,GroundVector b){return Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
        private static GroundVector Transform(double[] m,double x,double y)
        {double w=m[6]*x+m[7]*y+m[8];return new GroundVector((m[0]*x+m[1]*y+m[2])/w,(m[3]*x+m[4]*y+m[5])/w);}
        public GroundVector Project(double u,double v){return Transform(h,u,v);}
        public GroundVector Unproject(double x,double y){return Transform(inverse,x,y);}
        public double Denominator(double u,double v){return h[6]*u+h[7]*v+1;}
        public GroundVector Gate(GroundGate gate,double t)
        {
            int e=gate.edge;GroundVector a=Project(e==1 || e==2?1:0,e>=2?1:0);
            e=(e+1)%4;GroundVector b=Project(e==1 || e==2?1:0,e>=2?1:0);
            GroundVector p=GroundVector.Lerp(a,b,gate.start+(gate.end-gate.start)*t);return Unproject(p.x,p.y);
        }
        public static GroundVector Inward(int edge)
        {return edge==0?new GroundVector(0,1):edge==1?new GroundVector(-1,0):edge==2?new GroundVector(0,-1):new GroundVector(1,0);}
        public static string Validate(DesktopGroundConfig config)
        {
            if(config==null || config.mode=="Default")return null;
            if(config.mode=="Line")
            {
                if(config.line==null || config.line.Length!=2 || !PointValid(config.line[0]) || !PointValid(config.line[1]))return "请在屏幕内指定直线的两个端点。";
                return Distance(new GroundVector(config.line[0].x,config.line[0].y),new GroundVector(config.line[1].x,config.line[1].y))<.06?"直线太短，请重新绘制。":null;
            }
            if(config.mode!="Planes" || config.surfaces==null || config.surfaces.Length<1 || config.surfaces.Length>3)return "三维地面需要 1～3 块地面。";
            foreach(GroundSurface s in config.surfaces)
            {
                if(s==null || s.points==null || s.points.Length!=4)return "每块地面需要四个角点。";
                double sign=0,area=0;
                for(int i=0;i<4;i++)
                {
                    GroundPoint a=s.points[i],b=s.points[(i+1)%4],c=s.points[(i+2)%4];
                    if(!PointValid(a)||!PointValid(b)||!PointValid(c))return "角点必须位于屏幕内。";
                    double cross=(b.x-a.x)*(c.y-b.y)-(b.y-a.y)*(c.x-b.x);
                    if(Math.Abs(cross)<.0001 || sign*cross<0)return "四点必须按边缘顺序构成凸四边形，不能交叉或共线。";
                    sign=cross;area+=a.x*b.y-a.y*b.x;
                }
                if(Math.Abs(area)<.008)return "地面面积太小，请扩大范围。";
                if(!GateValid(s.entrance)||!GateValid(s.exit))return "请在同一条边上分别指定入口、出口的两个端点。";
                try
                {
                    GroundMap m=new GroundMap(s,1000,1000);double min=double.MaxValue,max=0;
                    for(int i=0;i<4;i++){double d=m.Denominator(i==1||i==2?1:0,i>=2?1:0);min=Math.Min(min,d);max=Math.Max(max,d);}
                    if(min<=0 || max/min>8)return "透视过强，请适当加宽远端地面。";
                }
                catch(ArgumentException){return "地面透视无效，请重新绘制。";}
            }
            return null;
        }
        private static bool PointValid(GroundPoint p){return p!=null && !float.IsNaN(p.x)&&!float.IsNaN(p.y)&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;}
        private static bool GateValid(GroundGate g){return g!=null && g.edge>=0&&g.edge<4 && g.start>=0&&g.end<=1&&g.end-g.start>=.025f;}
    }

    public sealed class GroundRoute
    {
        public readonly GroundMap Map;
        public readonly bool IsLine;
        public GroundVector Position,Target,Heading;
        public int Stage; // 0 entering, 1 active, 2 approaching exit, 3 crossing exit, 4 complete
        public double Opacity=1;
        private readonly Random random;
        private readonly GroundSurface surface;
        private GroundVector exitPoint;
        private bool exitGateReached;
        private readonly GroundVector lineA,lineB;
        private readonly double lineLength;
        private double entryFadeDistance;
        private GroundVector? steering;
        private GroundVector steeringHeading;
        public void SetSteering(GroundVector point){steering=point;steeringHeading=new GroundVector(point.x-Position.x,point.y-Position.y);SetHeading();}
        public void ClearSteering(){steering=null;SetHeading();}
        public double LastStepDistance {get;private set;}
        public GroundRoute(DesktopGroundConfig config,int index,double width,double height,Random random)
        {
            this.random=random;IsLine=config.mode=="Line";
            if(IsLine)
            {
                lineA=new GroundVector(config.line[0].x*width,config.line[0].y*height);lineB=new GroundVector(config.line[1].x*width,config.line[1].y*height);
                lineLength=GroundMap.Distance(lineA,lineB);bool forward=random.Next(2)==0;
                Position=new GroundVector(forward?-.02:1.02,0);Target=new GroundVector(forward?.08:.92,0);
            }
            else
            {
                surface=config.surfaces[index];Map=new GroundMap(surface,width,height);
                GroundVector p=Map.Gate(surface.entrance,random.NextDouble()),n=GroundMap.Inward(surface.entrance.edge);
                Position=new GroundVector(p.x-n.x*.025,p.y-n.y*.025);Target=new GroundVector(p.x+n.x*.08,p.y+n.y*.08);
            }
            SetHeading();Opacity=0;entryFadeDistance=Metric(Position,Target)/3;
        }
        public GroundVector Screen(GroundVector p){return IsLine?GroundVector.Lerp(lineA,lineB,p.x):Map.Project(p.x,p.y);}
        public GroundVector Foot {get{return Screen(Position);}}
        public double Metric(GroundVector a,GroundVector b)
        {double x=(b.x-a.x)*(IsLine?lineLength:Map.Width),y=(b.y-a.y)*(IsLine?0:Map.Depth);return Math.Sqrt(x*x+y*y);}
        private void SetHeading(){Heading=steering.HasValue?steeringHeading:new GroundVector(Target.x-Position.x,Target.y-Position.y);}
        public int BoundaryReplans;
        // A clipped position needs a reachable inward route, not another attempt
        // at the same boundary. Use screen space for skewed grounds.
        public void ConstrainFootprint(GroundVector[] region)
        {
            if(IsLine || Stage==0 || Stage>=3)return;
            GroundVector before=Foot,requested=Screen(Target);
            GroundVector placed=DesktopFootprintMath.Closest(region,before);
            GroundVector target=DesktopFootprintMath.Closest(region,requested);
            if(Stage==1 && (GroundMap.Distance(before,placed)>.25 || GroundMap.Distance(requested,target)>.25))
            {
                GroundVector interior=new GroundVector();
                foreach(var p in region){interior.x+=p.x/region.Length;interior.y+=p.y/region.Length;}
                // A convex region's centroid supplies a reachable escape path.
                target=interior;BoundaryReplans++;
            }
            Position=Map.Unproject(placed.x,placed.y);Target=Map.Unproject(target.x,target.y);
            SetHeading();RefreshOpacity();
        }
        private void RefreshOpacity()
        {
            // Only portal crossings fade; ordinary ground edges stay opaque.
            double interior=PortalInterior();
            Opacity=Stage==4?0:Stage==1 || Stage==2?1:Math.Max(0,Math.Min(1,interior/.035));
        }
        private double PortalInterior()
        {
            if(IsLine)return (Stage==0?Heading.x<0:exitPoint.x>=.5)?1-Position.x:Position.x;
            int edge=Stage==0?surface.entrance.edge:surface.exit.edge;
            return edge==0?Position.y:edge==1?1-Position.x:edge==2?1-Position.y:Position.x;
        }

        private void Wander()
        {
            Target=IsLine?new GroundVector(Position.x<.5?.96:.04,0):new GroundVector(.09+random.NextDouble()*.82,.09+random.NextDouble()*.82);
            SetHeading();
        }
        public void Avoid(){if(Stage==1)Wander();}
        public GroundVector Preview(double distance)
        {var aim=steering??Target;double d=Metric(Position,aim);return GroundVector.Lerp(Position,aim,d<.00001?1:Math.Min(1,distance/d));}
        // Pose-facing must retain the chosen tangent after this frame's short
        // waypoint is reached; movement itself must still stop at the waypoint.
        public GroundVector FacingPreview(double distance)
        {
            if(!steering.HasValue || Metric(Position,steering.Value)>.001)return Preview(distance);
            double length=Metric(new GroundVector(),Heading);
            return length<1e-8?Position:new GroundVector(Position.x+Heading.x*distance/length,Position.y+Heading.y*distance/length);
        }
        public void Step(double distance,double seconds=0,double fadeSeconds=0)
        {
            // Give the visible portal ramp time to render even for a fast actor
            // or a narrow gate. Legacy geometry-only callers retain distance steps.
            if(seconds>0 && fadeSeconds>0)
            {
                if(Stage==0)distance=Math.Min(distance,entryFadeDistance*seconds/fadeSeconds);
                else if(Stage==3 && !exitGateReached)
                {
                    double inward=PortalInterior(),remaining=Metric(Position,Target);
                    // A shoe inset can shift the approach diagonally. Travel at
                    // normal speed until the actual gate's fade band, then size
                    // the band in the route's direction rather than one UV axis.
                    if(inward>.035+1e-7)distance=Math.Min(distance,remaining*(inward-.035)/inward);
                    else if(inward>1e-10)distance=Math.Min(distance,remaining/inward*.035*seconds/fadeSeconds);
                }
            }
            GroundVector previous=Position;
            Position=Preview(distance);
            LastStepDistance=Metric(previous,Position);
            RefreshOpacity();
            if(Metric(Position,Target)>.01)return;
            if(Stage==0){Stage=1;Wander();}
            else if(Stage==1)Wander();
            else if(Stage==2)
            {
                Stage=3;
                Target=exitPoint;
                SetHeading();
            }
            else if(Stage==3)
            {
                if(!exitGateReached){exitGateReached=true;GroundVector n=IsLine?new GroundVector(exitPoint.x<.5?1:-1,0):GroundMap.Inward(surface.exit.edge);Target=new GroundVector(exitPoint.x-n.x*.035,exitPoint.y-n.y*.035);SetHeading();}
                else {Stage=4;Opacity=0;}
            }
            RefreshOpacity();
        }
        public void Retire()
        {
            if(Stage>=2)return;steering=null;Stage=2;exitGateReached=false;
            if(IsLine){exitPoint=new GroundVector(Position.x<.5?0:1,0);Target=new GroundVector(exitPoint.x+(exitPoint.x<.5?.055:-.055),0);}
            else
            {
                exitPoint=Map.Gate(surface.exit,random.NextDouble());GroundVector n=GroundMap.Inward(surface.exit.edge);
                Target=new GroundVector(exitPoint.x+n.x*.055,exitPoint.y+n.y*.055);
            }
            SetHeading();
        }
        public void PlaceScreenForDrag(double x,double y)
        {
            if(!IsLine){PlaceForDrag(Map.Unproject(x,y));return;}
            double dx=lineB.x-lineA.x,dy=lineB.y-lineA.y;
            double t=((x-lineA.x)*dx+(y-lineA.y)*dy)/(lineLength*lineLength);
            PlaceForDrag(new GroundVector(Math.Max(.04,Math.Min(.96,t)),0));
        }
        public void PlaceForDrag(GroundVector uv) {steering=null;Position=uv;Stage=1;Opacity=1;SetHeading();}
        public bool SetScreen(double x,double y)
        {
            GroundVector uv;
            if(IsLine){double dx=lineB.x-lineA.x,dy=lineB.y-lineA.y;uv=new GroundVector(((x-lineA.x)*dx+(y-lineA.y)*dy)/(lineLength*lineLength),0);}
            else uv=Map.Unproject(x,y);
            if(uv.x<.04||uv.x>.96||(!IsLine&&(uv.y<.04||uv.y>.96)))return false;
            steering=null;Position=uv;Stage=1;Opacity=1;SetHeading();return true;
        }
    }
}
