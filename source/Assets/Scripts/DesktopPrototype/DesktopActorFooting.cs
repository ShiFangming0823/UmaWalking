using System;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopActor
    {
        private float gaitFloorY=float.PositiveInfinity,gaitFootRadius,sampledPoseFloor;
        private readonly System.Collections.Generic.Dictionary<string,float> gaitFloors=new System.Collections.Generic.Dictionary<string,float>();
        public float GroundFitScale=1,FootprintRadiusX,FootprintRadiusY;
        public int PoseTestVersion;
        public string GaitContactClip;public float GaitContactTime;
        public const float CustomFootViewport=.08f;
        private float desktopFootViewport=CustomFootViewport;
        public float CanvasFootViewport {get{return DesktopPlane?desktopFootViewport:Ground==null?FootViewport:CustomFootViewport;}}
        public float CalibratedFloorY {get{return groundWorldY;}}
        private Transform[] groundFeet;
        private Vector3 SoleAnchor()
        {
            Vector3 p=Model.transform.position;
            if(groundFeet!=null && groundFeet.Length==2 && footBounds!=null)
                p=(groundFeet[0].TransformPoint(footBounds[0].center)+groundFeet[1].TransformPoint(footBounds[1].center))*.5f;
            p.y=groundWorldY;return p;
        }
        private float FootRadiusNow(bool calibrating)
        {
            if(footBounds==null || groundFeet==null || groundFeet.Length!=2)return Height*Camera.orthographicSize/SurfaceSize*.12f;
            float radius=0;
            for(int f=0;f<2;f++)for(int c=0;c<8;c++)
            {
                var shoe=footBounds[f];Vector3 local=shoe.center+Vector3.Scale(shoe.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                Vector3 world=groundFeet[f].TransformPoint(local),relative=world-SoleAnchor();
                radius=Mathf.Max(radius,new Vector2(relative.x,relative.z).magnitude);
                if(calibrating)sampledPoseFloor=Mathf.Min(sampledPoseFloor,world.y);
                if(calibrating && world.y<gaitFloorY){gaitFloorY=world.y;GaitContactClip=Player.Name;GaitContactTime=Player.TimeSeconds;}
            }
            return radius;
        }
        private float GroundPerspective(GroundVector uv)
        {
            if(Ground==null || Ground.IsLine || Ground.IsDesktop)return 1;
            var m=Ground.Map;double reference=Math.Min(Math.Min(m.Denominator(0,0),m.Denominator(1,0)),Math.Min(m.Denominator(0,1),m.Denominator(1,1)));
            return (float)(reference/m.Denominator(uv.x,uv.y));
        }
        private void ConstrainGroundFootprint()
        {
            if(Ground==null || Ground.IsLine)return;
            if(DesktopPlane)
            {
                ConfigureDesktopPlane();Ground.ConstrainFootprint(Ground.DesktopRegion);
                UpdateGroundPosition();PlaceWindow();return;
            }
            if(Ground.Stage==0 || Ground.Stage>=3)return;
            float radius=Mathf.Max(gaitFootRadius,FootRadiusNow(false));
            for(int iteration=0;iteration<4;iteration++)
            {
                // Keep the fit stable on this floor instead of re-expanding
                // against the boundary on every running animation frame.
                GroundVector[] region;
                float basePixels=radius*SurfaceSize/(2*Camera.orthographicSize)*GroundPerspective(Ground.Position);
                float tilt=Mathf.Max(.05f,Mathf.Sin(ViewElevation*Mathf.Deg2Rad));
                do
                {
                    FootprintRadiusX=basePixels*GroundFitScale;FootprintRadiusY=FootprintRadiusX*tilt;
                    // The floor bounds the horizontal footprint, not the raised
                    // shoe's image. Constraining its animated height pushes the
                    // whole actor backwards on every stride on a narrow plane.
                    region=DesktopFootprintMath.Region(Ground.Map,FootprintRadiusX,FootprintRadiusY,2);
                    if(region.Length>=3)break;GroundFitScale*=.85f;
                }while(GroundFitScale>.01f);
                if(region.Length<3)return;
                GroundVector before=Ground.Foot;
                Ground.ConstrainFootprint(region);GroundVector after=Ground.Foot;
                if(GroundMap.Distance(before,after)<.01)break;
            }
            UpdateGroundPosition();PlaceWindow();
        }
        private void ConfigureDesktopPlane()
        {
            // Use the complete sampled motion envelope, including hair/accessories,
            // rather than a foot point. Margins and fit never vary with desktop Y.
            float pixels=SurfaceSize/(2*Camera.orthographicSize);
            float radius=Mathf.Max(envelopeRadius,Height/pixels*.3f);
            float sine=Mathf.Sin(ViewElevation*Mathf.Deg2Rad),cosine=Mathf.Cos(ViewElevation*Mathf.Deg2Rad);
            float lowFloor=groundWorldY,highFloor=groundWorldY;
            foreach(float floor in gaitFloors.Values){lowFloor=Mathf.Min(lowFloor,floor);highFloor=Mathf.Max(highFloor,floor);}
            float above=Mathf.Max(Height/pixels,envelopeTop+Model.transform.position.y-lowFloor)*cosine+radius*sine;
            float below=Mathf.Max(0,highFloor-Model.transform.position.y-envelopeBottom)*cosine+radius*sine;
            float half=radius*pixels+8,top=above*pixels+8,bottom=below*pixels+8;
            float fit=Mathf.Min(1,Mathf.Min((SurfaceSize-20)/(2*half),(SurfaceSize-20)/(top+bottom)));
            fit=Mathf.Min(fit,Mathf.Min((Overlay.ScreenArea.width-24)/(2*half),(Overlay.ScreenArea.height-24)/(top+bottom)));
            GroundFitScale=Mathf.Max(.01f,fit);
            half*=GroundFitScale;top*=GroundFitScale;bottom*=GroundFitScale;
            desktopFootViewport=(bottom+8)/SurfaceSize;
            FootprintRadiusX=radius*pixels*GroundFitScale;FootprintRadiusY=FootprintRadiusX*sine;
            Ground.SetDesktopBounds(half+4,top+4,half+4,bottom+4);
        }
        public void SampleGroundContact(float yaw)
        {
            Schedule.ForceGait(MiniBehavior.Walk);groundWorldY=gaitFloorY;
            if(GaitContactClip!=null){Player.Play(catalog.Find(GaitContactClip),true);Player.Seek(GaitContactTime);}
            FreezePosition();facingYaw=yaw;Model.transform.rotation=Quaternion.Euler(0,yaw,0);
            ConstrainGroundFootprint();ApplyGroundProjection();PlaceWindow();
        }
        public float SoleClearancePixels
        {
            get
            {
                if(groundFeet==null || footBounds==null)return 0;
                float lowest=float.PositiveInfinity;
                for(int f=0;f<2;f++)for(int c=0;c<8;c++)
                {var b=footBounds[f];var point=groundFeet[f].TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));lowest=Mathf.Min(lowest,point.y);}
                return (lowest-groundWorldY)*SurfaceSize/(2*Camera.orthographicSize)*GroundDepthScale*Mathf.Cos(ViewElevation*Mathf.Deg2Rad);
            }
        }
        private GroundVector[] ShoeOffsets(float scale)
        {
            if(footBounds==null || groundFeet==null)return new GroundVector[0];
            var result=new GroundVector[16];float pixels=SurfaceSize/(2*Camera.orthographicSize)*scale;
            var anchor=SoleAnchor();
            for(int f=0;f<2;f++)for(int c=0;c<8;c++)
            {var b=footBounds[f];Vector3 p=groundFeet[f].TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)))-anchor;result[f*8+c]=new GroundVector(Vector3.Dot(Camera.transform.right,p)*pixels,-Vector3.Dot(Camera.transform.up,p)*pixels);}
            return result;
        }
        public GroundVector[] ActualShoePixels()
        {
            var result=ShoeOffsets(GroundDepthScale);for(int i=0;i<result.Length;i++)result[i]=new GroundVector(result[i].x+X,result[i].y+GroundY);return result;
        }
        public GroundVector[] ProjectedFootContacts()
        {
            if(footBounds==null || groundFeet==null)return new GroundVector[0];
            var result=new GroundVector[16];float pixels=SurfaceSize/(2*Camera.orthographicSize)*(Ground==null?1:GroundDepthScale);
            for(int f=0;f<2;f++)for(int c=0;c<8;c++)
            {
                var shoe=footBounds[f];Vector3 p=groundFeet[f].TransformPoint(shoe.center+Vector3.Scale(shoe.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));
                p.y=groundWorldY;Vector3 anchor=SoleAnchor(),d=p-anchor;
                result[f*8+c]=new GroundVector(X+Vector3.Dot(Camera.transform.right,d)*pixels,GroundY-Vector3.Dot(Camera.transform.up,d)*pixels);
            }
            return result;
        }
    }
}
