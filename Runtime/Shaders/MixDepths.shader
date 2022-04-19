Shader "Hidden/MixDepths"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
            
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            //Texture2D _DownsampleTexture0;
            sampler2D _DownsampleTexture2;
            SamplerState my_point_clamp_sampler;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
               return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                //float2 mask = _DownsampleTexture0.Sample(my_point_clamp_sampler, i.uv).xy;
                float2 mask = tex2D(_DownsampleTexture2, i.uv).xy;

                float2 boxDepth = tex2D(_MainTex, i.uv).xy;

                mask.x -= mask.y > .1f ? boxDepth.y : 0;
                //return col;
                //return float4(0, 0, 0, 0);
                return float4(mask.x, 0, 0, 0);
            }
            ENDCG
        }
    }
}
