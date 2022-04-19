Shader "Hidden/Composite"
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
            Name "StandardComposite"
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

            sampler2D _MainTex;
            sampler2D _CameraSource;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 volume = tex2D(_MainTex, i.uv);
                float4 cam = tex2D(_CameraSource, i.uv);

                float3 combine = cam.xyz * volume.w;
                combine += volume.xyz;

                return float4(combine, 1);
            }
            ENDCG
        }
        
        Pass
        {
            Name "DebugComposite"
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

            sampler2D _MainTex;
            sampler2D _CameraSource;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 volume = tex2D(_MainTex, i.uv);
                float4 cam = tex2D(_CameraSource, i.uv);

                float3 combine = (volume.xyz / 10.0f) + cam.xyz;

                return float4(combine, 1);
            }
            ENDCG
        }
    }
}
