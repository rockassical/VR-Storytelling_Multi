using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class PlayerMovement : MonoBehaviour
{

    /*public InputActionReference moveLRAxis; //Left/Right
    public InputActionReference moveFBAxis; //Forward/Backward
    public InputActionReference moveUDAxis; //Up/Down

    public float movementClamping;

    // This script is built to be used with the 'ContinuousTurnProvider', which doesn't take any
    // input from the thumbstick y axis

    //public DynamicMoveProvider moveProvider;    //to override move

    // Start is called before the first frame update
    void Start()
    {
        turnProvider.enabled = false;
        moveProvider.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {

        // ---- Handle vertical movement ----
        Vector2 axis = moveUDAxis.action.ReadValue<Vector2>();
        float inputUD = axis.y;

        if(Math.Abs(inputUD) < 0.2f){
            inputUD = 0f;
        }

        // ---- Handle forward/backward movement ----
        Vector2 axis = moveFBAxis.action.ReadValue<Vector2>();
        float inputFB = axis.y;

        
        if(inputFB < 0.1f){
            inputFB = 0f;
        }
        

        // ---- Handle left/right movement ----
        Vector2 axis = moveLRAxis.action.ReadValue<Vector2>();
        float inputLR = axis.x;

        
        if(inputLR < 0.1f){
            inputLR = 0f;
        }
        

        CreateVerticalMove(inputUD);
    }*/
}
