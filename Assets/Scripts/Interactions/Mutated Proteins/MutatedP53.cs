using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Server-driven enemy that steals an HR DNA piece and runs back to spawn.
// Health and held-piece state replicated via NetworkVariables; transform
// replication relies on a NetworkTransform (or OwnerTransformSync) on this object.
public class MutatedP53 : NetworkBehaviour
{
    public int Health;
    private int MaxHealth;
    public float Speed;

    public GameObject HealthBar;
    public Image HealthBarValue;

    private NetworkVariable<bool> n_hasPiece = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> n_currentHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private GameObject[] DNAPieces;
    [SerializeField] private GameObject heldPiece;
    private Vector3 spawnPos;

    void Start()
    {
        MaxHealth = Health;
        if (IsServer) n_currentHealth.Value = Health;

        spawnPos = transform.position;
        DNAPieces = GameObject.FindGameObjectsWithTag("HR DNA Piece");

        n_currentHealth.OnValueChanged += (oldVal, newVal) => UpdateHealthUI(newVal);
    }

    void Update()
    {
        if (!IsServer) return;

        if (n_hasPiece.Value == false)
        {
            GameObject closestPiece = FindClosestPiece();
            if (closestPiece != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, closestPiece.transform.position, Speed);
                if (Vector3.Distance(transform.position, closestPiece.transform.position) < 0.1f)
                    PickUpPieceServer(closestPiece);
            }
        }
        else if (transform.position != spawnPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, spawnPos, Speed);
        }

        // Pin the held piece to us. Position only — we own it via ownership transfer
        // so OwnerTransformSync broadcasts from the server every frame.
        if (heldPiece != null)
            heldPiece.transform.position = transform.position;
    }

    void PickUpPieceServer(GameObject piece)
    {
        heldPiece = piece;

        // Take ownership so the piece's OwnerTransformSync broadcasts from the
        // server (which is now driving its position via this enemy).
        var pieceNO = piece.GetComponent<NetworkObject>();
        if (pieceNO != null && pieceNO.OwnerClientId != NetworkManager.Singleton.LocalClientId)
            pieceNO.ChangeOwnership(NetworkManager.Singleton.LocalClientId);

        n_hasPiece.Value = true;
        SetXRGrabableClientRpc(false);
    }

    GameObject FindClosestPiece()
    {
        GameObject closest = null;
        float closestDist = float.MaxValue;

        foreach (var p in DNAPieces)
        {
            if (p == null) continue;
            // Skip pieces already held by another enemy (parented under a MutatedP53).
            if (p.transform.parent != null && p.transform.parent.GetComponent<MutatedP53>() != null)
                continue;

            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closestDist) { closestDist = d; closest = p; }
        }
        return closest;
    }

    void UpdateHealthUI(int currentHealth)
    {
        if (HealthBar != null && !HealthBar.activeSelf) HealthBar.SetActive(true);
        if (HealthBarValue != null) HealthBarValue.fillAmount = (float)currentHealth / MaxHealth;
    }

    public void OnTriggerEnter(Collider col)
    {
        if (!IsServer) return;

        if (col.gameObject.CompareTag("LaserBullet"))
        {
            n_currentHealth.Value -= 10;

            // Bullet is a NetworkObject — must Despawn, not Destroy.
            var bulletNO = col.gameObject.GetComponent<NetworkObject>();
            if (bulletNO != null && bulletNO.IsSpawned) bulletNO.Despawn();
            else Destroy(col.gameObject);

            CheckDeath();
        }
    }

    void CheckDeath()
    {
        if (n_currentHealth.Value > 0) return;

        if (heldPiece != null)
        {
            // Re-enable grab and release ownership back to the server's default.
            SetXRGrabableClientRpc(true);
            heldPiece = null;
        }

        n_hasPiece.Value = false;
        GetComponent<NetworkObject>().Despawn();
    }

    [ClientRpc]
    void SetXRGrabableClientRpc(bool value)
    {
        if (heldPiece == null) return;
        var pieceGrab = heldPiece.GetComponent<XRGrabInteractable>();
        if (pieceGrab != null) pieceGrab.enabled = value;
    }
}
