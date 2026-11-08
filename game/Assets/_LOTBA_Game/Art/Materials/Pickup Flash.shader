Shader "LOTBA/Pickup Flash"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct ParticleVertex
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Interpolators
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Interpolators vert(ParticleVertex input)
            {
                Interpolators output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(Interpolators input) : SV_Target
            {
                float2 p = abs(input.uv * 2.0 - 1.0);
                float halo = saturate(1.0 - length(p));
                float rays = saturate(1.0 - max(p.x, p.y) - 4.0 * min(p.x, p.y));
                return fixed4(input.color.rgb, input.color.a * saturate(halo * halo * 0.55 + rays));
            }
            ENDCG
        }
    }
}
