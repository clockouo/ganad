Shader "AfterTheGlow/ExteriorStitch"
{
    Properties
    {
        _StreetTex ("Street", 2D) = "white" {}
        _ParkTex ("Park", 2D) = "white" {}
        _Layout ("Left, seam, width, street center Y", Vector) = (6.6,24.6,18,-2.376575)
        _Heights ("Street height, park center Y, park height, playable UV top", Vector) = (6,-2.4677,6,.83356)
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
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float2 world:TEXCOORD0; };
            sampler2D _StreetTex, _ParkTex;
            float4 _Layout, _Heights;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o;
            }
            fixed4 sampleMap(sampler2D source, float u, float v, float height)
            {
                // Reflect a narrow source edge into the join instead of stretching
                // a single pixel column. Source title rows are never sampled.
                u = 1-abs(1-abs(u));
                float above=max(0,v-_Heights.w)*height;
                float skyV=_Heights.w-min(above*.006,.012);
                fixed4 c=tex2D(source,float2(clamp(u,.0003,.9997),max(.0007,min(v,skyV))));
                // Continue the source's textured sky into the camera's upper margin.
                c.rgb=lerp(c.rgb,float3(.27,.37,.38),smoothstep(0,2.2,above)*.35);
                c.a=1; return c;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float streetU=(i.world.x-_Layout.x)/_Layout.z;
                float parkU=(i.world.x-_Layout.y)/_Layout.z;
                float streetV=(i.world.y-_Layout.w)/_Heights.x+.5;
                float parkV=(i.world.y-_Heights.y)/_Heights.z+.5;
                fixed4 a=sampleMap(_StreetTex,streetU,streetV,_Heights.x);
                fixed4 b=sampleMap(_ParkTex,parkU,parkV,_Heights.z);
                return lerp(a,b,smoothstep(_Layout.y-.65,_Layout.y+.65,i.world.x));
            }
            ENDCG
        }
    }
}
