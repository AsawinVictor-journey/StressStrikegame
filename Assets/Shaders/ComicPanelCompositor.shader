Shader "UI/ComicPanelCompositor"
{
    Properties
    {
        // Unused by the fragment shader, but CanvasRenderer always pushes RawImage.texture
        // into "_MainTex" — without this property Unity logs an error every frame.
        [NoScaleOffset] _MainTex ("Sprite Texture (unused)", 2D) = "white" {}

        [NoScaleOffset] _LeftTex ("Left Camera Tex", 2D) = "black" {}
        [NoScaleOffset] _CenterTex ("Center Camera Tex", 2D) = "black" {}
        [NoScaleOffset] _RightTex ("Right Camera Tex", 2D) = "black" {}

        _DividerLeftX ("Left Divider X (0-1)", Range(0,1)) = 0.3333
        _DividerRightX ("Right Divider X (0-1)", Range(0,1)) = 0.6667
        _SlantAmount ("Slant Amount (+inward / -outward)", Range(-0.6, 0.6)) = 0.16
        _Aspect ("Screen Aspect (W/H)", Float) = 1.7777778

        _BorderWidth ("Divider Border Width", Range(0, 0.05)) = 0.0035
        _FrameBorderWidth ("Outer Frame Width", Range(0, 0.05)) = 0.0055
        _BorderColor ("Border Color", Color) = (1, 1, 1, 1)

        // Standard UI plumbing so this behaves inside Masks / RectMask2D / raycasts.
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
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
            };

            sampler2D _LeftTex;
            sampler2D _CenterTex;
            sampler2D _RightTex;

            float _DividerLeftX;
            float _DividerRightX;
            float _SlantAmount;
            float _Aspect;
            float _BorderWidth;
            float _FrameBorderWidth;
            fixed4 _BorderColor;

            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                OUT.mask = float4(v.vertex.xy * 2 - 1, 0, 0);
                return OUT;
            }

            float4 _LeftTex_TexelSize;

            // Remaps screen-space uv to a "cover" sample coordinate so a fixed-aspect
            // source texture (e.g. a 16:9 camera RT) fills the screen without stretching,
            // cropping the excess instead — same idea as CSS background-size: cover.
            float2 CoverUV(float2 uv, float screenAspect, float texAspect)
            {
                float2 scale = screenAspect < texAspect
                    ? float2(screenAspect / texAspect, 1.0)
                    : float2(1.0, texAspect / screenAspect);
                return (uv - 0.5) * scale + 0.5;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Diagonal, mirrored/alternating divider lines: left divider leans one way,
                // right divider leans the other, giving the center panel its hourglass "manga panel" shape.
                float centeredY = uv.y - 0.5;
                float shift = _SlantAmount * centeredY * _Aspect;
                float dividerL = _DividerLeftX + shift;
                float dividerR = _DividerRightX - shift;

                // All three camera RTs share the same resolution/aspect, so one texel-size
                // lookup (from _LeftTex) is enough to correct all three samples.
                float texAspect = _LeftTex_TexelSize.z / max(_LeftTex_TexelSize.w, 1e-5);
                float2 sampleUV = CoverUV(uv, _Aspect, texAspect);

                fixed4 col;
                if (uv.x < dividerL)
                {
                    col = tex2D(_LeftTex, sampleUV);
                }
                else if (uv.x < dividerR)
                {
                    col = tex2D(_CenterTex, sampleUV);
                }
                else
                {
                    col = tex2D(_RightTex, sampleUV);
                }

                // Thin manga-style borders: internal dividers + outer frame.
                float distL = abs(uv.x - dividerL);
                float distR = abs(uv.x - dividerR);
                float onDivider = step(distL, _BorderWidth) + step(distR, _BorderWidth);

                float onFrame =
                    step(uv.x, _FrameBorderWidth) +
                    step(1.0 - _FrameBorderWidth, uv.x) +
                    step(uv.y, _FrameBorderWidth) +
                    step(1.0 - _FrameBorderWidth, uv.y);

                col = lerp(col, _BorderColor, saturate(onDivider + onFrame));
                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDHLSL
        }
    }
}
