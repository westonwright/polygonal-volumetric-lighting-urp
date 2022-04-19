Shader "Hidden/DownsamplePoint"
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
            Name "DownsamplePoint"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            texture2D _MainTex;
            SamplerState my_point_clamp_sampler;

            fixed4 frag (v2f i) : SV_Target
            {
                float d1 = _MainTex.Sample(my_point_clamp_sampler, i.uv, int2(0, 0)).x;
                float d2 = _MainTex.Sample(my_point_clamp_sampler, i.uv, int2(1, 0)).x;
                float d3 = _MainTex.Sample(my_point_clamp_sampler, i.uv, int2(0, 1)).x;
                float d4 = _MainTex.Sample(my_point_clamp_sampler, i.uv, int2(1, 1)).x;

                return float4(max(max(d1, d2), max(d3, d4)), 0, 0, 0);
            }
            ENDCG
        }

        Pass
        {
            Name "Upsample"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            texture2D _MainTex;
            sampler2D _CameraDepthTexture;
            texture2D _CameraDepthTextureDownsampled;
            SamplerState my_linear_clamp_sampler;
            SamplerState my_point_clamp_sampler;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 color = float4(0, 0, 0, 0);

                int offset = 0;

                float d0 = tex2D(_CameraDepthTexture, i.uv).x;
                //float d0 = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).x);
    
                float d1 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(0, 0)).x;
                float d2 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(1, 0)).x;
                float d3 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(0, 1)).x;
                float d4 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(-1, 0)).x;
                float d5 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(0, -1)).x;
                //float d6 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(0, -1)).x;
                //float d7 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(-1, -1)).x;
                //float d8 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(1, -1)).x;
                //float d9 = _CameraDepthTextureDownsampled.Sample(my_linear_clamp_sampler, i.uv, int2(-1, 1)).x;
    
                
                d1 = abs(d0 - d1);
                d2 = abs(d0 - d2);
                d3 = abs(d0 - d3);
                d4 = abs(d0 - d4);
                d5 = abs(d0 - d5);
                //d6 = abs(d0 - d6);
                //d7 = abs(d0 - d7);
                //d8 = abs(d0 - d8);
                //d9 = abs(d0 - d9);
    
                //float dmin = min(min(d1, d2), min(d3, d4));
                float dmin = min(min(min(d1, d2), min(d3, d4)), d5);
                //float dmin = min(min(min(min(d1, d2), min(d3, d4)), min(min(d5, d6), min(d7, d8))), d9);
    
                if (dmin == d1)
                    offset = 0;
                else if (dmin == d2)
                    offset = 1;
                else if (dmin == d3)
                    offset = 2;
                else if (dmin == d4)
                    offset = 3;
                else if (dmin == d5)
                    offset = 4;
                /*
                else if (dmin == d6)
                    offset = 5;
                else if (dmin == d7)
                    offset = 6;
                else if (dmin == d8)
                    offset = 7;
                else if (dmin == d9)
                    offset = 8;
                */
                switch (offset)
                {
                    case 0:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(0, 0));
                        break;
                    case 1:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(1, 0));
                        break;
                    case 2:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(0, 1));
                        break;
                    case 3:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(-1, 0));
                        break;
                    case 4:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(0, -1));
                        break;
                    /*
                    case 5:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(0, -1));
                        break;
                    case 6:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(-1, -1));
                        break;
                    case 7:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(1, -1));
                        break;
                    case 8:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv, int2(-1, 1));
                        break;
                    */
                    default:
                        color = _MainTex.Sample(my_linear_clamp_sampler, i.uv).x;
                        break;
                }

                //return float4(d1, d2, d3, d4) / 200.0f;
                return color;
            }
            ENDCG
        }
    }
}
