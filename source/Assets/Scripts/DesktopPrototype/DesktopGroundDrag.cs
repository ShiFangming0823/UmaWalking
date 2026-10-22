using System;

namespace UmaDesktop
{
    // Screen-space closest points avoid the large errors of clamping skewed UVs.
    public static class DesktopGroundDrag
    {
        public const double Inset=.04;
        public static GroundVector Segment(GroundVector p,GroundVector a,GroundVector b)
        {
            double x=b.x-a.x,y=b.y-a.y,length=x*x+y*y;
            double t=length<1e-12?0:((p.x-a.x)*x+(p.y-a.y)*y)/length;
            return GroundVector.Lerp(a,b,Math.Max(0,Math.Min(1,t)));
        }
        public static GroundVector Closest(GroundMap map,GroundVector p)
        {
            GroundVector[] corners={map.Project(Inset,Inset),map.Project(1-Inset,Inset),map.Project(1-Inset,1-Inset),map.Project(Inset,1-Inset)};
            bool positive=false,negative=false;
            for(int i=0;i<4;i++)
            {GroundVector a=corners[i],b=corners[(i+1)%4];double cross=(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);positive|=cross>1e-7;negative|=cross< -1e-7;}
            if(!(positive&&negative))return p;
            GroundVector best=corners[0];double distance=double.MaxValue;
            for(int i=0;i<4;i++){GroundVector q=Segment(p,corners[i],corners[(i+1)%4]);double d=GroundMap.Distance(p,q);if(d<distance){best=q;distance=d;}}
            return best;
        }
        public static GroundVector Place(DesktopGroundConfig config,double width,double height,GroundVector desired,int current,out int index)
        {
            index=0;
            if(config.mode=="Line")
            {
                GroundVector a=new GroundVector(config.line[0].x*width,config.line[0].y*height),b=new GroundVector(config.line[1].x*width,config.line[1].y*height);
                return Segment(desired,GroundVector.Lerp(a,b,Inset),GroundVector.Lerp(a,b,1-Inset));
            }
            GroundVector best=new GroundVector();double distance=double.MaxValue;
            // Prefer the current floor for overlapping valid positions and ties.
            for(int n=-1;n<config.surfaces.Length;n++)
            {
                int i=n<0?current:n;if(i<0||i>=config.surfaces.Length || n>=0&&i==current)continue;
                GroundMap map=new GroundMap(config.surfaces[i],width,height);GroundVector q=Closest(map,desired);double d=GroundMap.Distance(desired,q);
                if(d<distance-1e-6){distance=d;best=q;index=i;}
            }
            return best;
        }
    }
}
