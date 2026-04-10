using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProteinMenuUI : MonoBehaviour
{
    public Transform GO_spawnPos;
    [SerializeField] List<GameObject> DNA_Pieces = new List<GameObject>();

    public Canvas canvas;
    bool canvasActive = false;

    public InputActionProperty buttonAction;

    private void OnEnable()
    {
        buttonAction.action.Enable();
    }

    private void OnDisable()
    {
        buttonAction.action.Disable();
    }
    void FixedUpdate()
    {
        if (buttonAction.action.WasPressedThisFrame())
        {
            Menu();
        }
    }

    void Menu()
    {
        canvasActive = !canvasActive;
        canvas.enabled = canvasActive;
    }

    public void Buttons(int b) 
    {
        Debug.Log(("Button Pressed"));

        if(DNA_Pieces.Count > 0)
            switch(b)
            {
                case 1:
                    Instantiate(DNA_Pieces[0], GO_spawnPos);
                    break;
            }


    }
}
