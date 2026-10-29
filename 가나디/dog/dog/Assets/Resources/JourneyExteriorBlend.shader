Shader "AfterTheGlow/JourneyExteriorBlend"
{
    Properties
    {
        _StreetTex ("Street artwork", 2D) = "white" {}
        _ParkTex ("Park artwork", 2D) = "white" {}
        _Layout ("Street left, park left, map width, street center Y", Vector) = (6.6,24.6,18,-2.372)
        _Heights ("Street height, park center Y, park height, blend half width", Vector) = (6,-2.422,6,1.2)
        _Sky ("Street clean sky limit, park clean sky limit", Vector) = (.846, .999, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float2 world : TEXCOORD0; };
            sampler2D _StreetTex, _ParkTex;
            float4 _Layout, _Heights, _Sky;
            float4 _StreetTex_TexelSize, _ParkTex_TexelSize;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy; return o;
            }
            fixed4 sampleMap(sampler2D source, float2 uv, float skyLimit, float2 texel, fixed3 sky)
            {
                // Extend only the clean sky. The street's dark 108px header is
                // framing, and must not become a horizontal stripe above the park.
                // Mirror the narrow blend margins so tree silhouettes continue
                // naturally, rather than stretching the last column into stripes.
                uv.x = 1 - abs(1 - abs(uv.x));
                float2 bounded = clamp(uv, texel * .5, float2(1, skyLimit) - texel * .5);
                fixed4 color = tex2D(source, bounded);
                color.rgb = lerp(color.rgb, sky, smoothstep(skyLimit - .055, skyLimit + .012, uv.y));
                color.a = 1;
                return color;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 streetUV = float2((i.world.x - _Layout.x) / _Layout.z,
                    (i.world.y - _Layout.w) / _Heights.x + .5);
                float2 parkUV = float2((i.world.x - _Layout.y) / _Layout.z,
                    (i.world.y - _Heights.y) / _Heights.z + .5);
                // Both sides share the park's empty sky, including the part of
                // the camera view above the supplied 3:1 paintings.
                fixed3 sky = tex2D(_ParkTex, float2(.5, .985)).rgb;
                fixed4 street = sampleMap(_StreetTex, streetUV, _Sky.x, _StreetTex_TexelSize.xy, sky);
                fixed4 park = sampleMap(_ParkTex, parkUV, _Sky.y, _ParkTex_TexelSize.xy, sky);
                float blend = smoothstep(_Layout.y - _Heights.w, _Layout.y + _Heights.w, i.world.x);
                return lerp(street, park, blend);
            }
            ENDCG
        }
    }
}
