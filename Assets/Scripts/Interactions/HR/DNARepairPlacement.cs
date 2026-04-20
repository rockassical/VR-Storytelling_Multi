using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.Playables;
using Unity.Netcode;

public class DNARepairPlacement : NetworkBehaviour
{
    [Header("NEXT TIMELINE")]
    public PlayableDirector Timeline_NHEJ;

    [Header("GAME MANAGER")]
    public GameManager GM;
    public MutatedProteinSpawner spawner;

    // Netcode additions
    // assign all TemplatePiece scene objects here so the server can track when all pieces have been scanned and activate sockets on every client
    // I did not do this in the inspector yet
    [Header("TEMPLATE PIECES (Netcode)")]
    public TemplatePiece[] TemplatePieces;

    /*
        - Declare the number of pieces that need to be placed for strand to be repaired
        - Check to see if strand has been repaired
        - Hide hologram when repaired
    */

    [Header("REPAIR SOCKETS")]
    public GameObject[] Sockets;
    private bool[] IsSocketFilled;

    private bool isRepaired;

    // bitmask tracking which sockets are filled, server-authoritative
    // Each bit corresponds to a socket index
    // Replicates to all clients
    readonly NetworkVariable<int> filledSocketMask = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Start is called before the first frame update
    void Start()
    {
        isRepaired = false;
        IsSocketFilled = new bool[Sockets.Length];

        for(int i = 0; i < Sockets.Length; i++){
            int index = i;
            Sockets[i].GetComponent<XRSocketInteractor>().selectEntered.AddListener((SelectEnterEventArgs args) => FillSocket(index, args));
        }
    }

    // subscribe to each TemplatePiece scan event so the server can ctivate sockets on all clients once everything is scanned
    public override void OnNetworkSpawn()
    {
        if (TemplatePieces != null)
            foreach (var tp in TemplatePieces)
                if (tp != null) tp.OnPieceScanned += OnAnyPieceScanned;
    }

    // clean up scan event subscriptions
    public override void OnNetworkDespawn()
    {
        if (TemplatePieces != null)
            foreach (var tp in TemplatePieces)
                if (tp != null) tp.OnPieceScanned -= OnAnyPieceScanned;
    }

    // called on ALL clients when any piece is scanned (via TemplatePiece
    // Only the server counts and broadcasts the socket activation
    void OnAnyPieceScanned()
    {
        if (!IsServer || TemplatePieces == null) return;
        int scanned = 0;
        foreach (var tp in TemplatePieces)
            if (tp != null && tp.IsScanned) scanned++;
        if (scanned >= TemplatePieces.Length)
            ActivateSocketsClientRpc();
    }

    // server tells every client to show the hologram sockets.
    [ClientRpc]
    void ActivateSocketsClientRpc()
    {
        foreach (var socket in Sockets)
            if (socket != null) socket.SetActive(true);
    }

   // Process the socket whenever it is filled -> stop from grabbing piece and hide socket
   public void FillSocket(int index, SelectEnterEventArgs args){
        IsSocketFilled[index] = true;

        var Socket = Sockets[index];
        var Piece = args.interactableObject.transform.gameObject;
        Piece.tag = "Untagged";

        // route the fill through the server so both players see the socket disappear and the piece lock in
        // Original coroutine is now called via FillSocketVisualClientRpc on all clients instead of locally
        if (IsSpawned)
        {
            ulong pieceId = Piece.GetComponent<NetworkObject>()?.NetworkObjectId ?? ulong.MaxValue;
            FillSocketServerRpc(index, pieceId);
        }
        else
        {
            // Fallback for non-networked testing
            StartCoroutine(WaitAndFillSocket(Socket, Piece));
        }
   }

    // server validates the fill, updates bitmask, and broadcasts visuals.
    [ServerRpc(RequireOwnership = false)]
    void FillSocketServerRpc(int index, ulong pieceNetId)
    {
        int bit = 1 << index;
        if ((filledSocketMask.Value & bit) != 0) return; // already filled
        filledSocketMask.Value |= bit;

        FillSocketVisualClientRpc(index, pieceNetId);

        int fullMask = (1 << Sockets.Length) - 1;
        if ((filledSocketMask.Value & fullMask) == fullMask)
            AllSocketsFilledClientRpc();
    }

    //  runs on every client - hides the socket and locks the piece
    [ClientRpc]
    void FillSocketVisualClientRpc(int index, ulong pieceNetId)
    {
        GameObject piece = null;
        if (pieceNetId != ulong.MaxValue &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pieceNetId, out var no))
            piece = no.gameObject;

        if (index < Sockets.Length)
            StartCoroutine(WaitAndFillSocket(Sockets[index], piece));
    }

    // fires on every client when all sockets are filled.
    [ClientRpc]
    void AllSocketsFilledClientRpc()
    {
        Debug.Log("ALL SOCKETS ARE FILLED! TIME FOR NHEJ!");
        spawner.DespawnAll();
        spawner.isActive = false;
        GM.playPhase(2);
    }

   // Wait for the piece to click into place
   IEnumerator WaitAndFillSocket(GameObject Socket, GameObject Piece){
       yield return new WaitForSeconds(0.5f);

       if (Socket != null) Socket.SetActive(false);
       if (Piece != null)
       {
           var grab = Piece.GetComponent<XRGrabInteractable>();
           if (grab != null) grab.enabled = false;
       }

       CheckIfSocketsAreFilled();
   }

   void CheckIfSocketsAreFilled(){
       bool AllFilled = true;

       for(int i = 0; i < IsSocketFilled.Length; i++){
           if(!IsSocketFilled[i]){
               AllFilled = false;
           }
       }

       if(AllFilled){
           Debug.Log("ALL SOCKETS ARE FILLED! TIME FOR NHEJ!");
            // ACTIVATE TIMELINE --> MOVE TO NEXT PART
            //Timeline_NHEJ.Play();

            GM.playPhase(2);

       }
   }
}
