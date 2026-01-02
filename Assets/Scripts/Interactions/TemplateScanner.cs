using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

public class GunInputXR : MonoBehaviour
{
    [SerializeField] public GameObject ScanPulse;
    [SerializeField] XRGrabInteractable grab;

    [Header("Input")]
    [SerializeField] InputActionProperty scanAction;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        scanAction.action.Enable();
    }

    void OnDisable()
    {
        scanAction.action.Disable();
    }

    void Update()
    {
        if (!grab.isSelected)
            return;

        if (scanAction.action.WasPressedThisFrame())
        {
            Scan();
        }
    }


    void Scan(){
        ScanPulse.SetActive(true);
    }

}
