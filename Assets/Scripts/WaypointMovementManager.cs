using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SWS;

public class WaypointMovementManager : MonoBehaviour
{

    /*
        This is meant to be a helper to the game manager specifically for managing path-based movement
    */

    [Header("Spline movement reference")]
    public splineMove p53;      // only need 1, both move equal time

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

        switch(movePhase){
            // Start of HR
            case 1:
              gameManager.playPhase(2);

              break;
            default:
                break;
        }
    }
}
