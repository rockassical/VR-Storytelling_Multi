using Unity.Netcode;
using UnityEngine;

// An interactable cut point on DNA ends that Detects tool activation and reports trimming to server
public class TrimPoint : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] int pointIndex;
    [SerializeField] int assignedPlayerRole = 1; // 1 = player1, 2 = player2

    [Header("References")]
    [SerializeField] GameObject nucleotideFragment;
    [SerializeField] GameObject cutLineIndicator;

    [Header("Effects")]
    [SerializeField] float fragmentDriftSpeed = 0.5f;
    [SerializeField] float fragmentLifetime = 3f;

    [Header("Placement Indicator")]
    [SerializeField] Material indicatorMaterial;
    [SerializeField] float indicatorRadius = 0.12f;

    bool isTrimmed;
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

        indicatorSphere.SetActive(false);
    }

    public void SetIndicatorVisible(bool visible)
    {
        if (indicatorSphere != null)
            indicatorSphere.SetActive(visible);
    }

    public int PointIndex => pointIndex;
    public int AssignedPlayerRole => assignedPlayerRole;
    public bool IsTrimmed => isTrimmed;

    public void OnToolActivated(ulong clientId)
    {
        if (isTrimmed) return;
        if (NHEJManager.Instance == null) return;
        if (NHEJManager.Instance.CurrentPhase != NHEJPhase.Phase3_Trimming) return;

        // In single-player debug mode both roles belong to the local client,
        // so skip the role check and let the server use the point's assignedPlayerRole.
        if (!NHEJManager.Instance.DebugBypass)
        {
            int role = NHEJManager.Instance.GetPlayerRole(clientId);
            if (role != assignedPlayerRole)
            {
                Debug.Log($"[NHEJ] TrimPoint {pointIndex}: wrong player role {role}, expected {assignedPlayerRole}");
                return;
            }
        }

        // DISABLED — ReportTrimServerRpc replaced by ArtemisBlade/ReportCutServerRpc.
        // NHEJManager.Instance.ReportTrimServerRpc(pointIndex, clientId, assignedPlayerRole);
    }

    public void PerformTrim()
    {
        if (isTrimmed) return;
        isTrimmed = true;

        if (cutLineIndicator != null)
            cutLineIndicator.SetActive(false);

        if (nucleotideFragment != null)
        {
            nucleotideFragment.transform.SetParent(null);
            var rb = nucleotideFragment.GetComponent<Rigidbody>();
            if (rb == null)
                rb = nucleotideFragment.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.velocity = (Vector3.up + Random.insideUnitSphere).normalized * fragmentDriftSpeed;
            Destroy(nucleotideFragment, fragmentLifetime);
        }

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;
    }
}
