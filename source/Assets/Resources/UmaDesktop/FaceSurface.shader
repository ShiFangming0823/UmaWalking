Shader "Hidden/UmaWalking/FaceSurface"
{
    Properties
    {
        _MainTex ("Face atlas", 2D) = "white" {}
        _CharaColor ("Character tint", Color) = (1,1,1,1)
        _UVOffset ("Eye atlas offsets", Vector) = (0,0,0,0)
        _EyeMode ("Separate eye offsets", Float) = 0
        _UvBoundsLeft ("Left patch bounds", Vector) = (0,0,1,1)
        _UvBoundsRight ("Right patch bounds", Vector) = (0,0,1,1)
        _StencilMask ("Stencil reference", Float) = 100
        _StencilComp ("Stencil comparison", Float) = 8
        _StencilOp ("Stencil operation", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" "RenderType"="Opaque" }
        Pass
        {
            Name "FaceSurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            Stencil { Ref [_StencilMask] Comp [_StencilComp] Pass [_StencilOp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST,_MainTex_TexelSize,_UVOffset,_CharaColor;
                float4 _UvBoundsLeft,_UvBoundsRight;
                float _EyeMode;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;float4 bounds:TEXCOORD2;};
            Varyings vert(Attributes input)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                bool left=input.uv.x<=0.125;
                float4 bounds=(_EyeMode>0.5&&!left)?_UvBoundsRight:_UvBoundsLeft;
                float2 offset=_MainTex_ST.zw+(_EyeMode>0.5?(left?_UVOffset.xy:_UVOffset.zw):float2(0,0));
                o.uv=input.uv*_MainTex_ST.xy+offset;
                o.local=(input.uv-bounds.xy)/max(bounds.zw-bounds.xy,0.000001);
                o.bounds=float4(bounds.xy*_MainTex_ST.xy+offset,bounds.zw*_MainTex_ST.xy+offset);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                // Clamp within this expression patch, rather than at the outer
                // edge of the complete atlas, to avoid sampling another tile.
                float2 inset=_MainTex_TexelSize.xy*0.5;
                float2 uv=clamp(i.uv,i.bounds.xy+inset,i.bounds.zw-inset);
                half4 sample=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                // DXT atlases contain nearly identical backing colors (for
                // example blue 203 and 206 on otherwise equal skin). Those
                // blocks form rectangular seams that MSAA cannot remove.
                // Snap only near-backing colors; retain eyes, teeth and lips.
                half3 backing=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,_MainTex_TexelSize.xy*1.5).rgb;
                half3 delta=abs(sample.rgb-backing);
                half difference=max(delta.r,max(delta.g,delta.b));
                half repair=1-smoothstep(0.020,0.040,difference);
                sample.rgb=lerp(sample.rgb,backing,repair);
                return half4(sample.rgb*_CharaColor.rgb,sample.a);
            }
            ENDHLSL
        }
    }
}
