using System;
using Unity.Netcode;
using UnityEngine;

public class TemplatePiece : NetworkBehaviour 
{
    public Material UnscannedMat;
    public Material ScannedMatOutsideBars;
    public Material ScannedMatInsideBars;

    public GameObject OutsidePiece;
    public GameObject InsidePiece;

    public bool scanned = false;

    // Netcode addition
    // Fires on ALL clients when this piece is scanned through OnValueChanged 
    // DNARepairPlacement subscribes to this to know when to activate sockets server-side
    public event Action OnPieceScanned;

    // server-authoritative scan state, replicated to all clients
    readonly NetworkVariable<bool> netScanned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // read the networked state instead of the local bool.
    public bool IsScanned => netScanned.Value;

    // Start is called before the first frame update
    void Start()
    {
        OutsidePiece.GetComponent<MeshRenderer>().material = UnscannedMat;
        InsidePiece.GetComponent<MeshRenderer>().material  = UnscannedMat;
    }

    // hook up the NetworkVariable listener on spawn
    public override void OnNetworkSpawn()
    {
        netScanned.OnValueChanged += OnNetScannedChanged;
        if (netScanned.Value) ApplyScannedMaterials(); // catch late-joiners
    }

    // clean up listener on despawn.
    public override void OnNetworkDespawn()
    {
        netScanned.OnValueChanged -= OnNetScannedChanged;
    }

    // runs on every client when server sets netScanned = true.
    void OnNetScannedChanged(bool _, bool curr)
    {
        if (curr)
        {
            ApplyScannedMaterials();
            OnPieceScanned?.Invoke();
        }
    }

    public void ScanPiece()
    {
        scanned = true;
        if (IsSpawned)
        {
            // route through server so all clients see the scan.
            ScanPieceServerRpc();
        }
        else
        {
            // Fallback for non-networked testing
            ApplyScannedMaterials();
        }
    }

    // server validates and broadcasts the scan state.
    [ServerRpc(RequireOwnership = false)]
    void ScanPieceServerRpc()
    {
        if (!netScanned.Value) netScanned.Value = true;
    }

    void ApplyScannedMaterials()
    {
        OutsidePiece.GetComponent<MeshRenderer>().material = ScannedMatOutsideBars;
        InsidePiece.GetComponent<MeshRenderer>().material  = ScannedMatInsideBars;
    }
}
