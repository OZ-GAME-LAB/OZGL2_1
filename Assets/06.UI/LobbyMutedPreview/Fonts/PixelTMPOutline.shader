Shader "OZGL2/UI/Pixel TMP Outline"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _Color ("Text Color", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0.314, 0.286, 0.286, 1)
        _OutlineTexels ("Outline Width (Atlas Texels)", Range(0, 6)) = 3.5
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZTest [unity_GUIZTestMode]
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 position : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineTexels;
            float4 _ClipRect;

            Varyings Vert(AppData input)
            {
                Varyings output;
                output.position = input.vertex.xy;
                output.vertex = UnityPixelSnap(UnityObjectToClipPos(input.vertex));
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 stepUv = _MainTex_TexelSize.xy * _OutlineTexels;
                fixed face = tex2D(_MainTex, input.uv).a;
                fixed spread = face;
                spread = max(spread, tex2D(_MainTex, input.uv + float2(stepUv.x, 0)).a);
                spread = max(spread, tex2D(_MainTex, input.uv + float2(-stepUv.x, 0)).a);
                spread = max(spread, tex2D(_MainTex, input.uv + float2(0, stepUv.y)).a);
                spread = max(spread, tex2D(_MainTex, input.uv + float2(0, -stepUv.y)).a);
                spread = max(spread, tex2D(_MainTex, input.uv + stepUv).a);
                spread = max(spread, tex2D(_MainTex, input.uv - stepUv).a);
                spread = max(spread, tex2D(_MainTex, input.uv + float2(stepUv.x, -stepUv.y)).a);
                spread = max(spread, tex2D(_MainTex, input.uv + float2(-stepUv.x, stepUv.y)).a);

                fixed border = saturate(spread - face) * _OutlineColor.a;
                fixed alpha = saturate(face + border) * input.color.a;
                fixed3 rgb = lerp(_OutlineColor.rgb, input.color.rgb, face / max(face + border, 0.0001));
                fixed4 result = fixed4(rgb, alpha);

                #if UNITY_UI_CLIP_RECT
                    result.a *= UnityGet2DClipping(input.position, _ClipRect);
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
