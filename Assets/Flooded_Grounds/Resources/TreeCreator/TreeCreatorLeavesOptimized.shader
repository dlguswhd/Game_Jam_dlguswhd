Shader "Hidden/Nature/Tree Creator Leaves Optimized" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1) // (187,219,106,255)
	_Cutoff ("Alpha cutoff", Range(0,1)) = 0.3
	_TranslucencyViewDependency ("View dependency", Range(0,1)) = 0.7
	_ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
	_ShadowOffsetScale ("Shadow Offset Scale", Float) = 1
	
	_MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
	_ShadowTex ("Shadow (RGB)", 2D) = "white" {}
	_BumpSpecMap ("Normalmap (GA) Spec (R) Shadow Offset (B)", 2D) = "bump" {}
	_TranslucencyMap ("Trans (B) Gloss(A)", 2D) = "white" {}

	// These are here only to provide default values
	[HideInInspector] _TreeInstanceColor ("TreeInstanceColor", Vector) = (1,1,1,1)
	[HideInInspector] _TreeInstanceScale ("TreeInstanceScale", Vector) = (1,1,1,1)
	[HideInInspector] _SquashAmount ("Squash", Float) = 1
}

// URP compatibility (original Built-in implementation follows below).
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" "UniversalMaterialType"="Lit" }
        Cull Off

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
            #define FG_LEAVES 1
            
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
            #define FG_LEAVES 1
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
            
            #define FG_LEAVES 1
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
            #define FG_LEAVES 1
            #define FG_NORMALS_PASS 1
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }
    }

    SubShader { 
	Tags {
		"IgnoreProjector"="True"
		"RenderType"="TreeLeaf"
	}
	LOD 200
	Cull Off
	
	
CGPROGRAM
#pragma surface surf Lambert alphatest:_Cutoff vertex:TreeVertLeaf nolightmap noforwardadd
//#pragma surface surf TreeLeaf alphatest:_Cutoff vertex:TreeVertLeaf nolightmap noforwardadd
#pragma target 3.0

#include "UnityBuiltin3xTreeLibrary.cginc"

sampler2D _MainTex;
sampler2D _BumpSpecMap;
sampler2D _TranslucencyMap;

struct Input {
	float2 uv_MainTex;
	fixed4 color : COLOR; // color.a = AO
	half3 viewDir;
//	half3 lightDir;
};

void surf (Input IN, inout SurfaceOutput o) {

	fixed4 c = tex2D(_MainTex, IN.uv_MainTex);	
	fixed4 trngls = tex2D(_TranslucencyMap, IN.uv_MainTex);	
	
	
	half3 vd = pow(normalize(IN.viewDir), 12);
	half vdlerp = lerp(1,0, vd.b);
	
	o.Albedo = c.rgb * IN.color.rgb * IN.color.a;
	//o.Albedo = vdlerp;
	
	
	//o.Translucency = trngls.b;
	//o.Gloss = trngls.a * _Color.r;
	o.Alpha = saturate(c.a * vdlerp);
	//o.Alpha = c.a;

	
	half4 norspc = tex2D (_BumpSpecMap, IN.uv_MainTex);
	//o.Specular = norspc.r;
	o.Normal = UnpackNormalDXT5nm(norspc);
	//o.Smoothness = 0;
	//o.Metallic = 0;
	o.Emission = _TranslucencyColor * 0.02;
}
ENDCG

	// Pass to render object as a shadow caster
	
	Pass {
		Name "ShadowCaster"
		Tags { "LightMode" = "ShadowCaster" }
		
		CGPROGRAM
		#pragma vertex vert_surf
		#pragma fragment frag_surf
		#pragma multi_compile_shadowcaster
		#include "HLSLSupport.cginc"
		#include "UnityCG.cginc"
		#include "Lighting.cginc"

		#define INTERNAL_DATA
		#define WorldReflectionVector(data,normal) data.worldRefl

		#include "UnityBuiltin3xTreeLibrary.cginc"

		sampler2D _ShadowTex;

		struct Input {
			float2 uv_MainTex;
		};

		struct v2f_surf {
			V2F_SHADOW_CASTER;
			float2 hip_pack0 : TEXCOORD1;
		};
		float4 _ShadowTex_ST;
		v2f_surf vert_surf (appdata_full v) {
			v2f_surf o;
			TreeVertLeaf (v);
			o.hip_pack0.xy = TRANSFORM_TEX(v.texcoord, _ShadowTex);
			TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
			return o;
		}
		fixed _Cutoff;
		float4 frag_surf (v2f_surf IN) : SV_Target {
			half alpha = tex2D(_ShadowTex, IN.hip_pack0.xy).r;
			clip (alpha - _Cutoff);
			SHADOW_CASTER_FRAGMENT(IN)
		}
		ENDCG
	}
	
	
	
}

Dependency "BillboardShader" = "Hidden/Nature/Tree Creator Leaves Rendertex"
}
