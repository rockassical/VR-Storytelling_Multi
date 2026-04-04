using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProteinMenuUI : MonoBehaviour
{
    Canvas canvas;
    Camera camera;

    List<GameObject> DNA_Pieces = new List<GameObject>();
    void Start()
    {
        canvas = GetComponent<Canvas>();
        camera = Camera.main;
    }
    void OpenMenu()
    {

    }
    void FixedUpdate()
    {
        
    }

    public void Buttons(int b) 
    {
        Debug.Log(("Button Pressed"));
    }
}
