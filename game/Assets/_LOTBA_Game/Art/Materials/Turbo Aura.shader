Shader "LOTBA/Turbo Aura"
{
    Properties
    {
        _MainTex ("Aura", 2D) = "white" {}
        [HDR] _CoreColor ("Core Tint", Color) = (0.52, 1.15, 1.45, 0.8)
        [HDR] _ShellColor ("Shell Tint", Color) = (0.72, 0.45, 1.35, 0.55)
        _Rotation ("Core / Shell Rotation (rad/s)", Vector) = (6, -4.5, 0, 0)
        _ShellScale ("Shell Radial Scale", Range(1, 1.12)) = 1.05
        _Wiggle ("Edge Displacement", Range(0, 0.15)) = 0.085
        _Phase ("Animation Time", Float) = 0
        _Intensity ("Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _CoreColor, _ShellColor;
            float4 _Rotation;
            float _ShellScale, _Wiggle;
            float _Phase;
            half _Intensity;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 polar : TEXCOORD1;
                half4 tint : COLOR;
            };

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 layer : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float layer = input.layer.x;
                float2 polar = input.uv - 0.5;
                float radius = length(polar);
                float2 radial = polar / max(radius, 0.0001);
                float angle = _Phase * lerp(_Rotation.x, _Rotation.y, layer);
                float sine, cosine;
                sincos(angle, sine, cosine);
                output.uv = mul(float2x2(cosine, -sine, sine, cosine), polar) + 0.5;
                output.polar = polar;
                output.tint = lerp(_CoreColor, _ShellColor, layer);

                float3 position = input.vertex.xyz;
                position.xy = float2(0, 1.25) + (position.xy - float2(0, 1.25)) * lerp(1, _ShellScale, layer);
                float mask = smoothstep(0.15, 0.5, radius);
                float wave = sin(radial.x * 8 + radial.y * 5 - angle * 1.3)
                           * cos(radial.y * 9 - radial.x * 4 + angle * 0.7);
                float nose = saturate(radius / (0.5 * 0.375));
                float slope = nose * (4 - 3 * nose) * 3.85;
                float3 normal = normalize(float3(radial.x * slope / 2.31, radial.y * slope / 1.652, 1));
                position += normal * (_Wiggle * mask * wave);
                output.position = UnityObjectToClipPos(float4(position, 1));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 aura = tex2D(_MainTex, input.uv);
                half envelope = 1.0h - smoothstep(0.38h, 0.5h, length(input.polar));
                return half4(aura.rgb * input.tint.rgb * (aura.a * input.tint.a * envelope * _Intensity), 0);
            }
            ENDCG
        }
    }
}
