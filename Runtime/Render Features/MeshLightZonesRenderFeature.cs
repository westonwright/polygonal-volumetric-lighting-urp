using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System;

public class MeshLightZonesRenderFeature : ScriptableRendererFeature
{
    #region Constant properties
    //const string _packageName = "com.weston-wright.polygonal-volumetric-lighting-urp";
    const string _shadersPath = "Shaders/";
    const string _computePath = "Compute/";
    /*
    const string _shadowSampleShaderName = "ShadowSample.shader";
    const string _boxDepthShaderName = "BoxMeshDepth.shader";
    const string _mixDepthShaderName = "MixDepths.shader";
    const string _lightZoneMeshShaderName = "LightZoneMesh.shader";
    const string _atmosphereShaderName = "Atmosphere.shader";
    const string _smartSampleShaderName = "SmartSample.shader";
    const string _compositeShaderName = "Composite.shader";
    const string _laplacianComputeName = "Laplacian.compute";
    const string _downsampleBufferComputeName = "DownsampleBuffer.compute";
    const string _lightVolumeComputeName = "LightVolume.compute";
    const string _patchTextureComputeName = "PatchTexture.compute";
    const string _blurComputeName = "Blur.compute";
    */
    const string _shadowSampleShaderName = "ShadowSample";
    const string _boxDepthShaderName = "BoxMeshDepth";
    const string _mixDepthShaderName = "MixDepths";
    const string _lightZoneMeshShaderName = "LightZoneMesh";
    const string _atmosphereShaderName = "Atmosphere";
    const string _smartSampleShaderName = "SmartSample";
    const string _compositeShaderName = "Composite";
    const string _laplacianComputeName = "Laplacian";
    const string _downsampleBufferComputeName = "DownsampleBuffer";
    const string _lightVolumeComputeName = "LightVolume";
    const string _patchTextureComputeName = "PatchTexture";
    const string _blurComputeName = "Blur";
    const string _cubeName = "Cube.fbx";
    #endregion

    #region Public properties and methods
    public RenderPassEvent renderPassEvent
    {
        get { return _renderPassEvent; }
        set { _renderPassEvent = value; }
    }
    [SerializeField]
    RenderPassEvent _renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    [SerializeField]
    private bool renderInSceneView = false;

    public float laplacianGaussianStandardDeviation
    {
        get { return _laplacianGaussianStandardDeviation; }
        set { _laplacianGaussianStandardDeviation = value; }
    }
    [SerializeField]
    [Range(.1f, 5f)]
    float _laplacianGaussianStandardDeviation = .8f;
    
    public int laplacianKernelRadius
    {
        get { return _laplacianKernelRadius; }
        set { _laplacianKernelRadius = value; }
    }
    [SerializeField]
    [Range(2, 12)]
    int _laplacianKernelRadius = 4;

    public int maxLaplacianWidth
    {
        get
        {
            int closestPower = 128;
            while (closestPower < _maxLaplacianWidth)
            {
                closestPower *= 2;
            }
            return closestPower;
        }
        set
        {
            int closestPower = 128;
            while (closestPower < value)
            {
                closestPower *= 2;
            }
            _maxLaplacianWidth = closestPower;
        }
    }
    [SerializeField]
    [Range(128, 4096)]
    int _maxLaplacianWidth = 512;
    
    // force to be even
    public int chunksInWidth
    {
        get { return _chunksInWidth - (_chunksInWidth % 2); }
        set { _chunksInWidth = value - (value % 2); }
    }
    [SerializeField]
    [Range(2, 64)]
    int _chunksInWidth = 6;
    
    public int maxTesselation
    {
        get { return _maxTesselation; }
        set { _maxTesselation = value; }
    }
    [SerializeField]
    [Range(1, 8)]
    int _maxTesselation = 4;
    
    public float distanceFactor
    {
        get { return _distanceFactor; }
        set { _distanceFactor = value; }
    }
    [SerializeField]
    //[Range(.01f, 2f)]
    float _distanceFactor = 128f;
    
    public int downsampleAmount
    {
        get { return _downsampleAmount; }
        set { _downsampleAmount = value; }
    }
    [SerializeField]
    [Range(1, 4)]
    int _downsampleAmount = 2;
    
    public float patchThreshold
    {
        get { return _patchThreshold; }
        set { _patchThreshold = value; }
    }
    [SerializeField]
    float _patchThreshold = 10f;

    public float blurGaussianStandardDeviation
    {
        get { return _blurGaussianStandardDeviation; }
        set { _blurGaussianStandardDeviation = value; }
    }
    [SerializeField]
    [Range(.1f, 12f)]
    float _blurGaussianStandardDeviation = .8f;

    public int blurKernelRadius
    {
        get { return _blurKernelRadius; }
        set { _blurKernelRadius = value; }
    }
    [SerializeField]
    [Range(2, 12)]
    int _blurKernelRadius = 4;
    
    public float blurDepthFalloff
    {
        get { return _blurDepthFalloff; }
        set { _blurDepthFalloff = value; }
    }
    [SerializeField]
    [Range(0, 1)]
    float _blurDepthFalloff = .01f;
    
    public float extinctionCoefficient
    {
        get { return _extinctionCoefficient; }
        set { _extinctionCoefficient = value; }
    }
    [SerializeField]
    [Range(0, .1f)]
    float _extinctionCoefficient = .01f;
    
    public float meiSactteringCoefficient
    {
        get { return _meiSactteringCoefficient; }
        set { _meiSactteringCoefficient = value; }
    }
    [SerializeField]
    [Range(-1, 1)]
    float _meiSactteringCoefficient = 1f;
    

    public float rayleighSactteringCoefficient
    {
        get { return _rayleighSactteringCoefficient; }
        set { _rayleighSactteringCoefficient = value; }
    }
    [SerializeField]
    [Range(0, 10)]
    float _rayleighSactteringCoefficient = 1f;
    
    public float maxDepth
    {
        get { return _maxDepth; }
        set { _maxDepth = value; }
    }
    [SerializeField]
    float _maxDepth = 256f;
    
    public float fogDensity
    {
        get { return _fogDensity; }
        set { _fogDensity = value; }
    }
    [SerializeField]
    [Range(0, 1)]
    float _fogDensity = .5f;
    
    public Color fogColor
    {
        get { return _fogColor; }
        set { _fogColor = value; }
    }
    [SerializeField]
    Color _fogColor = Color.white;
    
    public Color ambientColor
    {
        get { return _ambientColor; }
        set { _ambientColor = value; }
    }
    [SerializeField]
    Color _ambientColor = Color.black;
    
    public Color extinctionColor
    {
        get { return _extinctionColor; }
        set { _extinctionColor = value; }
    }
    [SerializeField]
    Color _extinctionColor = Color.black;

    public void AddLightZone(LightVolumeZone lightZone)
    {
        if (!lightZones.Contains(lightZone))
        {
            lightZones.Add(lightZone);
        }
    }

    public void RemoveLightZone(LightVolumeZone lightZone)
    {
        if (lightZones.Contains(lightZone))
        {
            lightZones.Remove(lightZone);
        }
    }

    #endregion

    #region Private properties

    Shader _shadowSampleShader;
    Shader _boxDepthShader;
    Shader _mixDepthShader;
    Shader _lightZoneMeshShader;
    Shader _atmosphereShader;
    Shader _smartSampleShader;
    Shader _compositeShader;

    Material _shadowSampleMaterial;
    Material _boxDepthMaterial;
    Material _mixDepthMaterial;
    Material _lightVolumeMeshMaterial;
    Material _atmosphereMaterial;
    Material _smartSampleMaterial;
    Material _compositeMaterial;

    ComputeShader _laplacianCompute;
    ComputeShader _downsampleBufferCompute;
    ComputeShader _lightVolumeCompute;
    ComputeShader _patchTextureCompute;
    ComputeShader _blurCompute;

    Mesh _cubeMesh = null;

    List<LightVolumeZone> lightZones = new List<LightVolumeZone>();

    MeshLightZonesPass _renderPass = null;

    UniversalRenderPipelineAsset _pipeline;

    //string _dataPath;

    bool _initialized = false;
    #endregion

    private bool Initialize()
    {
        if (!LoadShader(ref _shadowSampleShader, _shadersPath, _shadowSampleShaderName)) return false;
        if (!LoadShader(ref _boxDepthShader, _shadersPath, _boxDepthShaderName)) return false;
        if (!LoadShader(ref _lightZoneMeshShader, _shadersPath, _lightZoneMeshShaderName)) return false;
        if (!LoadShader(ref _mixDepthShader, _shadersPath, _mixDepthShaderName)) return false;
        if (!LoadShader(ref _atmosphereShader, _shadersPath, _atmosphereShaderName)) return false;
        if (!LoadShader(ref _smartSampleShader, _shadersPath, _smartSampleShaderName)) return false;
        if (!LoadShader(ref _compositeShader, _shadersPath, _compositeShaderName)) return false;
        if (!LoadCompute(ref _laplacianCompute, _computePath, _laplacianComputeName)) return false;
        if (!LoadCompute(ref _downsampleBufferCompute, _computePath, _downsampleBufferComputeName)) return false;
        if (!LoadCompute(ref _lightVolumeCompute, _computePath, _lightVolumeComputeName)) return false;
        if (!LoadCompute(ref _patchTextureCompute, _computePath, _patchTextureComputeName)) return false;
        if (!LoadCompute(ref _blurCompute, _computePath, _blurComputeName)) return false;
        if (!LoadBuiltinMesh(ref _cubeMesh, _cubeName)) return false;
        return true;
    }

    private bool LoadShader(ref Shader shaderRefrence, string filePath, string fileName)
    {
        //Debug.Log(_packageName + filePath + fileName);
        shaderRefrence = Resources.Load<Shader>(filePath + fileName);
        //shaderRefrence = (Shader)AssetDatabase.LoadAssetAtPath(_dataPath + _packageName + filePath + fileName, typeof(Shader));
        if (shaderRefrence == null)
        {
            Debug.LogError("Missing Shader " + fileName + "! Package may be corrupted!");
            Debug.LogError("Please reimport Package");
            return false;
        }
        return true;
    }
    private bool LoadCompute(ref ComputeShader computeRefrence, string filePath, string fileName)
    {
        //Debug.Log(_packageName + filePath + fileName);
        computeRefrence = Resources.Load<ComputeShader>(filePath + fileName);
        //computeRefrence = (ComputeShader)AssetDatabase.LoadAssetAtPath(_dataPath + _packageName + filePath + fileName, typeof(ComputeShader));
        if (computeRefrence == null)
        {
            Debug.LogError("Missing Compute Shader " + fileName + "! Package may be corrupted!");
            Debug.LogError("Please reimport Package");
            return false;
        }
        return true;
    }
    private bool LoadBuiltinMesh(ref Mesh meshRefrence, string fileName)
    {
        meshRefrence = Resources.GetBuiltinResource<Mesh>(fileName);
        if (meshRefrence == null)
        {
            Debug.LogError("Missing Builtin Resource " + fileName);
            Debug.LogError("Please reimport Builtin Resourcs");
            return false;
        }
        return true;
    }

    public override void Create()
    {
        _initialized = Initialize();
        if (!_initialized) return;

        _pipeline = ((UniversalRenderPipelineAsset)GraphicsSettings.renderPipelineAsset);
        if (!_pipeline.supportsCameraDepthTexture)
        {
            Debug.LogError("Camera Depth Texture not supported on Universal Render Pipeline Asset!");
            Debug.LogError("Please enable Camera Depth Texture to use Volumetric Lighting!");
            return;
        }

        if (_renderPass != null)
        {
            _renderPass.DisposeBuffers();
        }
        
        _shadowSampleMaterial = new Material(_shadowSampleShader);
        _shadowSampleMaterial.hideFlags = HideFlags.DontSave;

        _boxDepthMaterial = new Material(_boxDepthShader);
        _boxDepthMaterial.hideFlags = HideFlags.DontSave;

        _lightVolumeMeshMaterial = new Material(_lightZoneMeshShader);
        _lightVolumeMeshMaterial.enableInstancing = true;
        _lightVolumeMeshMaterial.hideFlags = HideFlags.DontSave;

        _mixDepthMaterial = new Material(_mixDepthShader);
        _mixDepthMaterial.hideFlags = HideFlags.DontSave;

        _atmosphereMaterial = new Material(_atmosphereShader);
        _atmosphereMaterial.hideFlags = HideFlags.DontSave;
        
        _smartSampleMaterial = new Material(_smartSampleShader);
        _smartSampleMaterial.hideFlags = HideFlags.DontSave;

        _compositeMaterial = new Material(_compositeShader);
        _compositeMaterial.hideFlags = HideFlags.DontSave;

        _renderPass = new MeshLightZonesPass(
            _shadowSampleMaterial,
            _boxDepthMaterial,
            _lightVolumeMeshMaterial,
            _mixDepthMaterial,
            _atmosphereMaterial,
            _smartSampleMaterial,
            _compositeMaterial,
            _laplacianCompute,
            _downsampleBufferCompute,
            _lightVolumeCompute,
            _patchTextureCompute,
            _blurCompute,
            _cubeMesh,
            "MeshLightZonePass"
            );
        _renderPass.renderPassEvent = renderPassEvent;

        _initialized = true;
    }
    // called every frame once per camera
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_renderPass != null)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game || (renderInSceneView && renderingData.cameraData.cameraType == CameraType.SceneView))
            {
                if (_initialized)
                {
                    // Gather up and pass any extra information our pass will need. (that might change frame to frame?)
                    if (!_renderPass.Setup(ref renderingData))
                    {
                        _renderPass.DisposeBuffers();
                        return;
                    }
                    _renderPass.InitializeBuffers(ref renderingData);

                    _renderPass.SetTarget(
                        laplacianGaussianStandardDeviation, 
                        laplacianKernelRadius, 
                        maxLaplacianWidth,
                        chunksInWidth, 
                        maxTesselation, 
                        distanceFactor, 
                        downsampleAmount,
                        patchThreshold,
                        blurGaussianStandardDeviation, 
                        blurKernelRadius, 
                        blurDepthFalloff, 
                        extinctionCoefficient, 
                        meiSactteringCoefficient, 
                        rayleighSactteringCoefficient, 
                        maxDepth,
                        fogDensity,
                        fogColor,
                        ambientColor,
                        extinctionColor,
                        lightZones.ToArray()
                        );

                    // Ask the renderer to add our pass.
                    // Could queue up multiple passes and/or pick passes to use
                    renderer.EnqueuePass(_renderPass);
                }
            }
        }
    }
    private void OnDisable()
    {
        //Debug.Log("on disable");
        if(_renderPass != null)
        {
            _renderPass.DisposeBuffers();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_shadowSampleMaterial != null)
        {
            CoreUtils.Destroy(_shadowSampleMaterial);
            _shadowSampleMaterial = null;
        }   
        if (_boxDepthMaterial != null)
        {
            CoreUtils.Destroy(_boxDepthMaterial);
            _boxDepthMaterial = null;
        }
        if(_lightVolumeMeshMaterial != null)
        {
            CoreUtils.Destroy(_lightVolumeMeshMaterial);
            _lightVolumeMeshMaterial = null;
        }
        if (_mixDepthMaterial != null)
        {
            CoreUtils.Destroy(_mixDepthMaterial);
            _mixDepthMaterial = null;
        }
        if (_atmosphereMaterial != null)
        {
            CoreUtils.Destroy(_atmosphereMaterial);
            _atmosphereMaterial = null;
        }
        if (_smartSampleMaterial != null)
        {
            CoreUtils.Destroy(_smartSampleMaterial);
            _smartSampleMaterial = null;
        }
        if(_compositeMaterial != null)
        {
            CoreUtils.Destroy(_compositeMaterial);
            _compositeMaterial = null;
        }
    }
}