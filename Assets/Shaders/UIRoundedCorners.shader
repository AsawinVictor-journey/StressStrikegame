// Rounds the corners of any UI Graphic (RawImage / Image) analytically, rather than
// by masking against a rounded sprite. The corner comes from a signed distance field
// evaluated per fragment, so it stays crisp at any canvas scale or resolution and
// costs no extra texture memory.
//
// _Size must be pushed from RoundedCorners.cs with the RectTransform's pixel size.
// Without the real aspect the SDF is evaluated in UV space, where one unit of X is a
// different number of pixels than one unit of Y, and the corners come out elliptical.
Shader "UI/RoundedCorners"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Radius ("Corner Radius (fraction of short side)", Range(0, 0.5)) = 0.15
        _Size ("Rect Size in px (set from script)", Vector) = (100, 100, 0, 0)

        // Standard UI plumbing so this behaves inside Mask / RectMask2D like any
        // other UI material.
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
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "RoundedCorners"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask          : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            float _Radius;
            float4 _Size;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                float2 pixelSize = OUT.vertex.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskSoftness = float2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                  0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Signed distance to an axis-aligned rounded box, evaluated in the rect's
            // own pixel space. Negative inside, zero on the edge, positive outside.
            float sdRoundedBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Fall back to a unit square if the script never pushed a size, so the
                // shader degrades to "slightly rounded" instead of dividing by zero.
                float2 size = (_Size.x > 0.0 && _Size.y > 0.0) ? _Size.xy : float2(1.0, 1.0);

                float2 halfSize = size * 0.5;
                float2 p = (IN.texcoord - 0.5) * size;

                // Radius is a fraction of the SHORT side, so 0.5 gives a clean pill and
                // the corners can never overlap on a wide rect.
                float r = clamp(_Radius * min(size.x, size.y), 0.0, min(halfSize.x, halfSize.y));

                float dist = sdRoundedBox(p, halfSize, r);

                // fwidth is the per-pixel rate of change of dist, so this feathers over
                // roughly one screen pixel no matter the scale, rotation or DPI.
                float aa = max(fwidth(dist) * 0.5, 1e-5);
                color.a *= 1.0 - smoothstep(-aa, aa, dist);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}
