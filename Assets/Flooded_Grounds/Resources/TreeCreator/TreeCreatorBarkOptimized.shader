Shader "Hidden/Nature/Tree Creator Bark Optimized" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
	_BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
	_TranslucencyMap ("Trans (RGB) Gloss(A)", 2D) = "white" {}
	
	// These are here only to provide default values
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
	[HideInInspector] _TreeInstanceColor ("TreeInstanceColor", Vector) = (1,1,1,1)
	[HideInInspector] _TreeInstanceScale ("TreeInstanceScale", Vector) = (1,1,1,1)
	[HideInInspector] _SquashAmount ("Squash", Float) = 1
}

// URP compatibility (original Built-in implementation follows below).
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FGVertex
            #pragma fragment FGFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #define FG_BARK 1
            
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FGVertex
            #pragma fragment FGFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define FG_BARK 1
            #define FG_SHADOW_PASS 1
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FGVertex
            #pragma fragment FGFragment
            #pragma multi_compile_instancing
            
            #define FG_BARK 1
            #define FG_DEPTH_PASS 1
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FGVertex
            #pragma fragment FGFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define FG_BARK 1
            #define FG_NORMALS_PASS 1
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }
    }

    SubShader { 
	Tags { "RenderType"="TreeBark" }
	LOD 200
	
CGPROGRAM
#pragma surface surf Lambert vertex:TreeVertBark addshadow nolightmap
#include "UnityBuiltin3xTreeLibrary.cginc"

sampler2D _MainTex;
sampler2D _BumpSpecMap;
sampler2D _TranslucencyMap;

struct Input {
	float2 uv_MainTex;
	fixed4 color : COLOR;
};

void surf (Input IN, inout SurfaceOutput o) {

	fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
	o.Albedo = c.rgb * IN.color.rgb * IN.color.a;
	
	//fixed4 trngls = tex2D (_TranslucencyMap, IN.uv_MainTex);
	//o.Gloss = trngls.a * _Color.r;
	o.Alpha = c.a;
	
	half4 norspc = tex2D (_BumpSpecMap, IN.uv_MainTex);
	//o.Specular = norspc.r;
	o.Normal = UnpackNormalDXT5nm(norspc);
	//o.Smoothness = 0;
	//o.Metallic = 0;
}
ENDCG
}

Dependency "BillboardShader" = "Hidden/Nature/Tree Creator Bark Rendertex"
}
