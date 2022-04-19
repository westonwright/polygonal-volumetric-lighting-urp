Shader "Unlit/LocalLightVolumeMesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    CGINCLUDE
    #include "UnityCG.cginc"

    //const float4x4 _worldToShadow[5];
    //const float4x4 _shadowToWorld[5];
    const float4x4 _cameraMatrix;
    const float4x4 _lightToWorld;
    //const float4 _cascadeShadowSplitSphereRadiiNear;
    //const float4 _cascadeShadowSplitSphereRadiiFar;
    const uint _maxTesselation;
    const uint _baseTesselationWidth;
    const float2 _baseChunkScale;
    const float3 _centerPos;
    //const float3 _cameraWorldPos;
    //const float4 _texelSize;
    //const float3 _lightDirection;
    //const float _depthBias;
    //const int _cascadeCount;

    struct v2f
    {
        //w determines if should be rendered or not
        float4 vertex : SV_POSITION;
        float4 world : TEXCOORD0;
        float4 view : TEXCOORD1;
        float3 triID : TEXCOORD2; // triID in x, render in y
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    //properties
    sampler2D _MainTex;
    float4 _MainTex_ST;

    //sampler2D _MainLightShadowmapTexture;
    //sampler2D _MainLightShadowmapTextureCopy;
    sampler2D _LocalShadowTexture;
    sampler2D _CameraDepthTexture;

    Texture2D _DownsampleTexture1;
    SamplerState my_point_clamp_sampler;

    // could replace with just reading values directly from quad but might need to swap x and y
    static const float2 TranslationTable[4] = { 
        float2(0, 0), 
        float2(1, 0), 
        float2(0, 1), 
        float2(1, 1)
    };
    
    uint GetTesselationDepth(uint quad)
    {
        uint depthKey = quad & 0x0003FFFF;
        uint tesselationDepth = _maxTesselation;
        uint shift = tesselationDepth * 2u;
        while ((depthKey >> shift) == 0u)
        {
            tesselationDepth--;
            shift -= 2u;
            if (shift <= 0u)
            {
                break;
            }
        }
        return tesselationDepth;
    }

    float2 MultiplyVectors(float2 vector1, float2 vector2) {
        return float2(vector1.x * vector2.x, vector1.y * vector2.y);
    }

    float2 DivideVectors(float2 vector1, float2 vector2) {
        return float2(vector1.x / vector2.x, vector1.y / vector2.y);
    }
    /*
    uint GetTesselationDepth(uint2 quad) {
        uint depthKey = quad.x & 0x0FFFFFFF;
        // tesselation depth was initialized at 14 because we are reserving the 2 most significant bits
        uint tesselationDepth = _maxTesselation;
        // 28 because despite having 2 extra bits, 
        // furthur tesselation wouldn't be possible in previous calculation steps
        // might need to change shift to int so it can go below 0?
        uint shift = tesselationDepth * 2u;
        while ((depthKey >> shift) == 0u) {
            tesselationDepth--;
            shift -= 2u;
            if (shift <= 0u) {
                break;
            }
        }
        return tesselationDepth;
    }
    */
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Transparent" }
        //LOD 100
        
        Cull Off
        Blend One One
        ZWrite Off
        
        Pass
        {
            Name "ProjectionMeshPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            static const int QuadTable[8] = {
                //only need quad 0 - 3
                0, 1,
                1, 1,
                0, 1,
                1, 1
            };

            // could change to float2
            static const float3 QuadTriTable[96] = {
                //always start on point at center of parent quad or furthest if not available
                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 1, 0), float3(2, 0, 0), float3(0, 0, 0),
                float3(1, 1, 0), float3(0, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(2, 0, 0), float3(0, 0, 0),
                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 1, 0), float3(1, 0, 0), float3(0, 0, 0),
                float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),

                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 1, 0), float3(1, 2, 0), float3(1, 0, 0),
                float3(0, 1, 0), float3(1, 0, 0), float3(0, 0, 0), float3(0, 1, 0), float3(1, 2, 0), float3(1, 0, 0),
                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),
                float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),

                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 0, 0), float3(0, -1, 0), float3(0, 1, 0),
                float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0), float3(0, -1, 0), float3(0, 1, 0),
                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 0, 0), float3(0, 0, 0), float3(0, 1, 0),
                float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),

                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 0), float3(-1, 1, 0), float3(1, 1, 0),
                float3(0, 0, 0), float3(1, 1, 0), float3(1, 0, 0), float3(0, 0, 0), float3(-1, 1, 0), float3(1, 1, 0),
                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 0), float3(0, 1, 0), float3(1, 1, 0),
                float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),
            };

            //static const uint quadtest[4] = { 0xF0000000 | 0, 0xF0000000 | 2, 0xF0000000 | 3, 0xF0000000 | 4 };

            // buffers
            Buffer<uint> _Quads;

            //use vertex id to get corret vertex from lookup table

            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID)
            {
                v2f o;

                uint quad = _Quads[instanceID];

                //uint neighbors = quad.x >> 30u; // 30 because neighbors are only stored in 2 bits for quads
                uint neighbors = (quad >> 18u) & 0x00000003; // 30 because neighbors are only stored in 2 bits for quads

                uint triID = (vertexID / 3u);
                int render = QuadTable[(neighbors * 2) + triID];
                if (render == 0) {
                    o.world = float4(0, 0, 0, 0);
                    o.vertex = float4(0, 0, 0, 0);
                    o.view = float4(0, 0, 0, 0);
                    o.triID = float4(0, 0, 0, 0);
                    return o;
                }

                uint tesselationDepth = GetTesselationDepth(quad);

                //uint posInParent = tesselationDepth > 0 ? quad.x & 0x00000003 : 0u;
                //uint posInParent = quad.x & 0x00000003;
                uint posInParent = quad & 0x00000003;
                //uint posInParent = 0u;
                //neighbors = 3u;

                //float2 parentOffset = float2(quad.y % _baseTesselationWidth, quad.y / _baseTesselationWidth);
                uint topQuad = ((quad & 0xFFF00000) >> 20u);
                float2 parentOffset = float2(topQuad % _baseTesselationWidth, topQuad / _baseTesselationWidth);
                parentOffset -= floor((float(_baseTesselationWidth) / 2u));// +float2(.5, .5);
                //parentOffset *= _baseChunkScale;
                parentOffset = MultiplyVectors(parentOffset, _baseChunkScale);
                parentOffset += _centerPos;

                float2 quadScale = _baseChunkScale / pow(2, tesselationDepth);

                float2 curScale = quadScale;
                //uint translationVal = quad.x;
                uint translationVal = quad & 0x0003FFFF;
                float2 offset = parentOffset; //0 - 1
                float2 translation = float2(0, 0);
                for (uint i = 0; i < tesselationDepth; i++)
                {
                    translation = TranslationTable[translationVal & 0x00000003];
                    offset += curScale * translation;
                    curScale *= 2;
                    translationVal >>= 2u;
                }
                // calculate position
                // get vertex position
                float3 triPos = QuadTriTable[(posInParent * 24) + (neighbors * 6) + vertexID];

                float3 lCoord = float3(((triPos.xy * quadScale) + offset.xy), 0);
                float2 uv = DivideVectors((lCoord.xy - (_centerPos.xy - (_baseChunkScale * (_baseTesselationWidth / 2)))), (_baseChunkScale * _baseTesselationWidth)) + float2(.001f, .001f);
                lCoord.z = tex2Dlod(_LocalShadowTexture, float4(uv, 0, 0)).r;
                float3 wCoord = mul(_lightToWorld, float4(lCoord, 1));

                //clamp x and y sample of shadowmap texture to 0 - 1 but keep position for worldspace
                //o.world = float4(mul(_shadowToWorld[0], float4(texUV.xy, tex2Dlod(_MainLightShadowmapTextureCopy, float4(texUV.xy, 0, 0)).r, 1)).xyz, 1);
                o.world = float4(wCoord, 1);
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                float3 positionVS = mul(UNITY_MATRIX_V, o.world).xyz;
                o.view = float4(positionVS, 1);
                //o.vertex = mul(_cameraVP, (float4(o.world.xyz, 1)));
                //convert the vertex position from world space to clip space
                o.triID = float4(triID, render, vertexID, 0);
                //o.triID = float4(uv, vertexID, 0);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                //don't render
                if (i.world.w <= .1f) {
                    return float4(0, 0, 0, 0);
                }

                // green is distance to front faces
                // red is distance to back faces
                //replace main tex with shadowmap
                //add in shadowmap world pos
                float4 projected = mul(_cameraMatrix, float4(i.world.xyz, 1));
                float2 uv = 1 - ((projected.xy / projected.w) * 0.5f + 0.5f);

                float trueDist = -i.view.z;
                float dist = trueDist;
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, uv));
                float2 boxDepth = _DownsampleTexture1.Sample(my_point_clamp_sampler, uv);
                depth = min(depth, boxDepth.x);
                dist = clamp(dist, 0, depth);

                int face = dist < boxDepth.y - .05f ? 0 : 1;
                face *= facing > 0 ? 1 : -1;

                dist = dist < boxDepth.y - .05f ? 0 : dist;

                //return facing > 0 ? float4(float(i.triID.z) / 6.0f, 0, 0, 1) : float4(0, float(i.triID.z) / 6.0f, 0, 1);
                float4 color = float4(0, 0, 0, 0);
                color.x = dist * sign(facing);
                color.y = face;
                return color;
            }
            ENDCG
        }

        Pass
        {
            Name "EdgeMeshPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // could change to float2
            static const float3 EdgeTriTable[6] = {
                //might want to flip this to clockwise for use with rotation?
                float3(0, 0, 0), float3(1, 0, 0), float3(0, 1, 0),  float3(0, 1, 0), float3(1, 0, 0), float3(1, 1, 0),
            };
            
            static const float4x4 TransformationTable[4] = {
                // x + 90
                float4x4(
                    1, 0, 0, 0,
                    0, 0, -1, 0,
                    0, 1, 0, 0,
                    0, 0, 0, 1
                    ),

                // y - 90
                float4x4(
                    0, 0, -1, 0,
                    0, 1, 0, 0,
                    1, 0, 0, 0,
                    0, 0, 0, 1
                    ),

                // x - 90, up 1
                float4x4(
                    1, 0, 0, 0,
                    0, 0, 1, 0,
                    0, -1, 0, 1,
                    0, 0, 0, 1
                    ),

                // y + 90, up 1
                float4x4(
                    0, 0, 1, 0,
                    0, 1, 0, 0,
                    -1, 0, 0, 1,
                    0, 0, 0, 1
                    ),

            };
            
            //buffers
            StructuredBuffer<uint> _Edges;

            //properties
            const float _edgeHeight;

            //use vertex id to get corret vertex from lookup table

            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID)
            {
                v2f o;

                uint edge = _Edges[instanceID];

                uint triID = (vertexID / 3u);

                //float2 parentOffset = float2(edge.y % _baseTesselationWidth, edge.y / _baseTesselationWidth);
                uint topQuad = ((edge & 0xFFF00000) >> 20u);
                float2 parentOffset = float2(topQuad % _baseTesselationWidth, topQuad / _baseTesselationWidth);

                //parentOffset *= _baseChunkScale;
                parentOffset -= floor((float(_baseTesselationWidth) / 2u));// +float2(.5, .5);
                //parentOffset *= _baseChunkScale;
                parentOffset = MultiplyVectors(parentOffset, _baseChunkScale);
                parentOffset += _centerPos;

                // this is the ceiling piece
                //if (edge.x == 0u)
                if ((edge & 0x0003FFFF) == 0u)
                //if (edge == 0u)
                {
                    float3 texPos = float3((EdgeTriTable[vertexID].xy * (_baseChunkScale * _baseTesselationWidth)) + parentOffset, _centerPos.z - _edgeHeight);

                    /*
                    // first to light space
                    // then to world space
                    texPos = mul(_lightToWorld, float4(texPos, 1)).xyz;

                    float3 sCoord = mul(_worldToShadow[_cascadeCount - 1], float4(texPos, 1)).xyz;

                    //texPos = mul(_worldToShadow[0], float4(texPos, 1)).xyz;     
                    sCoord.z = _edgeHeight;
                    float3 heightCoord = mul(_shadowToWorld[_cascadeCount - 1], float4(sCoord.xy, _edgeHeight, 1)).xyz;
                    */

                    //o.world = float4(heightCoord, 1);
                    o.world = mul(_lightToWorld, float4(texPos, 1));
                    o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                    float3 positionVS = mul(UNITY_MATRIX_V, o.world).xyz;
                    o.view = float4(positionVS, 1);
                    o.triID = float4(triID, 0, vertexID, 0);
                    return o;
                }

                //uint connectedEdge = edge.x >> 30u;
                uint connectedEdge = edge >> 18u & 0x00000003;

                uint tesselationDepth = GetTesselationDepth(edge);

                float2 edgeScale = _baseChunkScale / pow(2, tesselationDepth);

                float2 curScale = edgeScale;
                //uint translationVal = edge.x;
                uint translationVal = edge & 0x0003FFFF;
                // initializing offset like this allows edges to all actually be on the outside edge of the shape
                //float2 offset = float2(connectedEdge == 3u ? curScale : 0, connectedEdge == 2u ? curScale : 0); //0 - 1
                float2 offset = parentOffset + float2(connectedEdge == 3u ? curScale.x : 0, connectedEdge == 2u ? curScale.y : 0); //0 - 1
                float2 translation = float2(0, 0);

                for (uint i = 0u; i < tesselationDepth; i++)
                {
                    translation = TranslationTable[translationVal & 0x00000003];
                    offset += curScale * translation;
                    curScale *= 2;
                    translationVal >>= 2u;
                }

                //calculate position
                //get vertex position
                float3 triPos = mul(TransformationTable[connectedEdge], float4(EdgeTriTable[vertexID], 1)).xyz;
                //triPos.z *= _edgeHeight;

                float3 lCoord = float3(((triPos.xy * edgeScale) + offset.xy), 0);
                float2 uv = DivideVectors((lCoord.xy - (_centerPos.xy - (_baseChunkScale * (_baseTesselationWidth / 2)))), (_baseChunkScale * _baseTesselationWidth)) + float2(.001f, .001f);
                lCoord.z = triPos.z > 0 ? _centerPos.z - _edgeHeight : tex2Dlod(_LocalShadowTexture, float4(uv, 0, 0)).r;
                float3 wCoord = mul(_lightToWorld, float4(lCoord, 1));

                o.world = float4(wCoord, 1);
                //o.world = float4(mul(_shadowToWorld[0], float4(texPos.xy, _edgeHeight, 1)).xyz, 1);
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                float3 positionVS = mul(UNITY_MATRIX_V, o.world).xyz;
                o.view = float4(positionVS, 1);
                //o.vertex = mul(UNITY_MATRIX_VP, (float4(o.world.xyz, 1)));
                //convert the vertex position from world space to clip space
                o.triID = float4(triID, 0, vertexID, 0);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                //don't render
                if (i.world.w <= .1f) {
                    //return float4(0, 0, 0, 0);
                }

                // green is distance to front faces
                // red is distance to back faces
                //replace main tex with shadowmap
                //add in shadowmap world pos
                float4 projected = mul(_cameraMatrix, float4(i.world.xyz, 1));
                float2 uv = 1 - ((projected.xy / projected.w) * 0.5f + 0.5f);

                float dist = -i.view.z;
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, uv));
                float2 boxDepth = _DownsampleTexture1.Sample(my_point_clamp_sampler, uv);
                depth = min(depth, boxDepth.x);
                dist = clamp(dist, 0, depth);

                int face = dist < boxDepth.y - .05f ? 0 : 1;
                face *= facing > 0 ? 1 : -1;

                dist = dist < boxDepth.y - .05f ? 0 : dist;

                //return facing > 0 ? float4(float(i.triID.z) / 6.0f, 0, 0, 1) : float4(0, float(i.triID.z) / 6.0f, 0, 1);
                float4 color = float4(0, 0, 0, 0);
                color.x = dist * sign(facing);
                color.y = face;
                return color;
            }
            ENDCG
        }
    }
}
