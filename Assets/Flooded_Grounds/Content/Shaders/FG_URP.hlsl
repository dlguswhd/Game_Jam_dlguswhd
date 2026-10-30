// URP rendering for Flooded Grounds' original material properties.
// Shared by the forward, shadow, depth and depth-normal passes.
#ifndef FLOODED_GROUNDS_URP_INCLUDED
#define FLOODED_GROUNDS_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
#if defined(FG_TOPBLEND) || defined(FG_WATER)
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
#endif
#if defined(FG_TOPBLEND)
TEXTURE2D(_Spc); SAMPLER(sampler_Spc);
TEXTURE2D(_AO); SAMPLER(sampler_AO);
TEXTURE2D(_layer1Tex); SAMPLER(sampler_layer1Tex);
TEXTURE2D(_layer1Norm); SAMPLER(sampler_layer1Norm);
TEXTURE2D(_layer1Breakup); SAMPLER(sampler_layer1Breakup);
TEXTURE2D(_DetailBump); SAMPLER(sampler_DetailBump);
#elif defined(FG_WATER)
TEXTURE2D(_BumpMap2); SAMPLER(sampler_BumpMap2);
TEXTURE2D(_ParallaxMap); SAMPLER(sampler_ParallaxMap);
#elif defined(FG_TRIPLANAR)
TEXTURE2D(_BumpMap1); SAMPLER(sampler_BumpMap1);
#else
TEXTURE2D(_BumpSpecMap); SAMPLER(sampler_BumpSpecMap);
TEXTURE2D(_TranslucencyMap); SAMPLER(sampler_TranslucencyMap);
#endif

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
#if defined(FG_TOPBLEND)
float4 _BumpMap_ST;
half _Power, _Shift, _DetailInt, _DetailTiling;
half _layer1Tiling, _layer1Metal, _layer1BreakupAmnt;
#elif defined(FG_WATER)
float4 _BumpMap_ST, _BumpMap2_ST, _ParallaxMap_ST;
half4 _Color;
half _ScrollSpeed, _WaveFreq, _WaveHeight, _Parallax;
half _Smth, _Emis, _BumpLerp;
#elif defined(FG_TRIPLANAR)
half _TexScale, _BlendPlateau, _Cutoff;
#else
half4 _Color, _TranslucencyColor;
half _Cutoff;
#endif
CBUFFER_END

#if defined(FG_BARK) || defined(FG_LEAVES)
// Match the engine's UnityTerrain buffer layout for painted tree instances.
CBUFFER_START(UnityTerrain)
half4 _WavingTint;
float4 _WaveAndDistance, _CameraPosition;
float3 _CameraRight, _CameraUp;
half4 _TreeInstanceColor;
float4 _TreeInstanceScale;
float4x4 _TerrainEngineBendTree;
float4 _SquashPlaneNormal;
float _SquashAmount;
float3 _TreeBillboardCameraRight;
float4 _TreeBillboardCameraUp, _TreeBillboardCameraFront, _TreeBillboardCameraPos, _TreeBillboardDistances;
CBUFFER_END
float4 _Wind;
#endif

struct FGAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FGVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    float3 positionOS : TEXCOORD4;
    half3 normalOS : TEXCOORD5;
    half4 color : COLOR;
    half4 fogAndVertexLight : TEXCOORD6;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 7);
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 FGPosition(inout FGAttributes input)
{
    float3 p = input.positionOS.xyz;
#if defined(FG_WATER)
    p.y = sin(_Time.x * _WaveFreq + (p.x + p.z * 2.0) * 8.0) * _WaveHeight;
#elif defined(FG_BARK) || defined(FG_LEAVES)
    #if defined(FG_LEAVES)
        // Tree Creator collapses camera-facing leaf quads to their centers.
        // Expand them before bending, in every pass, including their shadows.
        float billboard = 1.0 - abs(input.tangentOS.w);
        float4x4 inverseTransposeModelView = transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V));
        p += mul(float4(input.normalOS.xy, 0, 0), inverseTransposeModelView).xyz * billboard;
        float3 billboardNormal = normalize(mul(float4(input.normalOS, 0), inverseTransposeModelView).xyz);
        float3 billboardTangent = normalize(mul(float4(input.tangentOS.xyz, 0), inverseTransposeModelView).xyz);
        input.normalOS = lerp(input.normalOS, billboardNormal, billboard);
        input.tangentOS = lerp(input.tangentOS, float4(billboardTangent, -1), billboard);
    #endif
    p *= _TreeInstanceScale.xyz;
    // Preserve Tree Creator's primary bend, branch phase and edge flutter.
    float objectPhase = dot(GetObjectToWorldMatrix()._m03_m13_m23, float3(1, 1, 1));
    float branchPhase = objectPhase + input.color.x;
    float vertexPhase = dot(p, float3(1, 1, 1) * (input.color.y + branchPhase));
    float4 waves = frac((_Time.y + float2(vertexPhase, branchPhase)).xxyy * float4(1.975, 0.793, 0.375, 0.193)) * 2 - 1;
    waves = abs(frac(waves + 0.5) * 2 - 1);
    waves = waves * waves * (3 - 2 * waves);
    float2 waveSum = waves.xz + waves.yw;
    float3 bend = input.color.y * 0.1 * input.normalOS;
    bend.y = input.lightmapUV.y * 0.3;
    p += (waveSum.xyx * bend + _Wind.xyz * waveSum.y * input.lightmapUV.y) * _Wind.w;
    p += input.lightmapUV.x * _Wind.xyz;
    float distanceToPlane = dot(_SquashPlaneNormal.xyz, p) + _SquashPlaneNormal.w;
    p -= _SquashPlaneNormal.xyz * distanceToPlane * (1.0 - _SquashAmount);
#endif
    return p;
}

float3 _LightDirection, _LightPosition;

FGVaryings FGVertex(FGAttributes input)
{
    FGVaryings output = (FGVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 p = FGPosition(input);
    VertexPositionInputs positions = GetVertexPositionInputs(p);
    VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionWS = positions.positionWS;
    output.positionCS = positions.positionCS;
#if defined(FG_SHADOW_PASS)
    float3 lightDirection = _LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        lightDirection = normalize(_LightPosition - positions.positionWS);
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positions.positionWS, normals.normalWS, lightDirection));
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
    #endif
#endif
    output.uv = input.uv;
    output.positionOS = input.positionOS.xyz;
    output.normalOS = input.normalOS;
    output.normalWS = normals.normalWS;
    output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.color = input.color;
#if defined(FG_BARK) || defined(FG_LEAVES)
    output.color.rgb = _TreeInstanceColor.rgb * _Color.rgb;
#endif
    output.fogAndVertexLight = half4(ComputeFogFactor(positions.positionCS.z), VertexLighting(positions.positionWS, normals.normalWS));
    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(normals.normalWS, output.vertexSH);
    return output;
}

half3 FGNormalToWorld(FGVaryings input, half3 normalTS)
{
    half3 n = normalize(input.normalWS);
    half3 tangent = normalize(input.tangentWS.xyz);
    half3 bitangent = cross(n, tangent) * input.tangentWS.w;
    return NormalizeNormalPerPixel(mul(normalTS, half3x3(tangent, bitangent, n)));
}

void FGSurface(FGVaryings input, out SurfaceData surface, out half3 normalWS)
{
    surface = (SurfaceData)0;
    surface.alpha = 1;
    surface.occlusion = 1;
    surface.normalTS = half3(0, 0, 1);
    float2 uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    normalWS = normalize(input.normalWS);

#if defined(FG_TOPBLEND)
    float2 bumpUV = input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
    half3 baseNormal = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV));
    half4 layer = SAMPLE_TEXTURE2D(_layer1Tex, sampler_layer1Tex, uv * _layer1Tiling);
    half3 layerNormal = UnpackNormal(SAMPLE_TEXTURE2D(_layer1Norm, sampler_layer1Norm, uv * _layer1Tiling));
    half breakup = SAMPLE_TEXTURE2D(_layer1Breakup, sampler_layer1Breakup, uv * _layer1Tiling).r;
    half3 modifiedNormal = baseNormal + half3(layerNormal.xy * 0.6, 0);
    half mask = (FGNormalToWorld(input, modifiedNormal).y * _Power + _Shift) * lerp(1, breakup, _layer1BreakupAmnt);
    half blend = saturate(mask * mask * mask);
    half4 metalSmooth = SAMPLE_TEXTURE2D(_Spc, sampler_Spc, uv);
    half3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_DetailBump, sampler_DetailBump, uv * _DetailTiling));
    surface.albedo = lerp(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb, layer.rgb, blend);
    surface.metallic = lerp(metalSmooth.r, _layer1Metal, blend);
    surface.smoothness = lerp(metalSmooth.a, layer.a, blend);
    surface.occlusion = SAMPLE_TEXTURE2D(_AO, sampler_AO, bumpUV).g;
    surface.normalTS = normalize(lerp(baseNormal, layerNormal, blend) + half3(detail.xy * _DetailInt, 0));
    normalWS = FGNormalToWorld(input, surface.normalTS);
#elif defined(FG_WATER)
    float2 scroll = _ScrollSpeed * _Time.x * float2(1, 0.5);
    float2 scroll2 = float2((1 - _ScrollSpeed) * _Time.x, (1 - _ScrollSpeed * _Time.x) * 0.5);
    float2 heightUV = input.uv * _ParallaxMap_ST.xy + _ParallaxMap_ST.zw + scroll * 0.2;
    half height = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, heightUV).r;
    half3 viewTS = mul(half3x3(normalize(input.tangentWS.xyz), cross(normalWS, normalize(input.tangentWS.xyz)) * input.tangentWS.w, normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS));
    float2 offset = (height - 0.5) * _Parallax * viewTS.xy / max(abs(viewTS.z) + 0.42, 0.1);
    float2 bumpUV = input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw + offset;
    half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV + scroll));
    half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV + scroll2));
    half3 n3 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap2, sampler_BumpMap2, input.uv * _BumpMap2_ST.xy + _BumpMap2_ST.zw));
    surface.normalTS = normalize(lerp(n1 + half3(n2.xy, 0), n3, _BumpLerp));
    surface.albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset + scroll).rgb * _Color.rgb;
    surface.smoothness = _Smth;
    surface.emission = surface.albedo * _Emis;
    normalWS = FGNormalToWorld(input, surface.normalTS);
#elif defined(FG_TRIPLANAR)
    half3 weights = max(abs(input.normalOS) - _BlendPlateau, 0);
    weights /= max(dot(weights, half3(1, 1, 1)), 0.0001);
    float3 p = input.positionOS * _TexScale;
    half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.yz) * weights.x
            + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.zx) * weights.y
            + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xy) * weights.z;
    half2 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap1, sampler_BumpMap1, p.yz)).xy;
    half2 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap1, sampler_BumpMap1, p.zx)).xy;
    half2 n3 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap1, sampler_BumpMap1, p.xy)).xy;
    half3 perturbation = half3(0, n1) * weights.x + half3(n2.y, 0, n2.x) * weights.y + half3(n3, 0) * weights.z;
    surface.albedo = c.rgb * input.color.rgb;
    surface.alpha = c.a;
    clip(surface.alpha - _Cutoff);
    normalWS = TransformObjectToWorldNormal(normalize(input.normalOS + perturbation));
#else
    half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    half4 packed = SAMPLE_TEXTURE2D(_BumpSpecMap, sampler_BumpSpecMap, uv);
    // Tree Creator's atlas packs normal X/Y in alpha/green, not RG.
    half2 xy = packed.ag * 2 - 1;
    surface.normalTS = half3(xy, sqrt(saturate(1 - dot(xy, xy))));
    surface.albedo = c.rgb * input.color.rgb * input.color.a;
    surface.alpha = c.a;
    normalWS = FGNormalToWorld(input, surface.normalTS);
    #if defined(FG_LEAVES)
        clip(surface.alpha - _Cutoff);
        surface.emission = _TranslucencyColor.rgb * 0.02;
    #endif
#endif
}

half4 FGFragment(FGVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surface;
    half3 normalWS;
    FGSurface(input, surface, normalWS);
#if defined(FG_DEPTH_PASS) || defined(FG_SHADOW_PASS)
    return 0;
#elif defined(FG_NORMALS_PASS)
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormal = PackNormalOctQuadEncode(normalWS);
        return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
    #else
        return half4(normalWS, 0);
    #endif
#else
    InputData data = (InputData)0;
    data.positionWS = input.positionWS;
    data.normalWS = normalWS;
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    data.fogCoord = input.fogAndVertexLight.x;
    data.vertexLighting = input.fogAndVertexLight.yzw;
    data.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
    data.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    half4 color = UniversalFragmentPBR(data, surface);
    color.rgb = MixFog(color.rgb, data.fogCoord);
    return half4(color.rgb, 1);
#endif
}
#endif
