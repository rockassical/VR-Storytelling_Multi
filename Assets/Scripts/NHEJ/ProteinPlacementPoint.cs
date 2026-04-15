using UnityEngine;

public class ProteinPlacementPoint : MonoBehaviour
{
    [Tooltip("Which player role this target is for (1 = P1/p53, 2 = P2/ATM)")]
    public int playerRole = 1;

    [Tooltip("Translucent material for the runtime placement indicator sphere. Assign a URP/Standard transparent material in the inspector.")]
    [SerializeField] Material indicatorMaterial;

    [Tooltip("Fallback radius if no SphereCollider is attached. Should match ProteinOrbitController.snapRadius.")]
    [SerializeField] float indicatorRadius = 0.06f;

    GameObject indicatorSphere;

    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        float radius = col != null ? col.radius : indicatorRadius;

        indicatorSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicatorSphere.name = "PlacementIndicator";
        indicatorSphere.transform.SetParent(transform, false);
        indicatorSphere.transform.localPosition = Vector3.zero;
        indicatorSphere.transform.localScale = Vector3.one * radius * 2f;

        Destroy(indicatorSphere.GetComponent<Collider>());

        if (indicatorMaterial != null)
            indicatorSphere.GetComponent<MeshRenderer>().material = indicatorMaterial;
    }
    public void SetIndicatorVisible(bool visible)
    {
        if (indicatorSphere != null)
            indicatorSphere.SetActive(visible);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = playerRole == 1
            ? new Color(0.2f, 0.6f, 1f, 0.55f)
            : new Color(0.2f, 1f, 0.4f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = playerRole == 1 ? Color.cyan : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.20f);
    }
}
