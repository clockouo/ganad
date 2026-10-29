Shader "AfterTheGlow/FountainWaterMemory"
{
    Properties { [PerRendererData] _MainTex ("Memory", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float2 ellipse=(i.uv-.5)*2*float2(240.0/230.0,135.0/110.0);
                float edge=1-smoothstep(.95,1,length(ellipse));
                fixed4 color=tex2D(_MainTex,i.uv)*i.color;
                color.a*=edge;
                return color;
            }
            ENDCG
        }
    }
}
