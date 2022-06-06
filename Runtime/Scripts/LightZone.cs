using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Linq;

[ExecuteAlways]
public class LightZone : MonoBehaviour
{
    [SerializeField]
    private Vector3 Offset = Vector3.zero;
    public Vector3 offset { get { return Offset; } }
    [SerializeField]
    private Vector3 Scale = Vector3.one;
    public Vector3 scale { get { return Scale; } }

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
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
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

    private void OnEnable()
    {
        Initialize();
        UpdateBounds();

        ExtractRenderFeature();

        if (meshLightZonesRenderFeature != null)
        {
            meshLightZonesRenderFeature.AddLightZone(this);
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
        Vector3 halfScale = Scale / 2.0f;
        zoneBounds = new Bounds(Offset, Scale);
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        Vector3[] points = new Vector3[]
        {
            Offset + new Vector3(halfScale.x, halfScale.y, halfScale.z),
            Offset + new Vector3(-halfScale.x, halfScale.y, halfScale.z),
            Offset + new Vector3(halfScale.x, -halfScale.y, halfScale.z),
            Offset + new Vector3(halfScale.x, halfScale.y, -halfScale.z),
            Offset + new Vector3(-halfScale.x, -halfScale.y, halfScale.z),
            Offset + new Vector3(halfScale.x, -halfScale.y, -halfScale.z),
            Offset + new Vector3(-halfScale.x, halfScale.y, -halfScale.z),
            Offset + new Vector3(-halfScale.x, -halfScale.y, -halfScale.z),
        };
        for(int i = 0; i < 8; i++)
        {
            points[i] = transform.TransformPoint(points[i]);
        }
        trueBounds = GeometryUtility.CalculateBounds(points, Matrix4x4.identity);
        //trueBounds = new Bounds(transform.position + offset, scale);
    }

    public Bounds? GetBounds()
    {
        if(trueBounds == null)
        {
            UpdateBounds();
        }
        return trueBounds;
    }
}