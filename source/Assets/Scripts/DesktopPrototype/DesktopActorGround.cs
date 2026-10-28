using System;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopActor
    {
        public GroundRoute Ground;
        public int GroundIndex=-1;
        public float GroundDepthScale=1;
        private float groundWorldY;
        private bool groundCameraSaved;
        private Matrix4x4 originalProjection;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        public float ViewElevation {get{return Ground!=null && Ground.IsDesktop?(float)GroundRoute.DesktopElevation:settings.CameraElevation;}}
        public bool DesktopPlane {get{return Ground!=null && Ground.IsDesktop;}}
        public bool CustomGround {get{return Ground!=null;}}
        public float PortalOpacity {get{return Mathf.SmoothStep(0,1,Ground==null?DefaultPortalOpacity:(float)Ground.Opacity);}}
        public void AssignGround(DesktopGroundConfig config,int index,System.Random random,GroundRoute retained=null)
        {
            if(!groundCameraSaved)
            {
                originalProjection=Camera.projectionMatrix;
                originalCameraPosition=Camera.transform.position;originalCameraRotation=Camera.transform.rotation;
                Vector3 up=Camera.transform.up,root=Model.transform.position;
                groundWorldY=((FootViewport-.5f)*2*Camera.orthographicSize+Vector3.Dot(Camera.transform.position,up)-up.x*root.x-up.z*root.z)/up.y;
                if(gaitFloors.ContainsKey(Player.Name))groundWorldY=gaitFloors[Player.Name];
                else if(!float.IsInfinity(gaitFloorY))groundWorldY=gaitFloorY;
                groundCameraSaved=true;
            }
            Ground=config==null||config.mode=="Default"?null:retained??new GroundRoute(config,index,Overlay.ScreenArea.width,Overlay.ScreenArea.height,random);
            GroundIndex=Ground==null?-1:index;GroundDepthScale=1;GroundFitScale=1;
            AvoidanceVelocity=new GroundVector();desktopHeading.Reset(facingYaw);
            Camera.transform.SetPositionAndRotation(originalCameraPosition,originalCameraRotation);
            Camera.projectionMatrix=originalProjection;Camera.ResetCullingMatrix();
            if(DesktopPlane)
            {
                Vector3 offset=new Vector3(0,Mathf.Sin(ViewElevation*Mathf.Deg2Rad),Mathf.Cos(ViewElevation*Mathf.Deg2Rad))*5;
                Camera.transform.position=Model.transform.position+offset;
                Camera.transform.LookAt(Model.transform.position);
                ConfigureDesktopPlane();
            }
            if(Ground==null)return;
            ConstrainGroundFootprint();UpdateGroundPosition();ApplyGroundProjection();PlaceWindow();
        }
        private void UpdateGroundPosition()
        {
            if(Ground==null)return;
            GroundVector p=Ground.Foot;X=Overlay.ScreenArea.xMin+(float)p.x;
            GroundVector ahead=Ground.Screen(Ground.FacingPreview(20));
            if(Math.Abs(ahead.x-p.x)>.05)Direction=ahead.x>p.x?1:-1;
            if(Phase!=DesktopPhase.Exiting && Ground.Stage==1)Phase=DesktopPhase.Active;
        }
        private float MoveGround(float distance,float seconds)
        {Ground.Step(distance,seconds,settings.FadeSeconds);UpdateGroundPosition();return (float)Ground.LastStepDistance;}
        public float GroundYaw
        {
            get
            {
                if(Ground==null || Ground.IsLine)return -Direction*settings.FacingAngle;
                if(Interaction)return -Direction*90;
                // The drawn plane can be skewed or rotated. Infer heading from its local
                // screen tangent, while retaining the model camera's fixed elevation.
                GroundVector here=Ground.Foot,ahead=Ground.Screen(Ground.FacingPreview(5));
                double dx=ahead.x-here.x,dy=ahead.y-here.y;
                double elevation=Math.Max(.05,Math.Sin(ViewElevation*Math.PI/180));
                return dx*dx+dy*dy<1e-10?facingYaw:(float)(Math.Atan2(-dx,dy/elevation)*180/Math.PI);
            }
        }
        private void ApplyGroundProjection()
        {
            if(Ground==null)return;
            GroundDepthScale=GroundPerspective(Ground.Position)*GroundFitScale;
            // A floor homography is a map for feet, not a camera calibration for a
            // 3D body. Applying it to mesh vertices introduces arbitrary shear and
            // nonuniform scale on narrow/diagonal quads. Keep the original 3D camera
            // and uniformly scale its image about the calibrated sole anchor.
            Vector3 sole=SoleAnchor();
            Vector4 anchor=new Vector4(sole.x,sole.y,sole.z,1);
            Vector4 projected=originalProjection*(Camera.worldToCameraMatrix*anchor);
            float anchorX=projected.x/projected.w,anchorY=projected.y/projected.w;
            Matrix4x4 projection=originalProjection;
            Vector4 homogeneous=originalProjection.GetRow(3);
            projection.SetRow(0,GroundDepthScale*(originalProjection.GetRow(0)-anchorX*homogeneous));
            projection.SetRow(1,GroundDepthScale*originalProjection.GetRow(1)+
                (2*CanvasFootViewport-1-GroundDepthScale*anchorY)*homogeneous);
            Camera.projectionMatrix=projection;
            Camera.cullingMatrix=projection*Camera.worldToCameraMatrix;
        }
        public Rect GroundBounds(GroundVector? position=null)
        {
            float x=X,y=GroundY;
            if(position.HasValue && Ground!=null){GroundVector p=Ground.Screen(position.Value);x=Overlay.ScreenArea.xMin+(float)p.x;y=Overlay.ScreenArea.yMin+(float)p.y;}
            float half=Mathf.Min(SurfaceSize*.5f,ClearanceHalfWidth*Mathf.Max(1,GroundDepthScale)*1.2f);
            return new Rect(x-half-20,y-SurfaceSize,half*2+40,SurfaceSize+20);
        }
        public bool SetGroundScreen(float x,float y)
        {if(Ground==null || !Ground.SetScreen(x-Overlay.ScreenArea.xMin,y-Overlay.ScreenArea.yMin))return false;UpdateGroundPosition();return true;}
    }
}
