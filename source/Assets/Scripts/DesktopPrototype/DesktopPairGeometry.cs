using System.Collections.Generic;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopActor
    {
        // Clip-specific screen intervals, measured at all 61 calibration poses in
        // both facing directions. Accessories and large characters participate.
        private readonly Dictionary<string,Vector2[]> pairFootprints=new Dictionary<string,Vector2[]>();
        private void MeasurePairPoint(string clip,Vector3 relative)
        {
            Vector2[] ranges;
            if(!pairFootprints.TryGetValue(clip,out ranges))
            {
                ranges=new[] {new Vector2(float.PositiveInfinity,float.NegativeInfinity),new Vector2(float.PositiveInfinity,float.NegativeInfinity)};
                pairFootprints.Add(clip,ranges);
            }
            for(int i=0;i<2;i++)
            {
                int direction=i==0?-1:1;
                Vector3 world=Model.transform.position+Quaternion.Euler(0,-direction*settings.FacingAngle,0)*relative;
                float pixel=(Camera.WorldToViewportPoint(world).x-0.5f)*SurfaceSize;
                ranges[i]=new Vector2(Mathf.Min(ranges[i].x,pixel),Mathf.Max(ranges[i].y,pixel));
            }
        }
        public Vector2 PairFootprint(UmaDatabaseEntry clip,int direction)
        {
            Vector2[] ranges;
            if(!pairFootprints.TryGetValue(clip.Name,out ranges))
                return new Vector2(InteractionOffset-InteractionHalfWidth,InteractionOffset+InteractionHalfWidth);
            Vector2 range=ranges[direction<0?0:1];float padding=12+Height*0.02f;
            return new Vector2(Mathf.Max(-SurfaceSize*0.5f,range.x-padding),Mathf.Min(SurfaceSize*0.5f,range.y+padding));
        }
    }
}
