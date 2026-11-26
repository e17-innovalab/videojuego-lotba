Shader "Carpinchos/Mobile/Color Horneado"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Name "ColorHorneado"
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half3 color : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color.rgb;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 color = saturate(input.color);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    color = LinearToGammaSpace(color);
                #endif
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
