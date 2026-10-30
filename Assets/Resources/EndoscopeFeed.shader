Shader "GameJam/EndoscopeFeed"
{
    Properties { [PerRendererData] _MainTex ("Camera", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off Cull Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float tick = floor(_Time.y * 24);
                float2 uv = i.uv;
                // Uneven horizontal sync, occasional narrow tearing bands, soft low-resolution image.
                float band = step(0.975, hash(float2(floor(uv.y * 28), tick)));
                uv.x += (hash(float2(tick, floor(uv.y * 180))) - 0.5) * (0.0015 + band * 0.012);
                float2 pixel = _MainTex_TexelSize.xy * 1.3;
                float3 c = tex2D(_MainTex, uv).rgb * 0.4;
                c += tex2D(_MainTex, uv + float2(pixel.x, 0)).rgb * 0.15;
                c += tex2D(_MainTex, uv - float2(pixel.x, 0)).rgb * 0.15;
                c += tex2D(_MainTex, uv + float2(0, pixel.y)).rgb * 0.15;
                c += tex2D(_MainTex, uv - float2(0, pixel.y)).rgb * 0.15;
                float grey = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(c, grey.xxx, 0.55);
                float noise = hash(floor(i.uv * float2(640, 360)) + tick * 13.7) - 0.5;
                c += noise * 0.07;
                c *= 0.92 + 0.05 * sin(i.uv.y * 2262) + 0.025 * sin(_Time.y * 49);
                float2 edge = (i.uv - 0.5) * 2;
                c *= saturate(1.2 - dot(edge, edge) * 0.38);
                return fixed4(saturate(c) * i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}
