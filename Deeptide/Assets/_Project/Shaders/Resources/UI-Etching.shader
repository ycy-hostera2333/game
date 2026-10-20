// 版画插图（策划/10 §11.2、§11.4 的 Etching/CardFace）：读打包贴图，颜色全部来自材质。
// R = 墨的覆盖率、G = 深渊遮罩、B = 癫狂层的墨（× _Reveal）、A = 轮廓。纸面上加一层很淡的纸纹。
// 用在插图窗的遮罩（Mask）里，所以带 UI 的模板测试参数。
Shader "Deeptide/UI/Etching"
{
    Properties
    {
        [PerRendererData] _MainTex ("Packed Art", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PaperColor ("Paper", Color) = (0.91, 0.88, 0.80, 1)
        _InkColor ("Ink", Color) = (0.11, 0.10, 0.09, 1)
        _AbyssColor ("Abyss", Color) = (0.10, 0.38, 0.37, 1)
        _MadColor ("Mad Layer Ink", Color) = (0.11, 0.10, 0.09, 1)
        _Reveal ("Mad Layer Reveal", Range(0, 1)) = 0
        _Grain ("Paper Grain", Range(0, 0.2)) = 0.05

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "DeeptideNoise.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 local : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _PaperColor, _InkColor, _AbyssColor, _MadColor;
            float _Reveal, _Grain;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 打包贴图按线性数据导入（sRGB 关），通道值就是覆盖率
                float4 p = tex2D(_MainTex, i.texcoord);
                float ink = p.r;
                float abyss = p.g;
                float mad = p.b * _Reveal;

                float grain = (DeeptideNoise(i.texcoord * float2(260, 180)) - 0.5) * _Grain;
                float3 paper = _PaperColor.rgb * (1 - grain);
                float3 rgb = lerp(paper, _InkColor.rgb, ink);
                rgb = lerp(rgb, _AbyssColor.rgb, abyss);
                rgb = lerp(rgb, _MadColor.rgb, mad);

                float a = max(p.a, mad) * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(a - 0.001);
                #endif
                return fixed4(rgb * i.color.rgb, a);
            }
            ENDCG
        }
    }
}
