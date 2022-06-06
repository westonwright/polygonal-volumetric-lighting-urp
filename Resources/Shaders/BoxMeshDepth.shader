Shader "Hidden/BoxMeshDepth"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Cull Off
        Blend One One
        ZWrite Off

        Pass
        {


            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 positionVS: TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float3 positionWS = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1.0)).xyz;
                float3 positionVS = mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).xyz;
                o.positionVS = float4(positionVS, 1);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float fragmentEyeDepth = -i.positionVS.z;

                return facing < 0 ? float4(fragmentEyeDepth, 0, 0, 0) : float4(0, fragmentEyeDepth, 0, 0);
            }
            ENDCG
        }
    }
}
