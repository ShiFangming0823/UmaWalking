using System;
using System.Linq;
using UnityEngine;
namespace UmaDesktop
{
    public sealed class MiniExpressionController
    {
        private static readonly int EyeUv=Shader.PropertyToID("_UVOffset"),MainSt=Shader.PropertyToID("_MainTex_ST");
        private sealed class FacePart
        {
            public readonly Renderer Renderer;
            public readonly Material Material;
            private readonly MaterialPropertyBlock original=new MaterialPropertyBlock(),scratch=new MaterialPropertyBlock();
            public readonly Vector2 Offset,Scale;
            public readonly Vector4 EyeOffset;
            public FacePart(Renderer renderer)
            {
                Renderer=renderer;Material=renderer.sharedMaterial;Offset=Material.mainTextureOffset;Scale=Material.mainTextureScale;
                EyeOffset=Material.HasProperty(EyeUv)?Material.GetVector(EyeUv):Vector4.zero;
                renderer.GetPropertyBlock(original,0);
            }
            public void Restore()
            {
                Material.mainTextureOffset=Offset;Material.mainTextureScale=Scale;
                if(Material.HasProperty(EyeUv))Material.SetVector(EyeUv,EyeOffset);
                Renderer.SetPropertyBlock(original.isEmpty?null:original,0);
            }
            public void SetVector(int property,Vector4 value)
            {
                // Per-material blocks take precedence over renderer-wide animation
                // blocks. Preserve other source properties instead of clearing them.
                Renderer.GetPropertyBlock(scratch,0);
                if(scratch.isEmpty)Renderer.GetPropertyBlock(scratch);
                scratch.SetVector(property,value);Renderer.SetPropertyBlock(scratch,0);
            }
            public Vector4 Effective(int property)
            {
                Renderer.GetPropertyBlock(scratch,0);if(scratch.HasVector(property))return scratch.GetVector(property);
                Renderer.GetPropertyBlock(scratch);if(scratch.HasVector(property))return scratch.GetVector(property);
                return Material.GetVector(property);
            }
        }
        private readonly FacePart eyes,mouth,left,right;
        private readonly System.Random random;
        private readonly int characterId;
        private MiniExpressionPreset preset;
        public string Label {get;private set;}="Default";
        public MiniBehavior Action {get;private set;}
        public int Selections {get;private set;}
        public bool OverrideActive {get{return preset!=null;}}
        public MiniExpressionController(UmaContainerCharacter model,int id,System.Random random)
        {
            this.random=random;characterId=id;var renderers=model.GetComponentsInChildren<Renderer>(true);
            eyes=new FacePart(renderers.Single(r=>r.name=="M_Eye"));mouth=new FacePart(renderers.Single(r=>r.name=="M_Mouth"));
            left=new FacePart(renderers.Single(r=>r.name=="M_Mayu_L"));right=new FacePart(renderers.Single(r=>r.name=="M_Mayu_R"));
        }
        public void Select(MiniBehavior action)
        {
            RestoreDefaults();Action=action;Selections++;
            Label=MiniExpressionPolicy.Choose(random,action,characterId);preset=MiniExpressionPolicy.Find(Label);
        }
        public void RestoreDefaults(){eyes.Restore();mouth.Restore();left.Restore();right.Restore();}
        public void Apply()
        {
            // After graph evaluation: override just the chosen atlas channels.
            // Default mode preserves the game's original facial/ear animation.
            if(preset==null)return;
            var eye=new Vector4(.25f*(preset.EyeRight%4),-.125f*(preset.EyeRight/4),.25f*(preset.EyeLeft%4),-.125f*(preset.EyeLeft/4));
            eyes.Material.SetVector(EyeUv,eye);eyes.SetVector(EyeUv,eye);
            SetAtlas(mouth,preset.Mouth,8);SetAtlas(left,preset.EyebrowLeft,4);SetAtlas(right,preset.EyebrowRight,4);
        }
        private static void SetAtlas(FacePart part,int index,int rows)
        {
            var offset=new Vector2(.25f*(index%4),-(1f/rows)*(index/4));
            part.Material.mainTextureOffset=offset;part.Material.mainTextureScale=part.Scale;
            part.SetVector(MainSt,new Vector4(part.Scale.x,part.Scale.y,offset.x,offset.y));
        }
        private static int Index(float x,float y,int rows){return Mathf.RoundToInt(x*4)+4*Mathf.RoundToInt(-y*rows);}
        public int[] ActualIndices
        {
            get
            {
                Vector4 e=eyes.Effective(EyeUv),l=left.Effective(MainSt),r=right.Effective(MainSt),m=mouth.Effective(MainSt);
                return new[]{Index(l.z,l.w,4),Index(r.z,r.w,4),Index(e.z,e.w,8),Index(e.x,e.y,8),Index(m.z,m.w,8)};
            }
        }
        public bool MatchesPreset {get{return preset==null || ActualIndices.SequenceEqual(preset.Indices);}}
        // Only invoked by the opt-in expression audit, never by normal UI commands.
        public void SetAuditPreset(string label){RestoreDefaults();Label=label;preset=MiniExpressionPolicy.Find(label);Apply();}
    }
}
