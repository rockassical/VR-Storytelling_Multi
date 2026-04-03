using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

public class GunInputXR : MonoBehaviour
{
    [SerializeField] public GameObject ScanPulse;
    [SerializeField] XRGrabInteractable grab;

    [Header("Input")]
    [SerializeField] InputActionProperty scanAction;

    [Header("Socket Interaction Objects")]
    public GameObject[] Sockets;

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
        else if(scanAction.action.WasReleasedThisFrame())
        {
            UnScan();
        }
    }


    void Scan(){
        ScanPulse.SetActive(true);
    }

    void UnScan(){
        ScanPulse.SetActive(false);
    }

    public void OnTriggerEnter(Collider col){
        if(col.gameObject.tag.Equals("Scannable")){

            Debug.Log("TEMPLATE SCANNED");

            foreach(GameObject Socket in Sockets){
                Socket.SetActive(true);
            }
        }
    }

}
