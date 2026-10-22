Shader "Hidden/UmaDesktop/LayeredAlpha"
{
    Properties { _MainTex ("Texture", 2D) = "black" {} _Fxaa ("FXAA", Float) = 1 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Fxaa;
            float4 frag(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                float alpha = saturate(color.a);
                if (alpha <= 0.001) return 0;
                // Keep the silhouette's MSAA coverage. Apply FXAA to the opaque
                // interior so small clothing details are filtered without a matte.
                if (_Fxaa > 0.5 && alpha > 0.999)
                {
                    float2 pixel = _MainTex_TexelSize.xy;
                    float4 nw = tex2D(_MainTex, i.uv + float2(-1,-1)*pixel);
                    float4 ne = tex2D(_MainTex, i.uv + float2(1,-1)*pixel);
                    float4 sw = tex2D(_MainTex, i.uv + float2(-1,1)*pixel);
                    float4 se = tex2D(_MainTex, i.uv + float2(1,1)*pixel);
                    if (min(min(nw.a,ne.a),min(sw.a,se.a)) > 0.999)
                    {
                        float3 luma = float3(0.299,0.587,0.114);
                        float lnw=dot(nw.rgb,luma),lne=dot(ne.rgb,luma),lsw=dot(sw.rgb,luma),lse=dot(se.rgb,luma),lm=dot(color.rgb,luma);
                        float low=min(lm,min(min(lnw,lne),min(lsw,lse)));
                        float high=max(lm,max(max(lnw,lne),max(lsw,lse)));
                        float2 direction=float2(-((lnw+lne)-(lsw+lse)),(lnw+lsw)-(lne+lse));
                        float reduce=max((lnw+lne+lsw+lse)*0.03125,0.0078125);
                        direction=clamp(direction/(min(abs(direction.x),abs(direction.y))+reduce),-8,8)*pixel;
                        float4 p1=tex2D(_MainTex,i.uv+direction*(-1.0/6.0));
                        float4 p2=tex2D(_MainTex,i.uv+direction*(1.0/6.0));
                        float4 p3=tex2D(_MainTex,i.uv+direction*(-0.5));
                        float4 p4=tex2D(_MainTex,i.uv+direction*(0.5));
                        if (min(min(p1.a,p2.a),min(p3.a,p4.a)) > 0.999)
                        {
                            float3 a=0.5*(p1.rgb+p2.rgb);
                            float3 b=a*0.5+0.25*(p3.rgb+p4.rgb);
                            float lb=dot(b,luma);color.rgb=(lb<low||lb>high)?a:b;
                        }
                    }
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                    color.rgb=LinearToGammaSpace(max(0,color.rgb/alpha))*alpha;
                #endif
                return float4(clamp(color.rgb,0,alpha),alpha);
            }
            ENDCG
        }
    }
}
