using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

public class LaserBlaster : MonoBehaviour
{
    [SerializeField] public GameObject LaserBullet;
    [SerializeField] public Transform BulletSpawn;
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
            Shoot();
        }
    }


    void Shoot(){
        Instantiate(LaserBullet, BulletSpawn.position, gameObject.transform.rotation);
    }

}
