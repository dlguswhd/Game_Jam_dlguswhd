// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'


Shader "Flooded_Grounds/Triplanar_BumpSpec"
{
    Properties
    {
        _TexScale ("Tex Scale", Range (0.1, 10.0))= 1.0
        _BlendPlateau ("BlendPlateau",     Range (0.0, 1.0)) = 0.2       
        _MainTex ("Base 1 (RGB) Gloss(A)", 2D) = "white" {}
        _BumpMap1 ("NormalMap 1 (_Y_X)", 2D)  = "bump" {}   
        
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
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
            #define FG_TRIPLANAR 1
            
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
            #define FG_TRIPLANAR 1
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
            
            #define FG_TRIPLANAR 1
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
            #define FG_TRIPLANAR 1
            #define FG_NORMALS_PASS 1
            #include "Assets/Flooded_Grounds/Content/Shaders/FG_URP.hlsl"
            ENDHLSL
        }
    }

    Category
    {
        SubShader
        {
            ZWrite On            
           Tags {"Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque"}
            LOD 400
 
            CGPROGRAM
                #pragma target 3.0                               
                #pragma surface surf Lambert vertex:vertLocal alphatest:_Cutoff
                           
                sampler2D _MainTex, _BumpMap1;          
                half _TexScale, _BlendPlateau;
          
               
                struct Input
                {
                    float3 thisPos;        
                    float3 thisNormal;
                    half4 color : COLOR; 
                };                    
               
                // Vertex program is determined in pragma above
                void vertWorld (inout appdata_full v, out Input o)
                {
                    o.thisNormal     = mul(unity_ObjectToWorld, float4(v.normal, 0.0f)).xyz;
                    o.thisPos         = mul(unity_ObjectToWorld, v.vertex);
                }
 
                void vertLocal (inout appdata_full v, out Input o)
                {
                    o.thisNormal     = v.normal;
                    o.thisPos         = v.vertex;// * _TexScale;
                    o.color = v.color;
                }        
 
 
                void surf (Input IN, inout SurfaceOutput o)
                {                    
                    // Determine the blend weights for the 3 planar projections.    
                    half3 blend_weights = abs( IN.thisNormal.xyz );           // Tighten up the blending zone:
                   
                    blend_weights = (blend_weights - _BlendPlateau);           // (blend_weights - 0.2) * 7; * 7 has no effect.
                    blend_weights = max(blend_weights, 0);                  // Force weights to sum to 1.0 (very important!)  
                    blend_weights /= (blend_weights.x + blend_weights.y + blend_weights.z ).xxx;  
                           
                    // Now determine a color value and bump vector for each of the 3  
                    // projections, blend them, and store blended results in these two vectors:  
                    half4 blended_color;     // .w hold spec value  Not true in this shader
                    half3 blended_bumpvec;
                             
                    // Compute the UV coords for each of the 3 planar projections.
                    // tex_scale (default ~ 1.0) determines how big the textures appear.  
                    half2 coord1 = IN.thisPos.yz * _TexScale;  
                    half2 coord2 = IN.thisPos.zx * _TexScale;  
                    half2 coord3 = IN.thisPos.xy * _TexScale;  
 
                    // Sample color maps for each projection, at those UV coords.  
                    half4 col1         = tex2D(_MainTex, coord1);
                    half4 col2         = tex2D(_MainTex, coord2);
                    half4 col3         = tex2D(_MainTex, coord3);
 
                    // Sample bump maps too, and generate bump vectors. (Note: this uses an oversimplified tangent basis.)  
                    // Using Unity packed normals (_Y_X), but we don't unpack since we don't need z.
                    // TO use Standard niormal maps change wy to xy               
                    half2 bumpVec1    = tex2D(_BumpMap1, coord1).wy * 2 - 1;  
                    half2 bumpVec2    = tex2D(_BumpMap1, coord2).wy * 2 - 1;  
                    half2 bumpVec3    = tex2D(_BumpMap1, coord3).wy * 2 - 1; 
 
                    half3 bump1     = half3(0, bumpVec1.x, bumpVec1.y);  
                    half3 bump2     = half3(bumpVec2.y, 0, bumpVec2.x);  
                    half3 bump3     = half3(bumpVec3.x, bumpVec3.y, 0);
 
                    // Finally, blend the results of the 3 planar projections.  
                    blended_color     = col1.xyzw * blend_weights.xxxx +  
                                        col2.xyzw * blend_weights.yyyy +  
                                        col3.xyzw * blend_weights.zzzz;  
                     
                    blended_bumpvec =   bump1.xyz * blend_weights.xxx +  
                                        bump2.xyz * blend_weights.yyy +  
                                        bump3.xyz * blend_weights.zzz;  
                   
                    half4 c = blended_color.rgba;
                    
                    o.Albedo = c.rgb * IN.color.rgb;
                   
                    o.Alpha = c.a;  
                   // o.Emission = c * 0.05; 
                   // o.Smoothness = c.r;
                   //o.Metallic = 0;                             
                    o.Normal = normalize( half3(0,0,1) + blended_bumpvec.xyz);        
                }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
 