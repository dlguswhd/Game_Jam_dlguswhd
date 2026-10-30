Shader "GameJam/BulletMark"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.color = input.color; return o; }
            half4 frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
