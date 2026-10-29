Shader "AfterTheGlow/GateForwardOpening"
{
    Properties
    {
        [PerRendererData] _MainTex ("Gate", 2D) = "white" {}
        _OpenAmount ("Open amount", Float) = 0
        _LeafWidth ("Source leaf width", Float) = 1
        _HingeX ("Outer hinge X", Float) = 0
        _HingeY ("Bottom hinge Y", Float) = 0
        _WidthFraction ("Open width fraction", Float) = .4
        _ForwardDepth ("Signed depth in local units", Float) = -.1
        _Perspective ("Signed free edge size change", Float) = -.18
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
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float _OpenAmount, _LeafWidth, _HingeX, _HingeY, _WidthFraction, _ForwardDepth, _Perspective;
            v2f vert(appdata v)
            {
                float edge = saturate((v.vertex.x - _HingeX) / max(.0001, _LeafWidth));
                float open = saturate(_OpenAmount);
                // Keep the outer hinge fixed. Negative depth and size change
                // move the free edge up and away into the garden.
                v.vertex.x = _HingeX + (v.vertex.x - _HingeX) * lerp(1, _WidthFraction, open);
                v.vertex.y = _HingeY + (v.vertex.y - _HingeY) * (1 + _Perspective * open * edge) - _ForwardDepth * open * edge;
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDCG
        }
    }
}
