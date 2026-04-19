using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProteinMenuUI : MonoBehaviour
{
    public Transform GO_spawnPos;
    [SerializeField] List<GameObject> DNA_Pieces = new List<GameObject>();

    public GameObject Scanner, Blaster, Artimis, Ligase, DNA_Menu;

    public Canvas canvas;
    bool canvasActive = false;
    bool DNA_MenuActive = false;

    public InputActionProperty buttonAction;

    private void OnEnable()
    {
        buttonAction.action.Enable();
    }

    private void OnDisable()
    {
        buttonAction.action.Disable();
    }

    private void Awake()
    {
        canvas.enabled = canvasActive;
    }
    void Update()
    {
        if (buttonAction.action.WasPressedThisFrame())
        {
            Debug.Log("Hit B");
            if(DNA_MenuActive)
            {
                setDNA_menuActive();
            }
            else Menu();
        }
    }

    void Menu()
    {
        canvasActive = !canvasActive;
        canvas.enabled = canvasActive;
    }

    void setDNA_menuActive()
    {
        DNA_MenuActive = !DNA_MenuActive;
        DNA_Menu.SetActive(DNA_MenuActive);
    }

    public void Buttons(int b) 
    {
        //Debug.Log(("Button Pressed"));

  
        switch(b)
        {
            case 2:
                Scanner.transform.position = GO_spawnPos.position;
                Menu();
                break;
            case 4:
                Artimis.transform.position = GO_spawnPos.position;
                Menu();
                break;
            case 5:
                Ligase.transform.position = GO_spawnPos.position;
                Menu();
                break;
            case 3:
                //Debug.Log("Blaster Filler");
                Blaster.transform.position = GO_spawnPos.position;
                break;

            case 6:
                if (DNA_Pieces.Count > 0)
                    setDNA_menuActive();
                else Debug.Log("DNA List is Empty");
                break;
            case 7:
                if (DNA_Pieces[0] != null)
                    Instantiate(DNA_Pieces[0], GO_spawnPos.position, Quaternion.identity);
                else Debug.Log("Null");
                Menu();
                break;
            case 8:
                if (DNA_Pieces[1] != null)
                    Instantiate(DNA_Pieces[1], GO_spawnPos.position, Quaternion.identity);
                else Debug.Log("Null");
                Menu();
                break;
            case 9:
                if (DNA_Pieces[2] != null)
                    Instantiate(DNA_Pieces[2], GO_spawnPos.position, Quaternion.identity);
                else Debug.Log("Null");
                Menu();
                break;
            case 10:
                if (DNA_Pieces[3] != null)
                    Instantiate(DNA_Pieces[3], GO_spawnPos.position, Quaternion.identity);
                else Debug.Log("Null");
                Menu();
                break;
            case 11:
                if (DNA_Pieces[4] != null)
                    Instantiate(DNA_Pieces[4], GO_spawnPos.position, Quaternion.identity);
                else Debug.Log("Null");
                Menu();
                break;
        }


    }
}
