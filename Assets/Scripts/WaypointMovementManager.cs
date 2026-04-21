using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Playables;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;
using SWS;

public class WaypointMovementManager : NetworkBehaviour
{

    /*
        This is meant to be a helper to the game manager specifically for managing path-based movement
    */

    [Header("Spline movement reference")]
    public splineMove p53;      // only need 1, both move equal time

    [Header("Ship Seat References")]
    public GameObject p53Ship;
    public GameObject ATMShip;

    private int movePhase;     // track which phase we are in

    [Header("Game Manager Reference")]
    public GameManager gameManager;

    // Start is called before the first frame update
    void Start()
    {
        movePhase = 0;

        p53.movementEnd.AddListener(OnDestinationReached);
    }

    void OnDestinationReached(){
        movePhase++;

        StartCoroutine(UnboardShipLocal(p53Ship.transform));

        switch(movePhase){
            // Start of HR
            case 1:
              gameManager.playPhase(2);

              break;
            case 2:
                gameManager.playPhase(3);

                break;
            default:
                break;
        }
    }

    IEnumerator UnboardShipLocal(Transform ship)
    {
        Transform xrOrigin = FindFirstObjectByType<XROrigin>().transform;

        // Optional: preserve world pose before parenting (prevents sudden snap bugs)
        Vector3 worldPos = xrOrigin.position;
        Quaternion worldRot = xrOrigin.rotation;

        xrOrigin.SetParent(null, true);
        ship.SetParent(xrOrigin, true);

        yield return null;

        xrOrigin.gameObject.GetComponentInChildren<DynamicMoveProvider>().enabled = true;

        // Snap cleanly into seat
        xrOrigin.position = worldPos;
        xrOrigin.rotation = worldRot;
    }
}
