using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LightVolumeZone : MonoBehaviour
{

#if UNITY_EDITOR
    [MenuItem("GameObject/Light/Light Volume Zone")]
    static void CreateZone(MenuCommand menuCommand)
    {
        GameObject g = new GameObject("Light Volume Zone", typeof(LightVolumeZone));
        GameObjectUtility.SetParentAndAlign(g, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(g, "Create " + g.name);
        Selection.activeObject = g;
    }
#endif

    //if gizmos should be drawn
    [SerializeField]
    [Tooltip("If Gizmos should be drawn for this source")]
    private bool preview = true;
    [SerializeField]
    private Color previewColor = Color.red;

    [SerializeField]
    private Vector3 Offset = Vector3.zero;
    public Vector3 offset { get { return Offset; } }
    [SerializeField]
    private Vector3 Size = Vector3.one;
    public Vector3 size { get { return Size; } }

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
            Gizmos.color = previewColor;
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
        if(transform.GetComponents<LightVolumeZone>().Length > 1)
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
        Vector3 halfScale = Size / 2.0f;
        zoneBounds = new Bounds(Offset, Size);
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