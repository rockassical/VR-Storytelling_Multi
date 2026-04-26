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
        // Unsubscribe + Stop so SWS / DOTween can't double-fire.
        if (p53 != null)
        {
            p53.movementEnd.RemoveListener(OnDestinationReached);
            p53.Stop();
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

    /// <summary>Re-arm the spline before starting the next leg.</summary>
    public void ArmNextLeg()
    {
        if (p53 == null) return;
        p53.movementEnd.RemoveListener(OnDestinationReached);
        p53.movementEnd.AddListener(OnDestinationReached);
    }

    // Fires on all clients so each player flips their own ship hierarchy locally.
    [ClientRpc]
    void InvertShipParentingClientRpc()
    {
        gameManager.InvertShipParenting();
    }
}
