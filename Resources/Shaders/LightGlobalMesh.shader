Shader "Hidden/LightGlobalMesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "../Shaders/Includes/LookupTables.cginc"       

    const float4x4 _lightToWorld;
    const uint _maxTesselation;
    const uint _baseTesselationWidth;
    const float2 _baseChunkScale;
    const float3 _centerPos;

    //properties
    sampler2D _MainTex;
    float4 _MainTex_ST;

    sampler2D _LocalShadowTexture;
    sampler2D _CameraDepthTextureDownsampled;
    
    uint GetTesselationDepth(uint quad) {
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

    float4 CalculateQuadWorldPos(uint quad, uint vertexID){

        uint neighbors = (quad >> 18u) & 0x00000003; 

        uint triID = (vertexID / 3u);
        int render = QuadTable[(neighbors * 2) + triID];
        render = clamp(render, 0, 1);

        uint tesselationDepth = GetTesselationDepth(quad);

        uint posInParent = quad & 0x00000003;

        uint topQuad = ((quad & 0xFFF00000) >> 20u);
        float2 parentOffset = float2(topQuad % _baseTesselationWidth, topQuad / _baseTesselationWidth);
        parentOffset -= floor((float(_baseTesselationWidth) / 2u));
        parentOffset = MultiplyVectors(parentOffset, _baseChunkScale);
        parentOffset += _centerPos;

        float2 quadScale = _baseChunkScale / pow(2, tesselationDepth);

        float2 curScale = quadScale;
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
        float4 wCoord = mul(_lightToWorld, float4(lCoord, 1));
        wCoord.w = render;
        return wCoord;
    }

    float4 CalculateCeilingWorldPos(uint vertexID, float edgeHeight){
        uint triID = (vertexID / 3u);

        uint topQuad = 0u;
        float2 parentOffset = float2(topQuad % _baseTesselationWidth, topQuad / _baseTesselationWidth);

        parentOffset -= floor((float(_baseTesselationWidth) / 2u));
        parentOffset = MultiplyVectors(parentOffset, _baseChunkScale);
        parentOffset += _centerPos;

        float3 texPos = float3((EdgeTriTable[vertexID].xy * (_baseChunkScale * _baseTesselationWidth)) + parentOffset, _centerPos.z - edgeHeight);
        return mul(_lightToWorld, float4(texPos, 1));
    }

    float4 CalculateEdgeWorldPos(uint edge, uint vertexID, float edgeHeight){
        uint triID = (vertexID / 3u);

        uint topQuad = ((edge & 0xFFF00000) >> 20u);
        float2 parentOffset = float2(topQuad % _baseTesselationWidth, topQuad / _baseTesselationWidth);

        parentOffset -= floor((float(_baseTesselationWidth) / 2u));// +float2(.5, .5);
        parentOffset = MultiplyVectors(parentOffset, _baseChunkScale);
        parentOffset += _centerPos;

        uint connectedEdge = edge >> 18u & 0x00000003;

        uint tesselationDepth = GetTesselationDepth(edge);

        float2 edgeScale = _baseChunkScale / pow(2, tesselationDepth);

        float2 curScale = edgeScale;
        uint translationVal = edge & 0x0003FFFF;
        // initializing offset like this allows edges to all actually be on the outside edge of the shape
        float2 offset = parentOffset + float2(connectedEdge == 3u ? curScale.x : 0, connectedEdge == 2u ? curScale.y : 0);
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

        float3 lCoord = float3(((triPos.xy * edgeScale) + offset.xy), 0);
        float2 uv = DivideVectors((lCoord.xy - (_centerPos.xy - (_baseChunkScale * (_baseTesselationWidth / 2)))), (_baseChunkScale * _baseTesselationWidth)) + float2(.001f, .001f);
        lCoord.z = triPos.z > 0 ? _centerPos.z - edgeHeight : tex2Dlod(_LocalShadowTexture, float4(uv, 0, 0)).r;
        return float4(mul(_lightToWorld, float4(lCoord, 1)).xyz, 1);
    }

    float4 CalculateFragColor(float4 worldPos, float2 uv, float distance, float facing){
        float dist = distance;
        float depth = LinearEyeDepth(tex2D(_CameraDepthTextureDownsampled, uv));
        dist = clamp(dist, 0, depth);

        float4 color = float4(0, 0, 0, 0);
        color.x = dist * sign(facing);
        // multiply by world pos w to disable rendering if needed
        return color * worldPos.w;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Transparent" }
        //LOD 100
        
        Pass
        {
            Cull Off
            Blend One One
            ZWrite Off
            Name "ProjectionMeshPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                //w determines if should be rendered or not
                float4 vertex : SV_POSITION;
                float4 screen : TEXCOORD0;
                float4 world : TEXCOORD1;
                float4 view : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // buffers
            Buffer<uint> _Quads;

            //use vertex id to get corret vertex from lookup table
            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID)
            {
                v2f o;
                // if wCoord.w is 0, then don't render this quad
                o.world = CalculateQuadWorldPos(_Quads[instanceID], vertexID);;
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                o.view = float4(mul(UNITY_MATRIX_V, o.world).xyz, 1);
                o.screen = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float4 color = CalculateFragColor(i.world, uv, -i.view.z, facing);
                return color;
            }

            ENDCG
        }
        
        Pass
        {
            Cull Back
            BlendOp Min
            Blend One One
            ZWrite Off
            Name "ProjectionMeshDebugPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                //w determines if should be rendered or not
                float4 vertex : SV_POSITION;
                float4 world : TEXCOORD1;
                float4 view : TEXCOORD2;
                float3 triID : TEXCOORD3; // triID in x, render in y
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // buffers
            Buffer<uint> _Quads;

            //use vertex id to get corret vertex from lookup table
            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID)
            {
                v2f o;
                // if wCoord.w is 0, then don't render this quad
                o.world = CalculateQuadWorldPos(_Quads[instanceID], vertexID);;
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                o.triID = float4((vertexID / 3u), o.world.w, vertexID, 0);
                o.view = float4(mul(UNITY_MATRIX_V, o.world).xyz, 1);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                //return facing > 0 ? float4(float(i.triID.z) / 6.0f, 0, 0, 1) : float4(0, float(i.triID.z) / 6.0f, 0, 1);
                return -i.view.z;
                //return float4(1, 1, 1, 1);
            }

            ENDCG
        }

        Pass
        {
            Cull Off
            Blend One One
            ZWrite Off
            Name "CeilingMeshPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                //w determines if should be rendered or not
                float4 vertex : SV_POSITION;
                float4 screen : TEXCOORD0;
                float4 world : TEXCOORD1;
                float4 view : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            //properties
            const float _edgeHeight;

            v2f vert(uint vertexID: SV_VertexID)
            {
                v2f o;
                o.world = CalculateCeilingWorldPos(vertexID, _edgeHeight);
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                o.view = float4(mul(UNITY_MATRIX_V, o.world).xyz, 1);
                o.screen = ComputeScreenPos(o.vertex);
                return o;
            };

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float4 color = CalculateFragColor(i.world, uv, -i.view.z, facing);
                return color;
            };
            ENDCG
        }
        
        Pass
        {
            Cull Off
            Blend One One
            ZWrite Off
            Name "CeilingMeshDebugPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                //w determines if should be rendered or not
                float4 vertex : SV_POSITION;
                float4 world : TEXCOORD0;
                float3 triID : TEXCOORD1; // triID in x, render in y
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            //properties
            const float _edgeHeight;

            v2f vert(uint vertexID: SV_VertexID)
            {
                v2f o;
                o.world = CalculateCeilingWorldPos(vertexID, _edgeHeight);
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                o.triID = float4((vertexID / 3u), 0, vertexID, 0);
                return o;
            };

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                return facing > 0 ? float4(float(i.triID.z) / 6.0f, 0, 0, 1) : float4(0, float(i.triID.z) / 6.0f, 0, 1);
            };
            ENDCG
        }

        Pass
        {
            Cull Off
            Blend One One
            ZWrite Off
            Name "EdgeMeshPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                //w determines if should be rendered or not
                float4 vertex : SV_POSITION;
                float4 screen : TEXCOORD0;
                float4 world : TEXCOORD1;
                float4 view : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //buffers
            StructuredBuffer<uint> _Edges;

            //properties
            const float _edgeHeight;

            //use vertex id to get corret vertex from lookup table
            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID)
            {
                v2f o;
                o.world = CalculateEdgeWorldPos(_Edges[instanceID], vertexID, _edgeHeight);
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                o.view = float4(mul(UNITY_MATRIX_V, o.world).xyz, 1);
                o.screen = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float4 color = CalculateFragColor(i.world, uv, -i.view.z, facing);
                return color;
            }
            ENDCG
        }
        
        Pass
        {
            Cull Off
            Blend One One
            ZWrite Off
            Name "EdgeMeshDebugPass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                //w determines if should be rendered or not
                float4 vertex : SV_POSITION;
                float4 world : TEXCOORD0;
                float3 triID : TEXCOORD1; // triID in x, render in y
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //buffers
            StructuredBuffer<uint> _Edges;

            //properties
            const float _edgeHeight;

            //use vertex id to get corret vertex from lookup table
            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID)
            {
                v2f o;
                o.world = CalculateEdgeWorldPos(_Edges[instanceID], vertexID, _edgeHeight);
                o.vertex = UnityObjectToClipPos(float4(o.world.xyz, 1));
                float3 positionVS = mul(UNITY_MATRIX_V, o.world).xyz;
                o.triID = float4((vertexID / 3u), 0, vertexID, 0);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                return facing > 0 ? float4(float(i.triID.z) / 6.0f, 0, 0, 1) : float4(0, float(i.triID.z) / 6.0f, 0, 1);
            }
            ENDCG
        }
    }
}
