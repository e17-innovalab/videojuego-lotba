Shader "LOTBA/Gem Flash Tint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _TopTint ("Top Tint", Color) = (1,1,1,1)
        _BottomTint ("Bottom Tint", Color) = (1,1,1,1)
        _RadialGradient ("Radial Gradient", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            fixed4 _TopTint, _BottomTint;
            half _RadialGradient;

            fixed4 frag(v2f input) : SV_Target
            {
                half radial = 1.0h - saturate(length(input.texcoord - 0.5h) * 2.0h);
                half weight = lerp(saturate(input.texcoord.y), radial, _RadialGradient);
                fixed4 tint = lerp(_BottomTint, _TopTint, weight);
                return SampleSpriteTexture(input.texcoord) * input.color * tint;
            }
            ENDCG
        }
    }
}
