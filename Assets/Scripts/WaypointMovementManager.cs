using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using SWS;

public class WaypointMovementManager : NetworkBehaviour
{
    [Header("Spline movement reference")]
    public splineMove p53;      // server drives; NetworkTransform replicates to clients

    [Header("Ship Seat References")]
    public GameObject p53Ship;
    public GameObject ATMShip;

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
                UnboardShipsClientRpc();
                gameManager.playPhase(2);
                break;
            case 2:
                UnboardShipsClientRpc();
                gameManager.playPhase(3);
                break;
        }
    }

    // Fires on all clients so each player unboards their own ship locally.
    [ClientRpc]
    void UnboardShipsClientRpc()
    {
        Transform ship = IsServer ? p53Ship.transform : ATMShip.transform;
        StartCoroutine(UnboardShipLocal(ship));
    }

    IEnumerator UnboardShipLocal(Transform ship)
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null) yield break;

        Transform t = xrOrigin.transform;
        Vector3 worldPos    = t.position;
        Quaternion worldRot = t.rotation;

        t.SetParent(null, true);
        ship.SetParent(t, true);

        yield return null;

        var move = t.GetComponentInChildren<DynamicMoveProvider>();
        if (move != null) move.enabled = true;

        t.localScale = new Vector3(1f, 1f, 1f);

        t.position = worldPos;
        t.rotation = worldRot;
    }
}
