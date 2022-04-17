Shader "Unlit/ShadowSample"
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

            #include "UnityCG.cginc"

            const float4x4 _worldToLight;
            const float4x4 _lightToWorld;
            const float4x4 _shadowToWorld0;
            const float4x4 _shadowToWorld1;
            const float4x4 _shadowToWorld2;
            const float4x4 _shadowToWorld3;
            const float4x4 _worldToShadow0;
            const float4x4 _worldToShadow1;
            const float4x4 _worldToShadow2;
            const float4x4 _worldToShadow3;

            const float4 _shadowSplitSphere0;
            const float4 _shadowSplitSphere1;
            const float4 _shadowSplitSphere2;
            const float4 _shadowSplitSphere3;
            const float4 _cascadeShadowSplitSphereRadii;
            const float4 _texelSizes;

            const float3 _lightDirection;
            const float3 _lightCameraChunk;

            const float _laplacianWorldSize;
            const float _chunkWorldSize;
            const float _depthBias;
            const float _maxCascadeDepth;

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

            sampler2D _MainTex;
            //sampler2D _MainLightShadowmapTexture;
            Texture2D _MainLightShadowmapTexture;
            SamplerState my_point_clamp_sampler;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // these would normally be passed as arrays from the start but there were issues with passing them in the render feature
                float4x4 _worldToShadow[4] = { _worldToShadow0, _worldToShadow1, _worldToShadow2, _worldToShadow3};
                float4x4 _shadowToWorld[4] = { _shadowToWorld0, _shadowToWorld1, _shadowToWorld2, _shadowToWorld3};

                // converts from uv space on the texture to world space in the light relative to the camera
                float3 lightPosInit = float3(((i.uv - float2(.5f, .5f)) * _laplacianWorldSize), 0) + float3(_lightCameraChunk.xy + (float2(_chunkWorldSize, _chunkWorldSize) / 2.0f), 0);
                // then to world space
                float3 worldPosInit = mul(_lightToWorld, float4(lightPosInit, 1));

                // calculates the xy position in the shadowmap from the world position
                // this is done for each shadow cascade regardless of if they exist or not
                // non-existant cascades will be filtered out later
                float3 shadowCoord0 = mul(_worldToShadow[0], float4(worldPosInit, 1)).xyz;
                //float shadowSample0 = tex2D(_MainLightShadowmapTexture, shadowCoord0.xy).r;
                float shadowSample0 = _MainLightShadowmapTexture.Sample(my_point_clamp_sampler, shadowCoord0.xy).r;
                float3 shadowWorldPos0 = mul(_shadowToWorld[0], float4(shadowCoord0.xy, shadowSample0, 1));
                // removes depth bias from the sample world position based on the cascade
                // this is done to make displacement consistent across cascades in later passes
                // and remove unwanted edge detection at the boundary of split spheres in the laplacian pass
                shadowWorldPos0 -= _lightDirection * (_texelSizes.x * _depthBias);

                float3 shadowCoord1 = mul(_worldToShadow[1], float4(worldPosInit, 1)).xyz;
                //float shadowSample1 = tex2D(_MainLightShadowmapTexture, shadowCoord1.xy).r;
                float shadowSample1 = _MainLightShadowmapTexture.Sample(my_point_clamp_sampler, shadowCoord1.xy).r;
                float3 shadowWorldPos1 = mul(_shadowToWorld[1], float4(shadowCoord1.xy, shadowSample1, 1));
                shadowWorldPos1 -= _lightDirection * (_texelSizes.y * _depthBias);

                float3 shadowCoord2 = mul(_worldToShadow[2], float4(worldPosInit, 1)).xyz;
                //float shadowSample2 = tex2D(_MainLightShadowmapTexture, shadowCoord2.xy).r;
                float shadowSample2 = _MainLightShadowmapTexture.Sample(my_point_clamp_sampler, shadowCoord2.xy).r;
                float3 shadowWorldPos2 = mul(_shadowToWorld[2], float4(shadowCoord2.xy, shadowSample2, 1));
                shadowWorldPos2 -= _lightDirection * (_texelSizes.z * _depthBias);

                float3 shadowCoord3 = mul(_worldToShadow[3], float4(worldPosInit, 1)).xyz;
                //float shadowSample3 = tex2D(_MainLightShadowmapTexture, shadowCoord3.xy).r;
                float shadowSample3 = _MainLightShadowmapTexture.Sample(my_point_clamp_sampler, shadowCoord3.xy).r;
                float3 shadowWorldPos3 = mul(_shadowToWorld[3], float4(shadowCoord3.xy, shadowSample3, 1));
                shadowWorldPos3 -= _lightDirection * (_texelSizes.w * _depthBias);


                // gets the position of each of the shadow split spheres in light space and removes the z component
                float3 lightSplitSphere0 = float3(mul(_worldToLight, float4(_shadowSplitSphere0.xyz, 1)).xy, 0);
                float3 lightSplitSphere1 = float3(mul(_worldToLight, float4(_shadowSplitSphere1.xyz, 1)).xy, 0);
                float3 lightSplitSphere2 = float3(mul(_worldToLight, float4(_shadowSplitSphere2.xyz, 1)).xy, 0);
                float3 lightSplitSphere3 = float3(mul(_worldToLight, float4(_shadowSplitSphere3.xyz, 1)).xy, 0);
                // gets the distance of the light-space texture coordinate to each light-space split sphere
                // used later to ensure we are only sampling from the correct cascade
                float4 distancesI = float4(
                    distance(lightPosInit, lightSplitSphere0.xyz),
                    distance(lightPosInit, lightSplitSphere1.xyz),
                    distance(lightPosInit, lightSplitSphere2.xyz),
                    distance(lightPosInit, lightSplitSphere3.xyz)
                    );
                // gets the distance of each cascade's shadowmap sample and its split sphere
                // used to determine if this sample is within the range of the correct split sphere
                float4 distancesF = float4(
                    distance(shadowWorldPos0, _shadowSplitSphere0.xyz),
                    distance(shadowWorldPos1, _shadowSplitSphere1.xyz),
                    distance(shadowWorldPos2, _shadowSplitSphere2.xyz),
                    distance(shadowWorldPos3, _shadowSplitSphere3.xyz)
                    );

                // weights are calculated to determine which shadow sample should be used as the final output
                // weights prioritize higher resolution cascades
                bool4 weights = bool4(false, false, false, false);

                weights.x = distancesF.x < _cascadeShadowSplitSphereRadii.x;

                weights.y = !weights.x &&
                    (distancesF.y < _cascadeShadowSplitSphereRadii.y);
                
                weights.z = !weights.x && !weights.y &&
                    (distancesF.z < _cascadeShadowSplitSphereRadii.z);

                weights.w = !weights.x && !weights.y && !weights.z &&
                    (distancesF.w < _cascadeShadowSplitSphereRadii.w);

                float4 lDepths = float4(
                    mul(_worldToLight, shadowWorldPos0).z,
                    mul(_worldToLight, shadowWorldPos1).z,
                    mul(_worldToLight, shadowWorldPos2).z,
                    mul(_worldToLight, shadowWorldPos3).z
                    );

                // because of how we calculated weights so far, there can be holes in the map
                // adds anything rendered within the shadow cascade that wasn't picked up by any cascades in the previous calculations
                // the shadow sample must be below 1 or its world position will be clamped later
                // do this for all cascades in order
                // 
                // Mask determines the areas left to check. Don't check areas arlready covered by other cascades
                // and check distanceI to ensure only the current cascade is being sampled
                bool xMask = weights.x || (!weights.y && !weights.z && !weights.w && (distancesI.x < _cascadeShadowSplitSphereRadii.x));
                bool yMask = weights.y || (!weights.x && !weights.z && !weights.w && (distancesI.y < _cascadeShadowSplitSphereRadii.y));
                bool zMask = weights.z || (!weights.x && !weights.y && !weights.w && (distancesI.z < _cascadeShadowSplitSphereRadii.z));
                bool wMask = weights.w || (!weights.x && !weights.y && !weights.z && (distancesI.w < _cascadeShadowSplitSphereRadii.w));

                //weights.x = xMask && (_cascadeShadowSplitSphereRadii.y > 0 ? (shadowSample0 < .99f) : true);
                weights.x = xMask && ((shadowSample0 > 0) ? (_cascadeShadowSplitSphereRadii.y > 0 ?
                    (((lDepths.x < lDepths.y) && (lDepths.x < lDepths.z) && (lDepths.x < lDepths.w)) ?
                        true : ((!yMask && !zMask && !wMask) ?
                            true : (shadowSample0 < .99f))) :
                    true) : false);

                weights.y = yMask && !weights.x && ((shadowSample1 > 0) ? (_cascadeShadowSplitSphereRadii.z > 0 ?
                    (((lDepths.y < lDepths.z) && (lDepths.y < lDepths.w)) ? 
                        true : ((!weights.x && !zMask && !wMask) ?
                            true : (shadowSample1 < .99f))) :
                    true) : false);

                weights.z = zMask && !weights.x && !weights.y && ((shadowSample2 > 0) ? (_cascadeShadowSplitSphereRadii.w > 0 ?
                    ((lDepths.z < lDepths.w) ?
                        true : ((!weights.x && !weights.y && !wMask) ?
                            true : (shadowSample2 < .99f))) :
                    true) : false);
                
                weights.w = wMask && !weights.x && !weights.y && !weights.z;

                // multiplies the shadow samples by weights
                // mainly used to determine what wasn't covered by any cascades
                float shadowSamples =
                    shadowSample0 * weights.x +
                    shadowSample1 * weights.y +
                    shadowSample2 * weights.z +
                    shadowSample3 * weights.w;

                // multiplies the world positions by weights to determine the correct world position to use
                float3 wCoord =
                    shadowWorldPos0 * weights.x +
                    shadowWorldPos1 * weights.y +
                    shadowWorldPos2 * weights.z +
                    shadowWorldPos3 * weights.w;

                // converts the world position to the light space position
                // used for laplacian map and also displacement of the mesh in later passes
                float3 lCoord = mul(_worldToLight, float4(wCoord, 1)).xyz;
                // whatever wasn't covered by any cascades is projected to the far plane of the light
                lCoord.z = shadowSamples == 0 ? _maxCascadeDepth : lCoord.z;

                return float4(
                    lCoord.z, 0, 0,
                    //shadowSamples, 0, 0,
                    //xMask, yMask, zMask || wMask,
                    //weights.x, weights.y || weights.z || weights.w, 0,
                    //shadowSamples, weights.w || weights.x, weights.z,
                    //0, shadowSamples, 0,
                    0);
            }
            ENDCG
        }
    }
}
