using System;
using System.Collections.Generic;

namespace UmaDesktop
{
    // Erode a convex screen-space ground by an elliptical footprint. Checking the
    // centre alone is insufficient, particularly for narrow diagonal platforms.
    public static class DesktopFootprintMath
    {
        public static GroundVector[] Corners(GroundMap map)
        {return new[]{map.Project(0,0),map.Project(1,0),map.Project(1,1),map.Project(0,1)};}
        private static double Winding(GroundVector[] p)
        {double area=0;for(int i=0;i<p.Length;i++){var q=p[(i+1)%p.Length];area+=p[i].x*q.y-p[i].y*q.x;}return area>=0?1:-1;}
        public static GroundVector[] Region(GroundMap map,double radiusX,double radiusY,double padding,GroundVector[] visibleSoles=null)
        {
            var corners=Corners(map);double winding=Winding(corners);var polygon=new List<GroundVector>(corners);
            for(int edge=0;edge<4 && polygon.Count>0;edge++)
            {
                var a=corners[edge];var b=corners[(edge+1)%4];double dx=b.x-a.x,dy=b.y-a.y,length=Math.Sqrt(dx*dx+dy*dy);
                double nx=-dy/length*winding,ny=dx/length*winding;
                double margin=padding+Math.Sqrt(nx*nx*radiusX*radiusX+ny*ny*radiusY*radiusY);
                if(visibleSoles!=null)foreach(var sole in visibleSoles)margin=Math.Max(margin,padding-nx*sole.x-ny*sole.y);
                var next=new List<GroundVector>();var previous=polygon[polygon.Count-1];double before=(previous.x-a.x)*nx+(previous.y-a.y)*ny-margin;
                foreach(var current in polygon)
                {
                    double after=(current.x-a.x)*nx+(current.y-a.y)*ny-margin;
                    if((before>=0)!=(after>=0))next.Add(GroundVector.Lerp(previous,current,before/(before-after)));
                    if(after>=0)next.Add(current);previous=current;before=after;
                }
                polygon=next;
            }
            return polygon.ToArray();
        }
        public static bool Contains(GroundVector[] polygon,GroundVector point,double tolerance=.001)
        {
            if(polygon.Length<3)return false;double sign=Winding(polygon);
            for(int i=0;i<polygon.Length;i++)
            {var a=polygon[i];var b=polygon[(i+1)%polygon.Length];double cross=((b.x-a.x)*(point.y-a.y)-(b.y-a.y)*(point.x-a.x))*sign;if(cross< -tolerance)return false;}
            return true;
        }
        public static GroundVector Closest(GroundVector[] polygon,GroundVector point)
        {
            if(polygon.Length==0)throw new ArgumentException("No room for the character's footprint.");
            if(Contains(polygon,point))return point;
            var best=polygon[0];double distance=double.MaxValue;
            for(int i=0;i<polygon.Length;i++)
            {var p=DesktopGroundDrag.Segment(point,polygon[i],polygon[(i+1)%polygon.Length]);double d=GroundMap.Distance(point,p);if(d<distance){distance=d;best=p;}}
            return best;
        }
    }
}
