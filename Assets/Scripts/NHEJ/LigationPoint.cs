using Unity.Netcode;
using UnityEngine;

// An interactable nick point on a DNA strand. Detects ligase tool hit and reports ligation to server
public class LigationPoint : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] int pointIndex;
    [SerializeField] int assignedPlayerRole = 1; // 1 = player1 , 2 = player2 

    [Header("References")]
    [SerializeField] GameObject nickIndicator;
    [SerializeField] GameObject sealedVisual;

    [Header("Effects")]
    [SerializeField] ParticleSystem atpParticles;
    [SerializeField] float sealFlashDuration = 0.5f;

    bool isSealed;

    public int PointIndex => pointIndex;
    public int AssignedPlayerRole => assignedPlayerRole;
    public bool IsSealed => isSealed;


    public void OnToolActivated(ulong clientId)
    {
        if (isSealed) return;
        if (NHEJManager.Instance == null) return;
        if (NHEJManager.Instance.CurrentPhase != NHEJPhase.Phase6_Ligation) return;

        int role = NHEJManager.Instance.GetPlayerRole(clientId);
        if (role != assignedPlayerRole)
        {
            Debug.Log($"[NHEJ] LigationPoint {pointIndex}: wrong player role {role}, expected {assignedPlayerRole}");
            return;
        }

        NHEJManager.Instance.ReportLigationServerRpc(pointIndex, clientId);
    }

    // Called on all clients when server confirms the ligation.
    public void PerformSeal()
    {
        if (isSealed) return;
        isSealed = true;

        if (atpParticles != null)
            atpParticles.Play();

        if (nickIndicator != null)
            nickIndicator.SetActive(false);

        if (sealedVisual != null)
            sealedVisual.SetActive(true);

        // Disable collider
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Disable glow
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;
    }
}
