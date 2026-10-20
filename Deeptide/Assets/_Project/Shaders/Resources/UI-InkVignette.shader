// 癫狂暗角（策划/10 §7.2）：墨从四角渗进来，边缘带噪声起伏；底下再铺一层很淡的深渊青。
// 放在战斗舞台之上、UI 之下，所以只盖住背景和角色，不盖卡牌和数字。
Shader "Deeptide/UI/InkVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _InkColor ("Ink", Color) = (0.11, 0.10, 0.09, 1)
        _WashColor ("Wash", Color) = (0.10, 0.38, 0.37, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Wash ("Wash", Range(0, 1)) = 0
        _Aspect ("Aspect", Float) = 1.7778

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            #include "DeeptideNoise.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            fixed4 _Color, _InkColor, _WashColor;
            float _Intensity, _Wash, _Aspect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 c = (i.texcoord - 0.5) * float2(_Aspect, 1);
                float r = length(c) / length(float2(_Aspect, 1) * 0.5);   // 中心 0 → 四角 1
                float n = DeeptideFbm(i.texcoord * float2(7, 4) + _Time.y * 0.03);
                float edge = 1 - _Intensity * 1.25;
                float m = smoothstep(edge, edge + 0.3, r + (n - 0.5) * 0.2) * step(0.001, _Intensity);
                float a = m * 0.94;
                float wash = _Wash * (1 - a);
                float outA = a + wash;
                float3 rgb = (_InkColor.rgb * a + _WashColor.rgb * wash) / max(outA, 1e-4);
                return fixed4(rgb, outA * i.color.a);
            }
            ENDCG
        }
    }
}
