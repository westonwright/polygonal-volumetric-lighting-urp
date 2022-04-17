using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System;

public class MeshLightZonesRenderFeature : ScriptableRendererFeature
{

    #region Public properties and methods
    public RenderPassEvent renderPassEvent
    {
        get { return _renderPassEvent; }
        set { _renderPassEvent = value; }
    }
    [SerializeField]
    RenderPassEvent _renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

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
    [Range(1, 64)]
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
    float _maxDepth = 20f;
    
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

    public void AddLightZone(LightZone lightZone)
    {
        if (!lightZones.Contains(lightZone))
        {
            lightZones.Add(lightZone);
        }
    }

    public void RemoveLightZone(LightZone lightZone)
    {
        if (lightZones.Contains(lightZone))
        {
            lightZones.Remove(lightZone);
        }
    }

    #endregion

    #region Private properties

    [SerializeField]
    Shader _shadowSampleShader;
    [SerializeField]
    Shader _boxDepthShader;
    [SerializeField]
    Shader _lightVolumeMeshShader;
    [SerializeField]
    Shader _atmosphereShader;
    [SerializeField]
    Shader _compositeShader;

    Material _shadowSampleMaterial;
    Material _boxDepthMaterial;
    Material _lightVolumeMeshMaterial;
    Material _atmosphereMaterial;
    Material _compositeMaterial;

    [SerializeField]
    ComputeShader _laplacianCompute;
    [SerializeField]
    ComputeShader _downsampleCompute;
    [SerializeField]
    ComputeShader _lightVolumeCompute;
    [SerializeField]
    ComputeShader _blurCompute;
    [SerializeField]
    ComputeShader _upsampleCompute;

    [SerializeField]
    Mesh _cubeMesh;

    List<LightZone> lightZones = new List<LightZone>();

    MeshLightZonesPass _renderPass = null;

    bool _initialized = false;
    #endregion

    public override void Create()
    {
        if(
            _shadowSampleShader == null ||
            _boxDepthShader == null ||
            _lightVolumeMeshShader == null ||
            _atmosphereShader == null ||
            _compositeShader == null || 
            _laplacianCompute == null || 
            _downsampleCompute == null ||
            _lightVolumeCompute == null ||
            _blurCompute == null ||
            _upsampleCompute == null ||
            _cubeMesh == null)
        {
            _initialized = false;
            return;
        }

        if(_renderPass != null)
        {
            _renderPass.DisposeBuffers();
        }
        
        _shadowSampleMaterial = new Material(_shadowSampleShader);
        _shadowSampleMaterial.hideFlags = HideFlags.DontSave;

        _boxDepthMaterial = new Material(_boxDepthShader);
        _boxDepthMaterial.hideFlags = HideFlags.DontSave;

        _lightVolumeMeshMaterial = new Material(_lightVolumeMeshShader);
        _lightVolumeMeshMaterial.enableInstancing = true;
        _lightVolumeMeshMaterial.hideFlags = HideFlags.DontSave;

        _atmosphereMaterial = new Material(_atmosphereShader);
        _atmosphereMaterial.hideFlags = HideFlags.DontSave;

        _compositeMaterial = new Material(_compositeShader);
        _compositeMaterial.hideFlags = HideFlags.DontSave;

        _renderPass = new MeshLightZonesPass(
            _shadowSampleMaterial,
            _boxDepthMaterial,
            _lightVolumeMeshMaterial,
            _atmosphereMaterial,
            _compositeMaterial,
            _laplacianCompute,
            _downsampleCompute,
            _lightVolumeCompute,
            _blurCompute,
            _upsampleCompute,
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
            if (renderingData.cameraData.cameraType == CameraType.Game)
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

        _renderPass.DisposeBuffers();
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
        
        if(_atmosphereMaterial != null)
        {
            CoreUtils.Destroy(_atmosphereMaterial);
            _atmosphereMaterial = null;
        }
        if(_compositeMaterial != null)
        {
            CoreUtils.Destroy(_compositeMaterial);
            _compositeMaterial = null;
        }
    }
}