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
        // Only the server drives phase progression — prevents duplicate calls
        // if SWS fires the event on multiple clients.
        if (!IsServer) return;

        movePhase++;

        switch (movePhase)
        {
            case 1:
                gameManager.playPhase(2);
                break;
            case 2:
                gameManager.playPhase(3);
                break;
        }
    }
}
