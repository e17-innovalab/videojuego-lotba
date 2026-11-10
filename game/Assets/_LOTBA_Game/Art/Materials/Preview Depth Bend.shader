Shader "LOTBA/Preview Depth Bend"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard vertex:bend addshadow

        sampler2D _MainTex;
        fixed4 _Color;
        half _Metallic, _Glossiness;
        float4 _LotbaDepthBend;
        struct Input { float2 uv_MainTex; };

        void bend(inout appdata_full vertex)
        {
            float4 world = mul(unity_ObjectToWorld, vertex.vertex);
            float distance = max(0.0, world.z - _LotbaDepthBend.z - _LotbaDepthBend.x);
            world.x += _LotbaDepthBend.y * distance * distance;
            vertex.vertex = mul(unity_WorldToObject, world);
            float3 normal = UnityObjectToWorldNormal(vertex.normal);
            normal.z -= normal.x * (2.0 * _LotbaDepthBend.y * distance);
            vertex.normal = normalize(mul(normal, (float3x3)unity_ObjectToWorld));
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 albedo = tex2D(_MainTex, input.uv_MainTex) * _Color;
            output.Albedo = albedo.rgb;
            output.Alpha = albedo.a;
            output.Metallic = _Metallic;
            output.Smoothness = _Glossiness;
        }
        ENDCG
    }
}
