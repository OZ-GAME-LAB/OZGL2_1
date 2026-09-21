Shader "OZGL2/UI/Skill Category"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FaceColor ("Ivory White Face", Color) = (0.972549,0.949020,0.921569,1)
        _OutlineColor ("Category Outline", Color) = (0.647059,0.250980,0.247059,1)
        _OutlineTexels ("Outline Width (Source Texels)", Range(0,6)) = 2
        _ShadowCutoff ("Ignore Dark Source Noise", Range(0,0.4)) = 0.15
        [Toggle(SKILL_SLOT_TINT)] _SlotTint ("Soft Slot Tint Only", Float) = 0
        [Toggle(SKILL_EMPTY_FRAME)] _EmptyFrame ("Desaturated Empty Frame", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma shader_feature_local _ SKILL_SLOT_TINT
            #pragma shader_feature_local _ SKILL_EMPTY_FRAME
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color, _FaceColor, _OutlineColor;
            float _OutlineTexels, _ShadowCutoff;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 clipPosition = UnityObjectToClipPos(input.vertex);
                output.vertex = clipPosition;
                output.uv = input.uv;
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace()) input.color.rgb = UIGammaToLinear(input.color.rgb);
                output.color = input.color * _Color;
                float2 pixelSize = clipPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            fixed Coverage(float2 uv)
            {
                // 원본 실루엣과 투명한 구멍을 사용하되, 검은 소스 테두리를 흰색으로 채우지 않는다.
                fixed4 sample = tex2D(_MainTex, uv);
                fixed brightness = max(sample.r, max(sample.g, sample.b));
                float inBounds = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return sample.a * smoothstep(_ShadowCutoff, _ShadowCutoff + 0.01, brightness) * inBounds;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 result;
                #if SKILL_EMPTY_FRAME
                    // 빈 슬롯만 원본 명암/알파를 보존하며 무채색으로 표시한다.
                    // Button 호버 tint까지 합친 뒤 채도를 제거하여 색이 다시 묻지 않게 한다.
                    fixed4 sample = tex2D(_MainTex, input.uv) * input.color;
                    fixed gray = dot(sample.rgb, fixed3(0.2126, 0.7152, 0.0722));
                    result = fixed4(gray, gray, gray, sample.a);
                #elif SKILL_SLOT_TINT
                    // 별도 비트맵 없이 슬롯 안쪽만 옅게 물들이고 가장자리에서 사라진다.
                    float2 distance = abs(input.uv * 2 - 1);
                    fixed fade = 1 - smoothstep(0.55, 1, max(distance.x, distance.y));
                    result = fixed4(input.color.rgb, input.color.a * fade);
                #else
                    float2 offset = _MainTex_TexelSize.xy * _OutlineTexels;
                    fixed face = Coverage(input.uv);
                    fixed spread = face;
                    spread = max(spread, Coverage(input.uv + float2(offset.x, 0)));
                    spread = max(spread, Coverage(input.uv - float2(offset.x, 0)));
                    spread = max(spread, Coverage(input.uv + float2(0, offset.y)));
                    spread = max(spread, Coverage(input.uv - float2(0, offset.y)));
                    spread = max(spread, Coverage(input.uv + offset));
                    spread = max(spread, Coverage(input.uv - offset));
                    spread = max(spread, Coverage(input.uv + float2(offset.x, -offset.y)));
                    spread = max(spread, Coverage(input.uv + float2(-offset.x, offset.y)));
                    fixed border = saturate(spread - face) * _OutlineColor.a;
                    fixed alpha = face * _FaceColor.a + border;
                    // RGB는 이미지 원래 색이나 Button tint가 아닌 공통 흰색/분류색만 사용한다.
                    fixed3 faceColor = _FaceColor.rgb;
                    fixed3 outlineColor = _OutlineColor.rgb;
                    // 팔레트는 문서/Inspector와 같은 sRGB 값으로 보관한다.
                    if (!IsGammaSpace()) { faceColor = GammaToLinearSpace(faceColor); outlineColor = GammaToLinearSpace(outlineColor); }
                    fixed3 rgb = (faceColor * face * _FaceColor.a + outlineColor * border) / max(alpha, 0.0001);
                    result = fixed4(rgb, alpha * input.color.a);
                #endif
                #if UNITY_UI_CLIP_RECT
                    float2 edge = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                    result.a *= edge.x * edge.y;
                #endif
                #if UNITY_UI_ALPHACLIP
                    clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
