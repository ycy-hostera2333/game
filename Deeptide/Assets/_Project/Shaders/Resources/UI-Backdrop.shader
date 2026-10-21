// 战斗背景（策划/10 §3、§7.2、§11.2）：读打包贴图，R = 淡墨排线、B = 癫狂层。
// 墨色是 ink-mid（背景只用淡墨色阶，和角色拉开明度）；癫狂层着深渊青，显现程度 = max(全局 _Lunacy, _Song)。
// 纸色来自 Image 的颜色，所以清明时纸面提亮（LunacyFx）照常生效。
Shader "Deeptide/UI/Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Packed Background", 2D) = "black" {}
        _Color ("Paper (tint)", Color) = (0.91, 0.88, 0.80, 1)
        _InkColor ("Ink", Color) = (0.29, 0.27, 0.24, 1)
        _MadColor ("Mad Layer", Color) = (0.10, 0.38, 0.37, 1)
        _Song ("Song (min reveal)", Range(0, 1)) = 0

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
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            fixed4 _Color, _InkColor, _MadColor;
            float _Song;
            // 全局参数，不放进 Properties
            float _Lunacy, _ReduceFx;

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
                float4 p = tex2D(_MainTex, i.texcoord);
                float reveal = max(saturate(_Lunacy), _Song) * (1 - 0.5 * saturate(_ReduceFx));
                float3 rgb = lerp(i.color.rgb, _InkColor.rgb, p.r);
                rgb = lerp(rgb, _MadColor.rgb, p.b * reveal);
                return fixed4(rgb, i.color.a);
            }
            ENDCG
        }
    }
}
