Shader "LOTBA/UI/Radial Speed Lines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Line Color", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 0
        _SteppedFPS ("Animation FPS", Float) = 12
        _LineSpeedX ("Radial Speed (negative = outward)", Float) = -3
        _LineSpeedY ("Angular Speed", Float) = 0
        _LineScaleX ("Radial Scale", Float) = 0.35
        _LineScaleY ("Angular Frequency", Float) = 220
        _CenterFadeEdge ("Clear Center Radius", Range(0,1)) = 0.16
        _CenterFade ("Center Fade Width", Range(0.001,1)) = 0.18
        _LineDensity ("Noise Threshold (higher = fewer lines)", Range(0,1)) = 0.72
        _LineFalloff ("Line Edge Softness", Range(0.001,1)) = 0.08
        _Center ("Radial Center UV", Vector) = (0.5,0.5,0,0)
        _AspectRatio ("Width / Height", Float) = 1
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
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
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            half4 _Color;
            half _Opacity, _CenterFadeEdge, _CenterFade, _LineDensity, _LineFalloff;
            float _SteppedFPS, _LineSpeedX, _LineSpeedY, _LineScaleX, _LineScaleY, _AspectRatio;
            float4 _Center, _ClipRect;
            struct Attributes
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex.xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            // Lightweight periodic 2D value noise. Float coordinates prevent angular precision loss.
            half Hash(float2 p, float period)
            {
                p.y -= floor(p.y / period) * period;
                float3 h = frac(float3(p.xyx) * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return (half)frac((h.x + h.y) * h.z);
            }
            half ValueNoise(float2 p, float period)
            {
                float2 cell = floor(p);
                half2 f = (half2)frac(p);
                f = f * f * (3.0h - 2.0h * f);
                return lerp(lerp(Hash(cell,period),Hash(cell+float2(1,0),period),f.x),
                    lerp(Hash(cell+float2(0,1),period),Hash(cell+float2(1,1),period),f.x),f.y);
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 radial = input.uv - _Center.xy;
                radial.x *= max(_AspectRatio, 0.001);
                float dist = length(radial);
                float angle = atan2(radial.y,radial.x) * 0.159154943 + 0.5;
                float fps = max(_SteppedFPS,1.0);
                float time = floor(_Time.y * fps) / fps;
                float period = max(floor(_LineScaleY + 0.5),1.0);
                float angularPosition = angle * period + time * _LineSpeedY;
                // Each angular cell has a stable spine. Noise varies along its length,
                // rather than moving the two edges independently and making blunt ends.
                half noise = ValueNoise(float2(dist * max(_LineScaleX,0.001) + time * _LineSpeedX,
                    floor(angularPosition) + 0.5), period);
                half centerMask = saturate((half)(dist - _CenterFadeEdge) / max(_CenterFade,0.001h));
                // Width starts at zero at the current clear-center radius, not at UV origin.
                // Changing that radius therefore moves the pointed tips along with it.
                half width = centerMask * lerp(0.15h,0.85h,noise);
                half across = (half)abs(frac(angularPosition) - 0.5) * 2.0h;
                half softness = max((half)fwidth(across),max(_LineFalloff * width,0.001h));
                half shape = saturate((width - across) / softness + 0.5h);
                half density = smoothstep(_LineDensity,_LineDensity + max(_LineFalloff,0.001h),noise);
                half lines = shape * density;
                half4 result = input.color;
                result.a *= lines * centerMask * _Opacity * tex2D(_MainTex,input.uv).a;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.localPosition,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001h);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
