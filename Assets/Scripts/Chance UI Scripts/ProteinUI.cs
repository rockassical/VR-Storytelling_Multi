using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ProteinUI : MonoBehaviour
{
    XRGrabInteractable interactable;
    Canvas canvas;
    TextMeshProUGUI textbox;
    Transform mainCamPos;

    private void Start()
    {
        mainCamPos = Camera.main.transform;
        canvas = GetComponentInChildren<Canvas>();
        textbox = canvas.GetComponentInChildren<TextMeshProUGUI>();

        textbox.text = gameObject.name;

        TurnOff();
    }

    public void RotateUI()
    {
        canvas.transform.LookAt(canvas.transform.position + mainCamPos.forward);
    }
    private void LateUpdate()
    {
        // If the UI is active, keep it facing the user
        if (canvas.gameObject.activeSelf && mainCamPos != null)
        {
            RotateUI();
        }
    }
    public void TurnOn()
    {
        canvas.gameObject.SetActive(true);
    }

    public void TurnOff()
    {
        canvas.gameObject.SetActive(false);
    }
}
