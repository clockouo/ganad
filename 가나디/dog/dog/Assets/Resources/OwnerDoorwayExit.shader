Shader "AfterTheGlow/OwnerDoorwayExit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Owner", 2D) = "white" {}
        _DoorRight ("Right edge of the actual doorway", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" "DisableBatching"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float worldX:TEXCOORD1; };
            sampler2D _MainTex;
            float _DoorRight;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color;
                o.worldX = mul(unity_ObjectToWorld, v.vertex).x;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                clip(_DoorRight - i.worldX);
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
