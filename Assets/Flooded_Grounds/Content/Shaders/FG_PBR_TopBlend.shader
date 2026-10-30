Shader "Flooded_Grounds/PBR_TopBlend" {
    Properties {
        _MainTex ("Base Albedo (RGB)", 2D) = "white" {}
        _Spc("Base Metalness(R) Smoothness(A)", 2D) = "black" {}
        _BumpMap ("Base Normal", 2D) = "bump" {} 
        _AO("Base AO", 2D)= "white" {}
        _layer1Tex ("Layer1 Albedo (RGB) Smoothness (A)", 2D) = "white" {}
        _layer1Metal ("Layer1 Metalness", Range(0,1)) = 0
        _layer1Norm("Layer 1 Normal", 2D) = "bump" {}
        _layer1Breakup ("Layer1 Breakup (R)", 2D) = "white" {}
        _layer1BreakupAmnt ("Layer1 Breakup Amount", Range(0,1)) = 0.5
        _layer1Tiling("Layer1 Tiling", float) = 10
        _Power ("Layer1 Blend Amount", float ) = 1
        _Shift("Layer1 Blend Height", float) = 1           
        _DetailBump ("Detail Normal", 2D) = "bump" {}  
        _DetailInt ("DetailNormal Intensity", Range(0,1)) = 0.4
        _DetailTiling("DetailNormal Tiling", float) = 2  
    }

     CGINCLUDE
        #define _GLOSSYENV 1
        #define UNITY_SETUP_BRDF_INPUT MetallicSetup
    ENDCG
    
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
            #define FG_TOPBLEND 1
            
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
            #define FG_TOPBLEND 1
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
            
            #define FG_TOPBLEND 1
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
            #define FG_TOPBLEND 1
            #define FG_NORMALS_PASS 1
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }
    }

    SubShader {
        Tags { "RenderType"="Opaque" "Queue" = "Geometry" }
        LOD 500
 
        CGPROGRAM
        #include "UnityPBSLighting.cginc"
        #pragma surface surf Standard 
        #pragma target 3.0
       

        sampler2D _MainTex, _Spc, _BumpMap, _AO, _DetailBump, _layer1Tex, _layer1Norm, _layer1Breakup;
        half3 _layer1Color, _SelfIlum;
        half _Power, _DetailInt, _DetailTiling, _layer1Tiling, _layer1Metal, _layer1BreakupAmnt, _Shift, _VMask;



        struct Input {
            half2 uv_MainTex; 
            half2 uv_BumpMap;          
            half3 worldNormal;  INTERNAL_DATA         
        
        };


        void surf (Input IN, inout SurfaceOutputStandard o) {       


            half3 layer1direction = half3(0,1,0);


            //Texture Inputs
            half3 main = tex2D (_MainTex, IN.uv_MainTex);            
            half3 norm = UnpackNormal(tex2D (_BumpMap, IN.uv_BumpMap));
            half4 spec = tex2D(_Spc, IN.uv_MainTex);
            half3 ao = tex2D(_AO, IN.uv_BumpMap);
            half4 layer1 = tex2D (_layer1Tex, IN.uv_MainTex * _layer1Tiling);
            half3 layer1norm = UnpackNormal(tex2D(_layer1Norm, IN.uv_MainTex * _layer1Tiling));
            
            half layer1Breakup = tex2D (_layer1Breakup, IN.uv_MainTex * _layer1Tiling).r;
            
            half3 detnorm = UnpackNormal(tex2D (_DetailBump, IN.uv_MainTex * _DetailTiling));

            half3 modNormal = norm + half3(layer1norm.r * 0.6, layer1norm.g * 0.6, 0);


            //Prepare Blend Masks
            half blend = dot(WorldNormalVector(IN, modNormal), layer1direction);
            
   
                   
            half blend2 = (blend * _Power + _Shift) * lerp(1, layer1Breakup, _layer1BreakupAmnt);
            
            blend2 = saturate(pow(blend2, 3));

        
          
            //Combine Normals          
            half3 blendedNormal = lerp(norm, layer1norm, blend2);
            blendedNormal = blendedNormal + (detnorm * half3(_DetailInt,_DetailInt,0));
    
            //Combine Diffuse layers
            half3 blendedColor = lerp(main, layer1, blend2);

            o.Albedo = blendedColor;
            o.Occlusion = ao.rgb;
            o.Smoothness = lerp(spec.a, layer1.a, blend2);
            o.Metallic = lerp(spec.r, _layer1Metal, blend2);
            o.Normal = blendedNormal.rgb;
           

        }
        ENDCG
    } 
    FallBack "Bumped Specular"
}