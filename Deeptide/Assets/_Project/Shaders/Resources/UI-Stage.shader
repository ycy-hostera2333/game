// 战斗舞台上的角色（策划/10 §5.2、§7）：癫狂色差、低理智波纹、受击墨闪、死亡时溶解成墨向下流。
// 色差和波纹读全局 _Lunacy（0–1，LunacyFx 设置）和 _ReduceFx（「降低癫狂特效」开关）。
// 两种贴图：灰盒剪影（_Packed = 0，只用 alpha，颜色来自 Image）；正式角色图（_Packed = 1，10 §11.2 的打包贴图：
// R 墨、G 深渊遮罩、B 癫狂层的墨、A 轮廓；癫狂层按 _Lunacy 显现，可以长出轮廓之外）。
// UI 不用这个着色器，所以癫狂效果不会糊掉卡牌和数字（10 §1.2 支柱 4）。
Shader "Deeptide/UI/Stage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (0.81, 0.78, 0.69, 1)
        _Flash ("Flash", Range(0, 1)) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _FringeA ("Fringe A (teal)", Color) = (0.31, 0.76, 0.69, 1)
        _FringeB ("Fringe B (blood)", Color) = (0.88, 0.45, 0.42, 1)
        _Seed ("Seed", Float) = 0
        _Packed ("Packed Art", Float) = 0
        _PaperColor ("Paper", Color) = (0.91, 0.88, 0.80, 1)
        _InkColor ("Ink", Color) = (0.11, 0.10, 0.09, 1)
        _AbyssColor ("Abyss", Color) = (0.10, 0.38, 0.37, 1)
        _MadColor ("Mad Layer", Color) = (0.31, 0.76, 0.69, 1)

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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            fixed4 _Color, _FlashColor, _FringeA, _FringeB, _PaperColor, _InkColor, _AbyssColor, _MadColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Flash, _Dissolve, _Seed, _Packed;
            // 全局参数，不放进 Properties，否则材质上的值会盖掉全局值
            float _Lunacy, _ReduceFx;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // 打包贴图的 alpha = 轮廓 ∪ 显现出来的癫狂层
            float AlphaOf(float4 t, float reveal) { return _Packed > 0.5 ? max(t.a, t.b * reveal) : t.a; }
            float AlphaAt(float2 uv, float reveal) { return AlphaOf(tex2D(_MainTex, uv) + _TextureSampleAdd, reveal); }

            fixed4 frag(v2f i) : SV_Target
            {
                float reduce = saturate(_ReduceFx);
                float lun = saturate(_Lunacy);
                float ripple = saturate((lun - 0.6) / 0.3) * (1 - reduce);

                float2 uv = i.texcoord;
                uv.x += sin(uv.y * 17 + _Time.y * 2.6 + _Seed) * 0.009 * ripple;
                uv.y += sin(uv.x * 13 + _Time.y * 2.1 + _Seed * 0.7) * 0.004 * ripple;

                float off = 0.012 * lun * (1 - 0.7 * reduce);
                float4 tex = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float a = AlphaOf(tex, lun);
                float fa = saturate(AlphaAt(uv + float2(off, 0), lun) - a) * 0.8;
                float fb = saturate(AlphaAt(uv - float2(off, 0), lun) - a) * 0.8;

                // 本体颜色：剪影用 Image 的颜色；打包贴图按通道从色板上色（10 §11.2）
                float3 ink = i.color.rgb;
                if (_Packed > 0.5)
                {
                    ink = lerp(_PaperColor.rgb, _InkColor.rgb, tex.r);
                    ink = lerp(ink, _AbyssColor.rgb, tex.g);
                    ink = lerp(ink, _MadColor.rgb, tex.b * lun);
                    ink *= i.color.rgb;
                }

                // 「over」合成：先两道色差，再盖上本体
                float3 rgb = _FringeA.rgb * fa;
                float al = fa;
                rgb = _FringeB.rgb * fb + rgb * (1 - fb);
                al = fb + al * (1 - fb);
                float3 body = lerp(ink, _FlashColor.rgb, _Flash);
                rgb = body * a + rgb * (1 - a);
                al = a + al * (1 - a);
                float3 straight = rgb / max(al, 1e-4);

                // 死亡：从上往下溶解，按列错开成流淌的墨
                float colNoise = DeeptideNoise(float2(uv.x * 24 + _Seed, 0.5));
                float drip = DeeptideNoise(float2(uv.x * 46 + _Seed, uv.y * 2.5));
                float cut = 1 - _Dissolve * 1.3;
                float keep = 1 - smoothstep(cut - 0.02, cut + 0.02, uv.y + (colNoise - 0.5) * 0.18 - drip * 0.25 * _Dissolve);
                al *= keep * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                al *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(al - 0.001);
                #endif
                return fixed4(straight, al);
            }
            ENDCG
        }
    }
}
