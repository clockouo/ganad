Shader "AfterTheGlow/PettingFocus"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _BlurPixels ("Blur in source pixels", Range(0, 64)) = 0
        _Cutout ("Cutout fringe threshold", Range(0, 1)) = 0
        _SampleRect ("Texture sample bounds", Vector) = (0, 0, 1, 1)
    }
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
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 localPosition : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurPixels;
            float _Cutout;
            float4 _SampleRect;
            float4 _ClipRect;
            fixed4 SampleLayer(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, clamp(uv, _SampleRect.xy, _SampleRect.zw));
                if (color.a < _Cutout) color = 0;
                else if (_Cutout > 0) color.a = 1;
                return color;
            }
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; o.localPosition = v.vertex; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                #ifdef UNITY_UI_CLIP_RECT
                i.color.a *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif
                // Zero radius uses the original point-filtered pixel art exactly.
                if (_BlurPixels < 0.01) return SampleLayer(i.uv) * i.color;
                float weights[5] = { 1, 4, 6, 4, 1 };
                float2 stepUV = _MainTex_TexelSize.xy * _BlurPixels * 0.5;
                float4 sum = 0;
                [unroll] for (int y = 0; y < 5; y++)
                [unroll] for (int x = 0; x < 5; x++)
                {
                    fixed4 layerSample = SampleLayer(i.uv + float2(x - 2, y - 2) * stepUV);
                    layerSample.rgb *= layerSample.a;
                    sum += layerSample * weights[x] * weights[y];
                }
                sum *= (1.0 / 256.0);
                sum.rgb /= max(sum.a, .0001);
                return sum * i.color;
            }
            ENDCG
        }
    }
}
