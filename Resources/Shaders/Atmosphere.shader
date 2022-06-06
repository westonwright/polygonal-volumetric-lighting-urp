Shader "Hidden/Atmosphere"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #define PI 3.1415926538
            #define E 2.71828182

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            //sampler2D _CameraDepthTexture;
            sampler2D _CameraDepthTextureDownsampled;

            const float4x4 _cameraInverseProjection;
            const float4x4 _cameraToWorld;

            const float3 _lightDirection;
            const float3 _fogColor;
            const float3 _ambientColor;
            const float3 _extinctionColor;

            const float _maxDepth;
            const float _fogDensity;
            const float _extinctionCoef;
            const float _meiSactteringCoef;
            const float _rayleighSactteringCoef;

            // Approximation of the Mie-scattering function (light scatter through large particles like aerosols)
            // Essentially light is scattered more in the forward direction than when viewed from the side.
            // We use the Henyey-Greenstein phase function to approximate this (as outlined in "GPU Pro 5", page 131)
            // 
            // Henyey-Greenstein function:
            // Let x = angle between light and camera vectors
            //     g = Mie scattering coefficient (ScatterFalloff)
            // f(x) = (1 - g^2) / (4 * PI * (1 + g^2 - 2g*cos(x))^[3/2])
            float MieScatterHG(float lightDotView, float scattering)
            {
                float scatteringSq = scattering * scattering;
                float result = 1.0f - scatteringSq;
                result /= (4.0f * PI * pow(1.0f + scatteringSq - 2.0f * scattering * lightDotView, 1.5f));
                return result;
            }

            // Cornette-Shanks function might be more accurate.
            // Takes account of small particles illuminated by unpolarized light
            // f(x) = (3 * (1 - g^2) * (1 + cos^2(x))) / (2 * (2 + g^2) * (1 + g^2 - 2 * g * cos^2(x))^[3/2])
            float MeiScatteringCS(float lightDotViewSq, float scattering)
            {
                float scatteringSq = scattering * scattering;

                float result = 3.0f * (1.0f - scatteringSq) * (1.0f + lightDotViewSq);
                result /= (2.0f * (2.0f + scatteringSq) * pow((1.0f + scatteringSq - 2.0f * scattering * lightDotViewSq), 1.5f));
                return result;
            }

            // Rayleigh scattering is responsible for scattering low wavelength particles.
            // Causes blue tint of the sky
            // f(x) = (3 * (1 + cos^2(x))) / (16 * PI)
            float RayleighScattering(float lightDotViewSq, float scattering)
            {
                float result = 3.0f * (1 + lightDotViewSq);
                result /= (16 * PI);
                return result * scattering;
            }

            float BeerLaw(float extinction, float stepSize)
            {
                return exp(-extinction * stepSize);

            }
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 rayDirection = mul(_cameraInverseProjection, float4((i.uv * 2.0f) - 1.0f, 0.0f, 1.0f)).xyz;
                rayDirection = mul(_cameraToWorld, float4(rayDirection, 0.0f)).xyz;
                rayDirection = normalize(rayDirection);

                float sceneDepth = min(LinearEyeDepth(tex2D(_CameraDepthTextureDownsampled, i.uv).x), _maxDepth);
                float transmittance = BeerLaw(_extinctionCoef, sceneDepth);
                float lightDot = dot(-_lightDirection, rayDirection);
                float lightDotSq = lightDot * lightDot;

                float scattering = RayleighScattering(lightDotSq, _rayleighSactteringCoef) + MieScatterHG(lightDot, _meiSactteringCoef);
                scattering *= _fogDensity;

                float shadowTerm = min(tex2D(_MainTex, i.uv).x, _maxDepth);
                shadowTerm /= _maxDepth;

                //float3 fogColor = lerp(_shadowColor.xyz, _fogColor.xyz, (shadowTerm * _shadowColor.w)); //+(_ambientColor.xyz * _ambientColor.w));
                float3 fogColor = lerp(_ambientColor, _fogColor, shadowTerm);

                float3 result = (scattering * transmittance * fogColor) + (_extinctionColor * (1 - transmittance));

                return float4(result, transmittance);
            }
            ENDCG
        }
    }
}
