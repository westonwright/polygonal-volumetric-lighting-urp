using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System;
//used as refrence: https://github.com/NGrigorov/Demo2DGamePCMultiplayer/blob/master/Library/PackageCache/com.unity.render-pipelines.lightweight%406.9.2/Runtime/Passes/MainLightShadowCasterPass.cs
class MeshLightGlobalPass : ScriptableRenderPass
{
    // used to label this pass in Unity's Frame Debug utility
    string _profilerTag;

    Material _shadowSampleMaterial;
    Material _lightGlobalMeshMaterial;
    Material _atmosphereMaterial;
    Material _smartSampleMaterial;
    Material _compositeMaterial;
    ComputeShader _laplacianCompute;
    ComputeShader _downsampleBufferCompute;
    ComputeShader _lightVolumeCompute;
    ComputeShader _patchTextureCompute;
    ComputeShader _blurCompute;
    RenderTargetIdentifier _cameraColorTarget;
    RenderTargetIdentifier _cameraDepthTarget;

    const int _maxCascades = 4;
    int _shadowCasterCascadesCount;
    int _shadowmapWidth;
    int _shadowmapHeight;

    Matrix4x4[] _mainLightShadowMatrices;
    Matrix4x4[] _mainLightShadowProjectionMatrices;
    Matrix4x4[] _mainLightShadowMatricesInverse;
    ShadowSliceData[] _cascadeSlices;
    Vector4[] _cascadeSplitDistances;

    int _cameraTextureHeight, _cameraTextureWidth;
    int _downsampleTextureHeight, _downsampleTextureWidth;

    float _laplacianGaussianStandardDeviation;
    int _laplacianKernelRadius;
    int _maxLaplacianWidth;
    int _chunkSize;
    int _chunksInRadius;
    int _maxTesselation;
    float _edgeHeight;
    float _distanceFactor;
    int _downsampleAmount;
    float _patchThreshold;
    float _blurGaussianStandardDeviation;
    int _blurKernelRadius;
    float _blurDepthFalloff;
    float _upsampleDepthThreshold;
    float _extinctionCoef;
    float _mieScatteringCoef;
    float _rayleighSactteringCoef;
    float _maxDepth;
    float _fogDensity;
    Color _fogColor;
    Color _ambientColor;
    Color _extinctionColor;

    bool _initializeBuffers;

    int _temp0Id;
    RenderTargetIdentifier _temp0;

    int _temp1Id;
    RenderTargetIdentifier _temp1;

    int _downsampleTemp0Id;
    RenderTargetIdentifier _downsampleTemp0;

    int _downsampleTemp1Id;
    RenderTargetIdentifier _downsampleTemp1;

    int _depthDownsampleId;
    RenderTargetIdentifier _depthDownsample;

    int _localShadowTargetId;
    RenderTargetIdentifier _localShadowTarget;

    //int[] _outTargetID;
    //RenderTargetIdentifier[] _outTarget;

    // blur kernel can be stored as one dimensional
    // should make laplacian kernel a compute buffer as well
    // even though it can't be stored in 1 dimension
    public ComputeBuffer _laplacianKernel;
    public ComputeBuffer _blurKernel;

    public ComputeBuffer _tesselationMap;
    public ComputeBuffer _laplacianMaps;
    int _laplacianLevels;

    public ComputeBuffer _readQuadsBuffer;
    public ComputeBuffer _writeQuadsBuffer;
    public ComputeBuffer _countsBuffer;

    public ComputeBuffer _emitQuadsBuffer;
    public ComputeBuffer _emitEdgesBuffer;
    public ComputeBuffer _writeArgsBuffer; // 3 ints: { xyz dispatch }
    public ComputeBuffer _quadArgsBuffer; // 4 ints: { vertex count per instance, instance count, start vertex location, start instance location }
    public ComputeBuffer _edgeArgsBuffer; // 4 ints: { vertex count per instance, instance count, start vertex location, start instance location }

    public ComputeBuffer _tesselationDepthData;
    tesselationDepthData[] _depthDataArray;

    Vector3 _lightCameraPos;
    Vector3 _lightCameraChunk;
    int _baseTesselationMapWidth;
    int _baseTesselationMapSize;
    float _minCascadeDepth;
    float _maxCascadeDepth;
    Vector4 _texelSizes;
    int _maxQuadSize;
    int _totalTesselationSize;
    int _maxEdgeSize;
    int _totalLaplacianSize;

    MaterialPropertyBlock _meshBlock;

    struct tesselationDepthData
    {
        public uint _tesselationMapWidth;
        public uint _mapIndex;
        public uint _nextMapIndex;
        public uint _laplacianTextureWidth;
        public uint _laplacianStartIndex;
        public Matrix4x4 _coordsToLight;
    };

    public MeshLightGlobalPass(
        Material shadowSampleMaterial,
        Material lightGlobalMaterial,
        Material atmosphereMaterial,
        Material smartSampleMaterial,
        Material compositeMaterial,
        ComputeShader laplacianCompute,
        ComputeShader downsampleBufferCompute,
        ComputeShader lightVolumeCompute,
        ComputeShader patchTextureCompute,
        ComputeShader blurCompute,
        string profilerTag
        )
    {
        _shadowSampleMaterial = shadowSampleMaterial;
        _lightGlobalMeshMaterial = lightGlobalMaterial;
        _atmosphereMaterial = atmosphereMaterial;
        _smartSampleMaterial = smartSampleMaterial;
        _compositeMaterial = compositeMaterial;
        _laplacianCompute = laplacianCompute;
        _downsampleBufferCompute = downsampleBufferCompute;
        _lightVolumeCompute = lightVolumeCompute;
        _patchTextureCompute = patchTextureCompute;
        _blurCompute = blurCompute;
        _profilerTag = profilerTag;

        _mainLightShadowMatrices = new Matrix4x4[_maxCascades + 1];
        _mainLightShadowProjectionMatrices = new Matrix4x4[_maxCascades + 1];
        _mainLightShadowMatricesInverse = new Matrix4x4[_maxCascades + 1];
        _cascadeSlices = new ShadowSliceData[_maxCascades];
        _cascadeSplitDistances = new Vector4[_maxCascades];

        _temp0Id = Shader.PropertyToID("_TempTexture0");
        _temp1Id = Shader.PropertyToID("_TempTexture1");
        _downsampleTemp0Id = Shader.PropertyToID("_DownsampleTexture0");
        _downsampleTemp1Id = Shader.PropertyToID("_DownsampleTexture1");
        _depthDownsampleId = Shader.PropertyToID("_CameraDepthTextureDownsampled");
        _localShadowTargetId = Shader.PropertyToID("_LocalShadowTexture");

        _meshBlock = new MaterialPropertyBlock();
    }

    public void InitializeBuffers(ref RenderingData renderingData)
    {
        if (
        _tesselationMap == null ||
        _laplacianMaps == null ||
        _readQuadsBuffer == null ||
        _writeQuadsBuffer == null ||
        _countsBuffer == null ||
        _emitQuadsBuffer == null ||
        _writeArgsBuffer == null ||
        _quadArgsBuffer == null ||
        _emitEdgesBuffer == null ||
        _edgeArgsBuffer == null ||
        _tesselationDepthData == null ||
        _laplacianKernel == null ||
        _blurKernel == null
        )
        {
            _initializeBuffers = true;
            return;
        }
        else
        {
            var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            if (
                _cameraTextureWidth != cameraTargetDescriptor.width ||
                 _cameraTextureHeight != cameraTargetDescriptor.height ||
                _shadowmapWidth != renderingData.shadowData.mainLightShadowmapWidth ||
                _shadowmapHeight != ((_shadowCasterCascadesCount == 2) ?
                renderingData.shadowData.mainLightShadowmapHeight >> 1 :
                renderingData.shadowData.mainLightShadowmapHeight)
            )
            {
                DisposeBuffers();
                _initializeBuffers = true;
            }
            else
            {
                _initializeBuffers = false;
            }
        }
    }

    public bool Setup(ref RenderingData renderingData)
    {
        if (!renderingData.shadowData.supportsMainLightShadows)
            return false;

        ClearShadowData();
        int shadowLightIndex = renderingData.lightData.mainLightIndex;
        if (shadowLightIndex == -1)
            return false;

        VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
        Light light = shadowLight.light;
        if (light.shadows == LightShadows.None)
            return false;

        if (shadowLight.lightType != LightType.Directional)
        {
            Debug.LogWarning("Only directional lights are supported as main light.");
        }

        Bounds bounds;
        if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
            return false;

        _shadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;

        int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(renderingData.shadowData.mainLightShadowmapWidth,
            renderingData.shadowData.mainLightShadowmapHeight, _shadowCasterCascadesCount);
            _shadowmapWidth = renderingData.shadowData.mainLightShadowmapWidth;
            _shadowmapHeight = (_shadowCasterCascadesCount == 2) ?
            renderingData.shadowData.mainLightShadowmapHeight >> 1 :
            renderingData.shadowData.mainLightShadowmapHeight;
        for (int cascadeIndex = 0; cascadeIndex < _shadowCasterCascadesCount; ++cascadeIndex)
        {
            bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData,
                shadowLightIndex, cascadeIndex, _shadowmapWidth, _shadowmapHeight, shadowResolution, light.shadowNearPlane,
                out _cascadeSplitDistances[cascadeIndex], out _cascadeSlices[cascadeIndex], out _cascadeSlices[cascadeIndex].viewMatrix, out _cascadeSlices[cascadeIndex].projectionMatrix);

            if (!success)
                return false;
        }

        return true;
    }

    public void DisposeBuffers()
    {
        //Debug.Log("disposing");

        if (
            _tesselationMap != null ||
            _laplacianMaps != null ||
            _readQuadsBuffer != null ||
            _writeQuadsBuffer != null ||
            _countsBuffer != null ||
            _emitQuadsBuffer != null ||
            _writeArgsBuffer != null ||
            _quadArgsBuffer != null ||
            _emitEdgesBuffer != null ||
            _edgeArgsBuffer != null ||
            _tesselationDepthData != null ||
            _laplacianKernel != null ||
            _blurKernel != null
            )
        {
            _tesselationMap.Dispose();
            _laplacianMaps.Dispose();
            _readQuadsBuffer.Dispose();
            _writeQuadsBuffer.Dispose();
            _countsBuffer.Dispose();
            _emitQuadsBuffer.Dispose();
            _writeArgsBuffer.Dispose();
            _quadArgsBuffer.Dispose();
            _emitEdgesBuffer.Dispose();
            _edgeArgsBuffer.Dispose();
            _tesselationDepthData.Dispose();
            _laplacianKernel.Dispose();
            _blurKernel.Dispose();
        }
    }

    public void SetTarget(
        float laplacianGaussianStandardDeviation,
        int laplacianKernelRadius,
        int maxLaplacianWidth,
        int chunkSize,
        int chunksInRadius,
        int maxTesselation,
        float edgeHeight,
        float distanceFactor,
        int downsampleAmount,
        float patchThreshold,
        float blurGaussianStandardDeviation,
        int blurKernelRadius,
        float blurDepthFalloff,
        float upsampleDepthThreshold,
        float extinctionCoef,
        float mieScatteringCoef,
        float rayleighSactteringCoef,
        float maxDepth,
        float fogDensity,
        Color fogColor,
        Color ambientColor,
        Color extinctionColor
        )
    {
        _laplacianGaussianStandardDeviation = laplacianGaussianStandardDeviation;
        _laplacianKernelRadius = laplacianKernelRadius;
        _chunkSize = chunkSize;
        _chunksInRadius = chunksInRadius;
        _maxTesselation = maxTesselation;
        _edgeHeight = edgeHeight;
        _distanceFactor = distanceFactor;
        _maxLaplacianWidth = maxLaplacianWidth;
        _downsampleAmount = downsampleAmount;
        _patchThreshold = patchThreshold;
        _blurGaussianStandardDeviation = blurGaussianStandardDeviation;
        _blurKernelRadius = blurKernelRadius;
        _blurDepthFalloff = blurDepthFalloff;
        _upsampleDepthThreshold = upsampleDepthThreshold;
        _extinctionCoef = extinctionCoef;
        _mieScatteringCoef = mieScatteringCoef;
        _rayleighSactteringCoef = rayleighSactteringCoef;
        _maxDepth = maxDepth;
        _fogDensity = fogDensity;
        _fogColor = fogColor;
        _ambientColor = ambientColor;
        _extinctionColor = extinctionColor;

        _baseTesselationMapWidth = _chunksInRadius + _chunksInRadius - 1;
        _baseTesselationMapSize = _baseTesselationMapWidth * _baseTesselationMapWidth;

        _maxQuadSize = Mathf.RoundToInt(Mathf.Pow(Mathf.RoundToInt(_baseTesselationMapWidth * Mathf.Pow(2, _maxTesselation)), 2));
        _totalTesselationSize = Mathf.CeilToInt(((_maxQuadSize / 3) * 4) / 16f); // + 1;
        // why divide here?
        // find better way to reduce size than just dividing randomly
        //_maxQuadSize /= 4;

        _maxEdgeSize = Mathf.RoundToInt(_baseTesselationMapWidth * Mathf.Pow(2, _maxTesselation)) * 4;
        //_maxEdgeSize /= 4;

        _totalLaplacianSize = Mathf.FloorToInt(Mathf.Pow(_maxLaplacianWidth, 2) / 3f) * 4;
        _laplacianLevels = Mathf.RoundToInt(Mathf.Log(_maxLaplacianWidth, 2)) - 1; //goes from max width down to 2
    }

    // called each frame before Execute, use it to set up things the pass will need
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        RenderTextureDescriptor cameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        // might not need to change color format?
        cameraDescriptor.colorFormat = RenderTextureFormat.ARGBFloat;
        cameraDescriptor.enableRandomWrite = true;
        _cameraTextureWidth = cameraDescriptor.width;
        _cameraTextureHeight = cameraDescriptor.height;
        _downsampleTextureWidth = _cameraTextureWidth / _downsampleAmount;
        _downsampleTextureHeight = _cameraTextureHeight / _downsampleAmount;

        _cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;

        int cameraDepthId = Shader.PropertyToID("_CameraDepthTexture");
        _cameraDepthTarget = new RenderTargetIdentifier(cameraDepthId);

        //prevent antialiasing warnings?
        if (_cameraTextureWidth > 0 && _cameraTextureHeight > 0)
        {
            cmd.GetTemporaryRT(_temp0Id, cameraDescriptor);
            _temp0 = new RenderTargetIdentifier(_temp0Id);
            ConfigureTarget(_temp0);
            
            cmd.GetTemporaryRT(_temp1Id, cameraDescriptor);
            _temp1 = new RenderTargetIdentifier(_temp1Id);
            ConfigureTarget(_temp1);

            RenderTextureDescriptor downsampleDescriptor = new RenderTextureDescriptor(_downsampleTextureWidth, _downsampleTextureHeight, cameraDescriptor.colorFormat);
            downsampleDescriptor.enableRandomWrite = true;

            cmd.GetTemporaryRT(_downsampleTemp0Id, downsampleDescriptor);
            _downsampleTemp0 = new RenderTargetIdentifier(_downsampleTemp0Id);
            ConfigureTarget(_downsampleTemp0);

            cmd.GetTemporaryRT(_downsampleTemp1Id, downsampleDescriptor);
            _downsampleTemp1 = new RenderTargetIdentifier(_downsampleTemp1Id);
            ConfigureTarget(_downsampleTemp1);

            RenderTextureDescriptor downsampleDepthDescriptor = new RenderTextureDescriptor(_downsampleTextureWidth, _downsampleTextureHeight, RenderTextureFormat.RFloat);
            downsampleDescriptor.enableRandomWrite = true;
            downsampleDescriptor.msaaSamples = 1;

            cmd.GetTemporaryRT(_depthDownsampleId, downsampleDepthDescriptor);
            _depthDownsample = new RenderTargetIdentifier(_depthDownsampleId);
            ConfigureTarget(_depthDownsample);
        }
        else
        {
            RenderTextureDescriptor dummyDescriptor = new RenderTextureDescriptor(1, 1);
            dummyDescriptor.enableRandomWrite = true;

            cmd.GetTemporaryRT(_temp0Id, dummyDescriptor);
            _temp0 = new RenderTargetIdentifier(_temp0Id);
            ConfigureTarget(_temp0);
            
            cmd.GetTemporaryRT(_temp1Id, dummyDescriptor);
            _temp1 = new RenderTargetIdentifier(_temp1Id);
            ConfigureTarget(_temp1);

            cmd.GetTemporaryRT(_downsampleTemp0Id, dummyDescriptor);
            _downsampleTemp0 = new RenderTargetIdentifier(_downsampleTemp0Id);
            ConfigureTarget(_downsampleTemp0);

            cmd.GetTemporaryRT(_downsampleTemp1Id, dummyDescriptor);
            _downsampleTemp1 = new RenderTargetIdentifier(_downsampleTemp1Id);
            ConfigureTarget(_downsampleTemp1);

            cmd.GetTemporaryRT(_depthDownsampleId, dummyDescriptor);
            _depthDownsample = new RenderTargetIdentifier(_depthDownsampleId);
            ConfigureTarget(_depthDownsample);
        }

        // can use the green channel on this to tell laplacian texture to ignore parts if desired
        RenderTextureDescriptor localShadowTargetDescriptor = new RenderTextureDescriptor(_maxLaplacianWidth, _maxLaplacianWidth, RenderTextureFormat.RGFloat);
        localShadowTargetDescriptor.enableRandomWrite = true;
        cmd.GetTemporaryRT(_localShadowTargetId, localShadowTargetDescriptor, FilterMode.Point);
        _localShadowTarget = new RenderTargetIdentifier(_localShadowTargetId);
        ConfigureTarget(_localShadowTarget);

        /*
        _outTarget = new RenderTargetIdentifier[10];
        _outTargetID = new int[8];
        int size = _chunksInRadius + _chunksInRadius - 1;
        for(int i = 0; i < 8; i++)
        {
            _outTargetID[i] = Shader.PropertyToID("_Out" + i);
            RenderTextureDescriptor outDescriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.RGFloat);
            outDescriptor.enableRandomWrite = true;
            cmd.GetTemporaryRT(_outTargetID[i], outDescriptor);
            _outTarget[i] = new RenderTargetIdentifier(_outTargetID[i]);
            ConfigureTarget(_outTarget[i]);
            
            size *= 2;
        }
        */
        ConfigureClear(ClearFlag.All, Color.clear);
    }

    // Execute is called for every eligible camera every frame. It's not called at the moment that
    // rendering is actually taking place, so don't directly execute rendering commands here.
    // Instead use the methods on ScriptableRenderContext to set up instructions.
    // RenderingData provides a bunch of (not very well documented) information about the scene
    // and what's being rendered.
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        //shadow stuff
        LightData lightData = renderingData.lightData;
        ShadowData shadowData = renderingData.shadowData;

        int shadowLightIndex = lightData.mainLightIndex;
        // don't render if the shadow light doesn't exist
        if (shadowLightIndex == -1)
        {
            return;
        }

        VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];

        for (int i = 0; i < _shadowCasterCascadesCount; ++i)
        {
            _mainLightShadowProjectionMatrices[i] = _cascadeSlices[i].projectionMatrix;
            _mainLightShadowMatrices[i] = _cascadeSlices[i].shadowTransform;
            _mainLightShadowMatricesInverse[i] = _cascadeSlices[i].shadowTransform.inverse;
        }

        // We setup and additional a no-op WorldToShadow matrix in the last index
        // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
        // out of bounds. (position not inside any cascade) and we want to avoid branching
        Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
        noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
        for (int i = _shadowCasterCascadesCount; i <= _maxCascades; ++i)
        {
            _mainLightShadowMatrices[i] = noOpShadowMatrix;
            // figure out no-op for inverse
        }

        _lightCameraPos = shadowLight.localToWorldMatrix.inverse * (renderingData.cameraData.worldSpaceCameraPos - shadowLight.light.transform.position);
        _lightCameraChunk = new Vector3(Mathf.FloorToInt(_lightCameraPos.x / _chunkSize) * _chunkSize, Mathf.FloorToInt(_lightCameraPos.y / _chunkSize) * _chunkSize, _lightCameraPos.z);

        _minCascadeDepth = Mathf.Infinity;
        _maxCascadeDepth = -Mathf.Infinity;
        for (int i = 0; i < _shadowCasterCascadesCount; i++)
        {
            Vector3 cascadeLightPos = shadowLight.localToWorldMatrix.inverse * new Vector4(_cascadeSplitDistances[i].x, _cascadeSplitDistances[i].y, _cascadeSplitDistances[i].z, 1);
            float curMin = (cascadeLightPos.z - _cascadeSplitDistances[i].w);
            float curMax = (cascadeLightPos.z + _cascadeSplitDistances[i].w);
            _minCascadeDepth = curMin < _minCascadeDepth ? curMin : _minCascadeDepth;
            _maxCascadeDepth = curMax > _maxCascadeDepth ? curMax : _maxCascadeDepth;
        }

        // used to undo depth bias in shadow maps
        _texelSizes = new Vector4(
            _shadowCasterCascadesCount > 0 ? (2.0f / _mainLightShadowProjectionMatrices[0].m00) / _cascadeSlices[0].resolution : 0,
            _shadowCasterCascadesCount > 1 ? (2.0f / _mainLightShadowProjectionMatrices[1].m00) / _cascadeSlices[1].resolution : 0,
            _shadowCasterCascadesCount > 2 ? (2.0f / _mainLightShadowProjectionMatrices[2].m00) / _cascadeSlices[2].resolution : 0,
            _shadowCasterCascadesCount > 3 ? (2.0f / _mainLightShadowProjectionMatrices[3].m00) / _cascadeSlices[3].resolution : 0
            );

        if (_initializeBuffers)
        {
            //Debug.Log("initializing");
            // could reduce to only max size plus half size instead of accounting for all sizes
            // but this change wouldn't save a significant amount of size 4/3 to 5/4 (16/12 to 15/12)
            // could also store 16 quads per int to reduce size significantly.
            _tesselationMap = new ComputeBuffer(_totalTesselationSize, sizeof(uint));

            _laplacianMaps = new ComputeBuffer(_totalLaplacianSize, sizeof(uint));

            // multiplied by 2 because these are stored as uint2 in compute shader
            // store instead on only one int. 12 most significant bits reserves for parents. enough for 64x64 parents
            // store tesselation in 16 least significant bits. enough for 8 levels of tesselation
            // reserve 2 bits for starter index
            // reserve 2 bits for neighbors
            // total of 32 bits
            _readQuadsBuffer = new ComputeBuffer(_maxQuadSize, sizeof(uint));

            _writeQuadsBuffer = new ComputeBuffer(_maxQuadSize, sizeof(uint));

            _emitQuadsBuffer = new ComputeBuffer(_maxQuadSize, sizeof(uint));

            _writeArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);

            _quadArgsBuffer = new ComputeBuffer(6, sizeof(uint), ComputeBufferType.IndirectArguments);

            _emitEdgesBuffer = new ComputeBuffer(_maxEdgeSize, sizeof(uint));

            _edgeArgsBuffer = new ComputeBuffer(6, sizeof(uint), ComputeBufferType.IndirectArguments);

            _countsBuffer = new ComputeBuffer(4, sizeof(uint));

            _tesselationDepthData = new ComputeBuffer(_maxTesselation, (sizeof(uint) * 5) + (sizeof(float) * 16), ComputeBufferType.Structured);

            _laplacianKernel = new ComputeBuffer(_laplacianKernelRadius * _laplacianKernelRadius, sizeof(float));

            _blurKernel = new ComputeBuffer(_blurKernelRadius, sizeof(float));

            _initializeBuffers = false;
        }

        // fetch a command buffer to use
        CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
        cmd.Clear();

        uint xGroupSize;
        uint yGroupSize;
        uint zGroupSize;

        // shadow sampling
        ComputeShadowSample(cmd, shadowLight, shadowData, shadowLightIndex, _localShadowTarget);

        // laplacian edge map
        int kernelKernel = _laplacianCompute.FindKernel("LaplacianKernel");
        int laplacianKernel = _laplacianCompute.FindKernel("ComputeLaplacian");

        cmd.SetComputeIntParam(_laplacianCompute, "_textureSize", _maxLaplacianWidth);
        cmd.SetComputeIntParam(_laplacianCompute, "_kernelRadius", _laplacianKernelRadius);
        cmd.SetComputeFloatParam(_laplacianCompute, "_stDev", _laplacianGaussianStandardDeviation);
        cmd.SetComputeFloatParam(_laplacianCompute, "_minCascadeDepth", _minCascadeDepth);
        cmd.SetComputeFloatParam(_laplacianCompute, "_maxCascadeDepth", _maxCascadeDepth);

        _laplacianCompute.GetKernelThreadGroupSizes(kernelKernel, out xGroupSize, out yGroupSize, out zGroupSize);
        cmd.SetComputeBufferParam(_laplacianCompute, kernelKernel, "_KernelBuffer", _laplacianKernel);

        cmd.DispatchCompute(_laplacianCompute, kernelKernel,
            Mathf.CeilToInt((_laplacianKernelRadius) / (float)xGroupSize),
            1,
            1);

        _laplacianCompute.GetKernelThreadGroupSizes(laplacianKernel, out xGroupSize, out yGroupSize, out zGroupSize);
        cmd.SetComputeTextureParam(_laplacianCompute, laplacianKernel, "_BaseTexture", _localShadowTarget);
        cmd.SetComputeBufferParam(_laplacianCompute, laplacianKernel, "_KernelBuffer", _laplacianKernel);
        cmd.SetComputeBufferParam(_laplacianCompute, laplacianKernel, "_LaplacianMaps", _laplacianMaps);

        cmd.DispatchCompute(_laplacianCompute, laplacianKernel,
            Mathf.CeilToInt(_maxLaplacianWidth / (float)xGroupSize),
            Mathf.CeilToInt(_maxLaplacianWidth / (float)yGroupSize),
            1);

        // downsample the laplacian
        int downsampleKernel = _downsampleBufferCompute.FindKernel("Downsample");
        int laplacianWidth = _maxLaplacianWidth / 2;
        cmd.SetComputeBufferParam(_downsampleBufferCompute, downsampleKernel, "_LaplacianMaps", _laplacianMaps);
        for (int i = 0; i < _laplacianLevels - 1; i++)
        {
            cmd.SetComputeIntParam(_downsampleBufferCompute, "_maxLaplacianWidth", _maxLaplacianWidth);
            cmd.SetComputeIntParam(_downsampleBufferCompute, "_outputWidth", laplacianWidth);
            cmd.SetComputeIntParam(_downsampleBufferCompute, "_downsampleLevel", i);

            cmd.DispatchCompute(_downsampleBufferCompute, downsampleKernel,
                Mathf.CeilToInt(laplacianWidth / (float)xGroupSize),
                Mathf.CeilToInt(laplacianWidth / (float)yGroupSize),
                1);

            laplacianWidth /= 2;
        }

        // downsample 
        ComputeDepthDownsample(cmd, _depthDownsample);

        ComputeLightMesh(cmd, renderingData, shadowLight, _downsampleTemp0);

        ComputePatchTexture(cmd, _downsampleTemp0, _downsampleTemp1);

        // atmosphere
        ComputeAtmosphere(cmd, renderingData, shadowLight, _downsampleTemp1, _downsampleTemp0);

        // blur
        ComputeBlur(cmd, _downsampleTemp1, _downsampleTemp0);

        // upsample
        ComputeUpsample(cmd, _downsampleTemp0, _temp0);

        // blit to screen
        // we apply our material while blitting to a temporary texture
        cmd.Blit(_cameraColorTarget, _temp1);
        cmd.SetGlobalTexture("_CameraSource", _temp1);
        cmd.Blit(_temp0, _cameraColorTarget, _compositeMaterial, 0);
        //cmd.Blit(_downsampleTemp0, _cameraColorTarget, _compositeMaterial, 0);
        // ...then blit it back again 

        // don't forget to tell ScriptableRenderContext to actually execute the commands
        context.ExecuteCommandBuffer(cmd);
        // tidy up after ourselves
        cmd.Clear();
        CommandBufferPool.Release(cmd);
        //context.Submit();
    }

    private void ComputeShadowSample(CommandBuffer cmd, VisibleLight shadowLight, ShadowData shadowData, int shadowLightIndex, RenderTargetIdentifier endTexture)
    {
        cmd.SetGlobalMatrix("_worldToLight", shadowLight.localToWorldMatrix.inverse);
        cmd.SetGlobalMatrix("_lightToWorld", shadowLight.localToWorldMatrix);
        cmd.SetGlobalMatrix("_shadowToWorld0", _mainLightShadowMatricesInverse[0]);
        cmd.SetGlobalMatrix("_shadowToWorld1", _mainLightShadowMatricesInverse[1]);
        cmd.SetGlobalMatrix("_shadowToWorld2", _mainLightShadowMatricesInverse[2]);
        cmd.SetGlobalMatrix("_shadowToWorld3", _mainLightShadowMatricesInverse[3]);
        cmd.SetGlobalMatrix("_worldToShadow0", _mainLightShadowMatrices[0]);
        cmd.SetGlobalMatrix("_worldToShadow1", _mainLightShadowMatrices[1]);
        cmd.SetGlobalMatrix("_worldToShadow2", _mainLightShadowMatrices[2]);
        cmd.SetGlobalMatrix("_worldToShadow3", _mainLightShadowMatrices[3]);
        cmd.SetGlobalVector("_shadowSplitSphere0", _cascadeSplitDistances[0]);
        cmd.SetGlobalVector("_shadowSplitSphere1", _cascadeSplitDistances[1]);
        cmd.SetGlobalVector("_shadowSplitSphere2", _cascadeSplitDistances[2]);
        cmd.SetGlobalVector("_shadowSplitSphere3", _cascadeSplitDistances[3]);

        cmd.SetGlobalVector("_cascadeShadowSplitSphereRadii", new Vector4(
            _cascadeSplitDistances[0].w,
            _cascadeSplitDistances[1].w,
            _cascadeSplitDistances[2].w,
            _cascadeSplitDistances[3].w));
        cmd.SetGlobalVector("_texelSizes", _texelSizes);

        cmd.SetGlobalVector("_lightDirection", shadowLight.light.transform.forward);
        //cmd.SetGlobalVector("_lightCameraChunk", _lightCameraChunk);

        cmd.SetGlobalVector("_centerOffset", new Vector3(_lightCameraChunk.x + (_chunkSize / 2f), _lightCameraChunk.y + (_chunkSize / 2f), 0));
        cmd.SetGlobalVector("_laplacianWorldSize", new Vector2(_baseTesselationMapWidth * _chunkSize, _baseTesselationMapWidth * _chunkSize));
        //cmd.SetGlobalFloat("_chunkWorldSize", _chunkSize);
        cmd.SetGlobalFloat("_depthBias", shadowData.bias[shadowLightIndex].x);
        cmd.SetGlobalFloat("_maxCascadeDepth", _maxCascadeDepth);
        cmd.SetGlobalFloat("_maxLightDepth", _maxCascadeDepth);
        cmd.SetGlobalFloat("_minLightDepth", -Mathf.Infinity);

        cmd.Blit(null, endTexture, _shadowSampleMaterial, 0);
    }

    private void ComputeDepthDownsample(CommandBuffer cmd, RenderTargetIdentifier endTexture)
    {
        int downsamplePass = _smartSampleMaterial.FindPass("DownsamplePoint");
        _smartSampleMaterial.SetInt("downsampleAmount", _downsampleAmount);
        // downsamples the camera depth
        cmd.Blit(_cameraDepthTarget, endTexture, _smartSampleMaterial, downsamplePass);
    }

    private void ComputeLightMesh(CommandBuffer cmd, RenderingData renderingData, VisibleLight shadowLight, RenderTargetIdentifier endTexture)
    {
        int initializeTesselationKernel = _lightVolumeCompute.FindKernel("InitializeTesselationMap");
        int initializeLayerKernel = _lightVolumeCompute.FindKernel("InitializeFirstLayer");
        int initializeQuadsKernel = _lightVolumeCompute.FindKernel("InitializeQuadBuffers");
        int initializeEdgesKernel = _lightVolumeCompute.FindKernel("InitializeEdgeBuffers");
        int initializeCountsKernel = _lightVolumeCompute.FindKernel("InitializeCountsBuffer");
        int initializeArgsKernel = _lightVolumeCompute.FindKernel("InitializeArgBuffers");
        int levelKernel = _lightVolumeCompute.FindKernel("ComputeLevel");
        int compareKernel = _lightVolumeCompute.FindKernel("Compare");
        int copyToArgsKernel = _lightVolumeCompute.FindKernel("CopyToArgs");
        int swapKernel = _lightVolumeCompute.FindKernel("SwapReadWrite");
        int prepareNextKernel = _lightVolumeCompute.FindKernel("PrepareNextLoop");
        int projectionPass = _lightGlobalMeshMaterial.FindPass("ProjectionMeshPass");
        int edgePass = _lightGlobalMeshMaterial.FindPass("EdgeMeshPass");
        int ceilingPass = _lightGlobalMeshMaterial.FindPass("CeilingMeshPass");

        uint xGroupSize;
        uint yGroupSize;
        uint zGroupSize;

        // pre-calculate any data the mesh compute shader will need for each level of tesselation
        if (_depthDataArray == null)
        {
            _depthDataArray = new tesselationDepthData[_maxTesselation];
        }
        for (int i = 0; i < _maxTesselation; i++)
        {
            uint tesselationMapWidth = (uint)(_baseTesselationMapWidth * Mathf.RoundToInt(Mathf.Pow(2, i)));
            uint currentWidth = tesselationMapWidth / 2;
            uint mapIndex = 0;
            while (currentWidth >= _baseTesselationMapWidth)
            {
                mapIndex += (uint)Mathf.RoundToInt(Mathf.Pow(currentWidth, 2));
                currentWidth /= 2;
            }

            currentWidth = tesselationMapWidth;
            uint nextMapIndex = 0;
            while (currentWidth >= _baseTesselationMapWidth)
            {
                nextMapIndex += (uint)Mathf.RoundToInt(Mathf.Pow(currentWidth, 2));
                currentWidth /= 2;
            }

            uint levelLaplacianWidth = (uint)_maxLaplacianWidth;
            uint laplacianLevel = 0;
            while (true) // this one gets nearst smaller
            {
                if (laplacianLevel >= _laplacianLevels - 1)
                {
                    break;
                }
                if (levelLaplacianWidth < tesselationMapWidth)
                {
                    break;
                }
                levelLaplacianWidth /= 2;
                laplacianLevel++;
            }
            uint laplacianTextureWidth = (uint)Mathf.RoundToInt(Mathf.Pow(2, (_laplacianLevels - laplacianLevel) + 1));

            uint curLaplacianTextureWidth = (uint)_maxLaplacianWidth;
            uint laplacianStartIndex = 0;
            for (int j = 0; j < laplacianLevel; j++)
            {
                laplacianStartIndex += (uint)Mathf.RoundToInt(Mathf.Pow(curLaplacianTextureWidth, 2));
                curLaplacianTextureWidth /= 2;
            }

            Vector3 scaleVector = Vector3.one * (1f / (float)tesselationMapWidth) * _baseTesselationMapWidth;
            scaleVector = new Vector3(scaleVector.x * _chunkSize, scaleVector.y * _chunkSize, scaleVector.z);
            // 1 / base should actually just be tesselation width for that level
            scaleVector = new Vector3(scaleVector.x, scaleVector.y, 1);
            //Vector3 translationVector = -(Vector2.one * Mathf.Floor(_chunksInWidth / 2f));
            Vector3 translationVector = -(Vector2.one * (_baseTesselationMapWidth / 2f));
            translationVector = new Vector3(translationVector.x * _chunkSize, translationVector.y * _chunkSize, translationVector.z);
            translationVector = new Vector3(translationVector.x, translationVector.y, 0) + _lightCameraChunk;

            Matrix4x4 coordsToLight = Matrix4x4.TRS(translationVector, Quaternion.identity, scaleVector);

            _depthDataArray[i] = new tesselationDepthData
            {
                _tesselationMapWidth = tesselationMapWidth,
                _mapIndex = mapIndex,
                _nextMapIndex = nextMapIndex,
                _laplacianTextureWidth = laplacianTextureWidth,
                _laplacianStartIndex = laplacianStartIndex,
                _coordsToLight = coordsToLight
            };
        }

        _lightVolumeCompute.GetKernelThreadGroupSizes(levelKernel, out xGroupSize, out yGroupSize, out zGroupSize);

        cmd.SetBufferData(_tesselationDepthData, _depthDataArray);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_TesselationDepthData", _tesselationDepthData);
        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_TesselationDepthData", _tesselationDepthData);

        cmd.SetComputeIntParam(_lightVolumeCompute, "_totalTesselationSize", _totalTesselationSize);
        cmd.SetComputeIntParam(_lightVolumeCompute, "_maxQuadSize", _maxQuadSize);
        cmd.SetComputeIntParam(_lightVolumeCompute, "_maxEdgeSize", _maxEdgeSize);
        cmd.SetComputeIntParam(_lightVolumeCompute, "_baseTesselationMapSize", _baseTesselationMapSize);

        cmd.SetComputeIntParam(_lightVolumeCompute, "_maxTesselation", _maxTesselation);
        cmd.SetComputeIntParam(_lightVolumeCompute, "_baseTesselationMapWidth", _baseTesselationMapWidth);
        cmd.SetComputeVectorParam(_lightVolumeCompute, "_cameraLightPos", shadowLight.localToWorldMatrix.inverse * renderingData.cameraData.camera.transform.position);
        cmd.SetComputeFloatParam(_lightVolumeCompute, "_fovYRads", renderingData.cameraData.camera.fieldOfView * Mathf.Deg2Rad);
        cmd.SetComputeFloatParam(_lightVolumeCompute, "_distFactor", _distanceFactor);
        cmd.SetComputeIntParam(_lightVolumeCompute, "_maxLaplacianWidth", _maxLaplacianWidth);
        cmd.SetComputeIntParam(_lightVolumeCompute, "_laplacianLevels", _laplacianLevels);

        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeTesselationKernel, "_TesselationMap", _tesselationMap);
        cmd.DispatchCompute(_lightVolumeCompute, initializeTesselationKernel, Mathf.Max(Mathf.CeilToInt(_totalTesselationSize / (float)xGroupSize), 1), 1, 1);

        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeLayerKernel, "_TesselationMap", _tesselationMap);
        cmd.DispatchCompute(_lightVolumeCompute, initializeLayerKernel, Mathf.Max(Mathf.CeilToInt(_baseTesselationMapSize / (float)xGroupSize), 1), 1, 1);

        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeQuadsKernel, "_ReadQuadsBuffer", _readQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeQuadsKernel, "_WriteQuadsBuffer", _writeQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeQuadsKernel, "_EmitQuads", _emitQuadsBuffer);
        cmd.DispatchCompute(_lightVolumeCompute, initializeQuadsKernel, Mathf.Max(Mathf.CeilToInt(_maxQuadSize / (float)xGroupSize), 1), 1, 1);

        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeEdgesKernel, "_EmitEdges", _emitEdgesBuffer);
        cmd.DispatchCompute(_lightVolumeCompute, initializeEdgesKernel, Mathf.Max(Mathf.CeilToInt(_maxEdgeSize / (float)xGroupSize), 1), 1, 1);

        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeCountsKernel, "_CountsBuffer", _countsBuffer);
        cmd.DispatchCompute(_lightVolumeCompute, initializeCountsKernel, 1, 1, 1);

        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeArgsKernel, "_WriteArgs", _writeArgsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeArgsKernel, "_QuadArgs", _quadArgsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, initializeArgsKernel, "_EdgeArgs", _edgeArgsBuffer);
        cmd.DispatchCompute(_lightVolumeCompute, initializeArgsKernel, 1, 1, 1);

        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_TesselationMap", _tesselationMap);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_LaplacianTextures", _laplacianMaps);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_ReadQuadsBuffer", _readQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_WriteQuadsBuffer", _writeQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_CountsBuffer", _countsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_EmitQuads", _emitQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, levelKernel, "_EmitEdges", _emitEdgesBuffer);

        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_TesselationMap", _tesselationMap);
        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_ReadQuadsBuffer", _readQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_WriteQuadsBuffer", _writeQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_CountsBuffer", _countsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_EmitQuads", _emitQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, compareKernel, "_EmitEdges", _emitEdgesBuffer);

        cmd.SetComputeBufferParam(_lightVolumeCompute, copyToArgsKernel, "_CountsBuffer", _countsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, copyToArgsKernel, "_WriteArgs", _writeArgsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, copyToArgsKernel, "_EdgeArgs", _edgeArgsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, copyToArgsKernel, "_QuadArgs", _quadArgsBuffer);

        cmd.SetComputeBufferParam(_lightVolumeCompute, swapKernel, "_CountsBuffer", _countsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, swapKernel, "_ReadQuadsBuffer", _readQuadsBuffer);
        cmd.SetComputeBufferParam(_lightVolumeCompute, swapKernel, "_WriteQuadsBuffer", _writeQuadsBuffer);

        cmd.SetComputeBufferParam(_lightVolumeCompute, prepareNextKernel, "_CountsBuffer", _countsBuffer);

        _meshBlock.SetBuffer("_Quads", _emitQuadsBuffer);
        _meshBlock.SetBuffer("_Edges", _emitEdgesBuffer);
        _meshBlock.SetMatrix("_lightToWorld", shadowLight.localToWorldMatrix);
        _meshBlock.SetInteger("_maxTesselation", _maxTesselation);
        _meshBlock.SetInteger("_baseTesselationWidth", _baseTesselationMapWidth);
        _meshBlock.SetVector("_baseChunkScale", new Vector2(_chunkSize, _chunkSize));
        _meshBlock.SetVector("_centerPos", _lightCameraChunk);
        _meshBlock.SetFloat("_edgeHeight", _edgeHeight);

        cmd.SetRenderTarget(endTexture);
        cmd.ClearRenderTarget(true, true, Color.clear, 1f);
        // draw ceiling
        cmd.DrawProcedural(Matrix4x4.identity, _lightGlobalMeshMaterial, ceilingPass, MeshTopology.Triangles, 6, 0, _meshBlock);
        for (int i = 0; i < _maxTesselation; i++)
        {
            //cmd.SetComputeTextureParam(_lightVolumeCompute, levelKernel, "_OutputTex", _outTarget[i]);
            //cmd.SetComputeTextureParam(_lightVolumeCompute, compareKernel, "_OutputTex", _outTarget[i]);

            cmd.DispatchCompute(_lightVolumeCompute, levelKernel, _writeArgsBuffer, 0);

            cmd.DispatchCompute(_lightVolumeCompute, compareKernel, _writeArgsBuffer, 0);

            cmd.DispatchCompute(_lightVolumeCompute, copyToArgsKernel, 1, 1, 1);

            cmd.DispatchCompute(_lightVolumeCompute, swapKernel, _writeArgsBuffer, 0);

            cmd.DispatchCompute(_lightVolumeCompute, prepareNextKernel, 1, 1, 1);

            cmd.DrawProceduralIndirect(Matrix4x4.identity, _lightGlobalMeshMaterial, projectionPass, MeshTopology.Triangles, _quadArgsBuffer, 0, _meshBlock);

            cmd.DrawProceduralIndirect(Matrix4x4.identity, _lightGlobalMeshMaterial, edgePass, MeshTopology.Triangles, _edgeArgsBuffer, 0, _meshBlock);
        }
    }
    
    private void ComputePatchTexture(CommandBuffer cmd, RenderTargetIdentifier textureToPatch, RenderTargetIdentifier endTexture)
    {
        int patchKernel = _patchTextureCompute.FindKernel("Patch");

        uint xGroupSize;
        uint yGroupSize;
        uint zGroupSize;

        _patchTextureCompute.GetKernelThreadGroupSizes(patchKernel, out xGroupSize, out yGroupSize, out zGroupSize);

        cmd.SetComputeIntParams(_patchTextureCompute, "_textureSize", new int[] { _downsampleTextureWidth, _downsampleTextureHeight });
        cmd.SetComputeFloatParam(_patchTextureCompute, "_patchThreshold", _patchThreshold);
        cmd.SetComputeTextureParam(_patchTextureCompute, patchKernel, "_BaseTexture", textureToPatch);
        cmd.SetComputeTextureParam(_patchTextureCompute, patchKernel, "_PatchedTexture", endTexture);

        cmd.DispatchCompute(_patchTextureCompute, patchKernel,
            Mathf.CeilToInt(_downsampleTextureWidth / (float)xGroupSize),
            Mathf.CeilToInt(_downsampleTextureHeight / (float)yGroupSize),
            1);
    }
   
    private void ComputeAtmosphere(CommandBuffer cmd, RenderingData renderingData, VisibleLight shadowLight, RenderTargetIdentifier meshDepthTexture, RenderTargetIdentifier endTexture)
    {
        cmd.SetGlobalMatrix("_cameraInverseProjection", renderingData.cameraData.camera.projectionMatrix.inverse);
        cmd.SetGlobalMatrix("_cameraToWorld", renderingData.cameraData.camera.cameraToWorldMatrix);
        cmd.SetGlobalVector("_lightDirection", shadowLight.light.transform.forward);
        cmd.SetGlobalVector("_fogColor", _fogColor);
        cmd.SetGlobalVector("_ambientColor", _ambientColor);
        cmd.SetGlobalVector("_extinctionColor", _extinctionColor);
        cmd.SetGlobalFloat("_maxDepth", _maxDepth);
        cmd.SetGlobalFloat("_fogDensity", _fogDensity);
        cmd.SetGlobalFloat("_extinctionCoef", _extinctionCoef);
        cmd.SetGlobalFloat("_meiSactteringCoef", _mieScatteringCoef);
        cmd.SetGlobalFloat("_rayleighSactteringCoef", _rayleighSactteringCoef);

        cmd.Blit(meshDepthTexture, endTexture, _atmosphereMaterial, 0);
    }

    private void ComputeBlur(CommandBuffer cmd, RenderTargetIdentifier tempTexture, RenderTargetIdentifier textureToBlurOutput)
    {
        // should cache kernel instead of calculating it every frame;
        int blurKernelKernel = _blurCompute.FindKernel("BlurKernel");
        int blurKernel = _blurCompute.FindKernel("ComputeBlur");

        uint xGroupSize;
        uint yGroupSize;
        uint zGroupSize;

        _blurCompute.GetKernelThreadGroupSizes(blurKernelKernel, out xGroupSize, out yGroupSize, out zGroupSize);

        cmd.SetComputeIntParam(_blurCompute, "_kernelRadius", _blurKernelRadius);
        cmd.SetComputeFloatParam(_blurCompute, "_stDev", _blurGaussianStandardDeviation);
        cmd.SetComputeIntParams(_blurCompute, "_textureSize", new int[] { _downsampleTextureWidth, _downsampleTextureHeight });
        //cmd.SetComputeIntParam(_blurCompute, "_downsampleRatio", _downsampleAmount);
        cmd.SetComputeFloatParam(_blurCompute, "_blurDepthFalloff", _blurDepthFalloff);

        cmd.SetComputeBufferParam(_blurCompute, blurKernelKernel, "_Kernel", _blurKernel);
        // creates the kernel
        cmd.DispatchCompute(_blurCompute, blurKernelKernel, Mathf.CeilToInt(_blurKernelRadius / (float)xGroupSize), 1, 1);

        cmd.SetComputeBufferParam(_blurCompute, blurKernel, "_Kernel", _blurKernel);
        cmd.SetComputeTextureParam(_blurCompute, blurKernel, "_CameraDepthTextureDownsampled", _depthDownsample);
        cmd.SetComputeTextureParam(_blurCompute, blurKernel, "_BaseTexture", textureToBlurOutput);
        cmd.SetComputeTextureParam(_blurCompute, blurKernel, "_BlurTexture", tempTexture);
        cmd.SetComputeIntParams(_blurCompute, "_direction", new int[] { 1, 0 });

        _blurCompute.GetKernelThreadGroupSizes(blurKernel, out xGroupSize, out yGroupSize, out zGroupSize);
        //blurs in one axis
        cmd.DispatchCompute(_blurCompute, blurKernel,
            Mathf.CeilToInt(_downsampleTextureWidth / (float)xGroupSize),
            Mathf.CeilToInt(_downsampleTextureHeight / (float)yGroupSize),
            1);

        cmd.SetComputeTextureParam(_blurCompute, blurKernel, "_BaseTexture", tempTexture);
        cmd.SetComputeTextureParam(_blurCompute, blurKernel, "_BlurTexture", textureToBlurOutput);
        cmd.SetComputeIntParams(_blurCompute, "_direction", new int[] { 0, 1 });
        // blurs in the other axis
        cmd.DispatchCompute(_blurCompute, blurKernel,
            Mathf.CeilToInt(_downsampleTextureWidth / (float)xGroupSize),
            Mathf.CeilToInt(_downsampleTextureHeight / (float)yGroupSize),
            1);
    }

    private void ComputeUpsample(CommandBuffer cmd, RenderTargetIdentifier textureToUpsample, RenderTargetIdentifier endTexture)
    {
        int upsamplePass = _smartSampleMaterial.FindPass("Upsample");
        _smartSampleMaterial.SetFloat("threshold", _upsampleDepthThreshold);

        cmd.Blit(textureToUpsample, endTexture, _smartSampleMaterial, upsamplePass);
    }

    // called after Execute, use it to clean up anything allocated in Configure
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(_temp0Id);
        cmd.ReleaseTemporaryRT(_temp1Id);
        cmd.ReleaseTemporaryRT(_downsampleTemp0Id);
        cmd.ReleaseTemporaryRT(_downsampleTemp1Id);
        cmd.ReleaseTemporaryRT(_depthDownsampleId);
        cmd.ReleaseTemporaryRT(_localShadowTargetId);

        /*
        for (int i = 0; i < 10; i++)
        {
            cmd.ReleaseTemporaryRT(_outTargetID[i]);
        }
        */
    }

    void ClearShadowData()
    {
        for (int i = 0; i < _mainLightShadowMatrices.Length; ++i)
            _mainLightShadowMatrices[i] = Matrix4x4.identity;

        for (int i = 0; i < _cascadeSplitDistances.Length; ++i)
            _cascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

        for (int i = 0; i < _cascadeSlices.Length; ++i)
            _cascadeSlices[i].Clear();
    }
}