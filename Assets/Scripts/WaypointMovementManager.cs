using UnityEngine;
using Unity.Netcode;
using SWS;

public class WaypointMovementManager : NetworkBehaviour
{
    [Header("Spline movement reference")]
    public splineMove p53;      // server drives; NetworkTransform replicates to clients

    [Header("Game Manager Reference")]
    public GameManager gameManager;

    private int movePhase;

    void Start()
    {
        movePhase = 0;
        p53.movementEnd.AddListener(OnDestinationReached);
    }

    void OnDestinationReached()
    {

        if (p53 != null)
        {
            p53.enabled = false;
            Debug.Log($"[Waypoint] Disabled splineMove p53 (IsServer={IsServer}).");
        }

        if (!IsServer) return;

        movePhase++;

        switch (movePhase)
        {
            case 1:
                InvertShipParentingClientRpc();
                gameManager.playPhase(2);
                break;
            case 2:
                InvertShipParentingClientRpc();
                gameManager.playPhase(3);
                break;
        }
    }

    // Fires on all clients so each player flips their own ship hierarchy locally.
    [ClientRpc]
    void InvertShipParentingClientRpc()
    {
        gameManager.InvertShipParenting();
    }
}
