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

    bool isTrimmed;

    public int PointIndex => pointIndex;
    public int AssignedPlayerRole => assignedPlayerRole;
    public bool IsTrimmed => isTrimmed;

    public void OnToolActivated(ulong clientId)
    {
        if (isTrimmed) return;
        if (NHEJManager.Instance == null) return;
        if (NHEJManager.Instance.CurrentPhase != NHEJPhase.Phase3_Trimming) return;

        int role = NHEJManager.Instance.GetPlayerRole(clientId);
        if (role != assignedPlayerRole)
        {
            Debug.Log($"[NHEJ] TrimPoint {pointIndex}: wrong player role {role}, expected {assignedPlayerRole}");
            return;
        }

        NHEJManager.Instance.ReportTrimServerRpc(pointIndex, clientId);
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
