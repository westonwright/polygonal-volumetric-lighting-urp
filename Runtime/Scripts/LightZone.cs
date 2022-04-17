using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Linq;

//[ExecuteAlways]
public class LightZone : MonoBehaviour
{
    [SerializeField]
    private Vector3 offset = Vector3.zero;
    [SerializeField]
    private Vector3 scale = Vector3.one;

    //if gizmos should be drawn
    [SerializeField]
    [Tooltip("If Gizmos should be drawn for this source")]
    private bool preview = true;

    private Color GizmoColor = Color.red;

    private Bounds? zoneBounds = null;
    private Bounds? trueBounds = null;

    MeshLightZonesRenderFeature meshLightZonesRenderFeature;

    private void OnDrawGizmosSelected()
    {
        UpdateBounds();
    }

    private void OnDrawGizmos()
    {        
        if (preview)
        {
            if (zoneBounds == null)
            {
                UpdateBounds();
            }
            //BoxCollider col = GetComponent<BoxCollider>();
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = GizmoColor;
            //Gizmos.DrawWireCube(HelperFunctions.MultiplyVectors(transform.localScale, col.center), HelperFunctions.MultiplyVectors(transform.localScale, col.size));
            Gizmos.DrawWireCube(zoneBounds.Value.center, zoneBounds.Value.size);
        }
    }
    
    //use to remove from gravity manager
    private void OnDisable()
    {
        if (meshLightZonesRenderFeature != null)
        {
            meshLightZonesRenderFeature.RemoveLightZone(this);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Initialize();
        if (Application.isPlaying)
        {
            UpdateBounds();

            ExtractRenderFeature();

            if(meshLightZonesRenderFeature != null)
            {
                meshLightZonesRenderFeature.AddLightZone(this);
            }
        }
    }

    private void ExtractRenderFeature()
    {
        var pipeline = ((UniversalRenderPipelineAsset)GraphicsSettings.renderPipelineAsset);
        FieldInfo propertyInfo = pipeline.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        ScriptableRendererData universalRendererData = ((ScriptableRendererData[])propertyInfo?.GetValue(pipeline))?[0];
        meshLightZonesRenderFeature = universalRendererData.rendererFeatures.OfType<MeshLightZonesRenderFeature>().FirstOrDefault();
    }

    public void Initialize()
    {
        if(transform.GetComponents<LightZone>().Length > 1)
        {
            this.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            Debug.LogError("Object alreay has a \"LightZone\" Component");
            Debug.LogWarning("Destroyed extra \"EffectZone\" Component");
            DestroyImmediate(this);
        }
        //prevents this from being added to objects

    }

    public void UpdateBounds()
    {   
        zoneBounds = new Bounds(offset, scale);
        trueBounds = new Bounds(transform.position + offset, scale);
    }

    public Bounds? GetBounds()
    {
        return trueBounds;
    }
}