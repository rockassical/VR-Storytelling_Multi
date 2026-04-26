using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Playables;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class GameManager : NetworkBehaviour
{
    public int maxPlayers = 2;

    [Header("UI Elements")]
    public GameObject StartGameUI;
    public Button StartGameButton, ApoptosisButton;

    [Header("Timelines")]
    public PlayableDirector Timeline_Intro;
    public PlayableDirector Timeline_HR, Timeline_NHEJ, Timeline_Apoptosis, Timeline_Conclusion;

    [Header("Ships to Board")]
    [Tooltip("Seat transform — the player gets parented to this to sit in the ship.")]
    public GameObject P53Ship;
    [Tooltip("Seat transform — the player gets parented to this to sit in the ship.")]
    public GameObject ATMShip;

    [Header("Ship Roots (for inversion)")]
    [Tooltip("Actual ship root (typically the parent of the seat). This is what becomes a child of the player after the spline ride.")]
    public GameObject P53ShipRoot;
    [Tooltip("Actual ship root (typically the parent of the seat). This is what becomes a child of the player after the spline ride.")]
    public GameObject ATMShipRoot;

    void Start()
    {
        StartGameButton.onClick.AddListener(() => StartGame());
        ApoptosisButton.onClick.AddListener(() => playPhase(5));
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log("PLAYER CONNECTED! (Current Players: " + NetworkManager.Singleton.ConnectedClientsList.Count + ")");

        if (NetworkManager.Singleton.ConnectedClientsList.Count >= maxPlayers)
            ShowStartUIClientRpc();
    }

    // Show the start button on the server only — host decides when to begin.
    [ClientRpc]
    void ShowStartUIClientRpc()
    {
        if (IsServer) StartGameUI.SetActive(true);
    }

    void StartGame()
    {
        if (!IsServer) return;
        playPhase(1);
    }

    // ── Timeline playback — always runs on ALL clients ────────────────────────

    /// <summary>
    /// Play a numbered phase timeline on every client.
    /// Safe to call from any client or the server — always routes through the server
    /// so both players see the same timeline at the same time.
    /// </summary>
    public void playPhase(int phase)
    {
        if (IsServer)
            PlayPhaseClientRpc(phase);
        else
            PlayPhaseServerRpc(phase);
    }

    [ServerRpc(RequireOwnership = false)]
    void PlayPhaseServerRpc(int phase) => PlayPhaseClientRpc(phase);

    [ClientRpc]
    void PlayPhaseClientRpc(int phase) => ExecutePhaseLocal(phase);

    void ExecutePhaseLocal(int phase)
    {
        switch (phase)
        {
            case 1: Timeline_Intro?.Play();       break;
            case 2: Timeline_HR?.Play();          break;
            case 3: Timeline_NHEJ?.Play();        break;
            case 4: Timeline_Apoptosis?.Play();   break;
            case 5: Timeline_Conclusion?.Play();  break;
        }
    }

    /// <summary>Pause a timeline locally. Called by Timeline UnityEvents — fires on whichever client the timeline is playing on.</summary>
    public void PauseForGameplay(PlayableDirector timeline) => timeline?.Pause();

    // ── Ship boarding — each client boards its own ship ───────────────────────

    public void BoardShips()
    {
        Transform seat = IsServer ? P53Ship.transform : ATMShip.transform;
        BoardShipLocal(seat);
    }

    void BoardShipLocal(Transform shipSeat)
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null) return;

        Transform t = xrOrigin.transform;

        // Undo any prior invert: detach the ship ROOT (not the seat) from the player.
        GameObject shipGO = IsServer ? P53ShipRoot : ATMShipRoot;
        if (shipGO != null)
        {
            Transform shipRoot = shipGO.transform;
            if (shipRoot.IsChildOf(t))
                shipRoot.SetParent(null, true);
        }

        if (t.parent != null) t.SetParent(null, false);
        t.SetParent(shipSeat, false);

        var move = t.GetComponentInChildren<DynamicMoveProvider>();
        if (move != null) move.enabled = false;

        t.localPosition = new Vector3(0f, -0.1f, 0f);
        t.localScale = new Vector3(1f, 1f, 1f);
        t.rotation = shipSeat.rotation;
    }

    // ── Ship parent inversion ────────────────────────────────────────────────
    // After the spline ride, flip the hierarchy: the ship becomes a child of
    // the player so the ship follows the player instead of the other way around.
    // Re-enables locomotion so the player can walk.
    public void InvertShipParenting()
    {
        // Server-side: hand each ship's ownership to the player whose hierarchy it's
        // joining, so its OwnerTransformSync (or NetworkTransform) broadcasts from
        // the right client and both players see the motion.
        if (IsServer)
        {
            ulong serverId = NetworkManager.Singleton.LocalClientId;
            ulong otherId = ulong.MaxValue;
            foreach (var c in NetworkManager.Singleton.ConnectedClientsIds)
                if (c != serverId) { otherId = c; break; }

            if (P53ShipRoot != null)
            {
                var no = P53ShipRoot.GetComponent<NetworkObject>();
                if (no != null && no.OwnerClientId != serverId) no.ChangeOwnership(serverId);
            }
            if (ATMShipRoot != null && otherId != ulong.MaxValue)
            {
                var no = ATMShipRoot.GetComponent<NetworkObject>();
                if (no != null && no.OwnerClientId != otherId) no.ChangeOwnership(otherId);
            }
        }

        InvertShipParentingLocal();
    }

    void InvertShipParentingLocal()
    {
        Debug.Log($"[Invert] Called. IsServer={IsServer}");

        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null) { Debug.LogError("[Invert] No XROrigin found."); return; }

        Transform t = xrOrigin.transform;
        Debug.Log($"[Invert] Player parent={(t.parent ? t.parent.name : "null")} P53ShipRoot={(P53ShipRoot ? P53ShipRoot.name : "null")} ATMShipRoot={(ATMShipRoot ? ATMShipRoot.name : "null")}");

        GameObject shipGO = IsServer ? P53ShipRoot : ATMShipRoot;
        if (shipGO == null)
        {
            Debug.LogError($"[Invert] {(IsServer ? "P53ShipRoot" : "ATMShipRoot")} is NOT assigned in GameManager Inspector. Aborting.");
            return;
        }
        Transform ship = shipGO.transform;

        // Capture both world transforms BEFORE any parent changes.
        Vector3    playerPos   = t.position;
        Quaternion playerRot   = t.rotation;
        Vector3    shipPos     = ship.position;
        Quaternion shipRot     = ship.rotation;
        Vector3    shipWorldSc = ship.lossyScale; // preserve visual size

        var shipNO = ship.GetComponent<NetworkObject>();
        if (shipNO != null) shipNO.AutoObjectParentSync = false;

        // Detach the player and force its localScale back to (1,1,1). If the seat
        // had a non-1 lossyScale, SetParent(null, true) bakes that into the player's
        // localScale, which throws off the camera offset and visually teleports
        // the player. Resetting to 1 fixes that — but means we have to explicitly
        // restore world position/rotation.
        try { t.SetParent(null, false); }
        catch (System.Exception e) { Debug.LogError($"[Invert] t.SetParent(null) threw: {e.Message}"); }
        Debug.Log($"[Invert] After unparent player → parent={(t.parent ? t.parent.name : "null")}");

        t.localScale = Vector3.one;
        t.position = playerPos;
        t.rotation = playerRot;

        try { if (ship.parent != null) ship.SetParent(null, true); }
        catch (System.Exception e) { Debug.LogError($"[Invert] ship.SetParent(null) threw: {e.Message}"); }
        Debug.Log($"[Invert] After unparent ship → parent={(ship.parent ? ship.parent.name : "null")}");

        try { ship.SetParent(t, false); }
        catch (System.Exception e) { Debug.LogError($"[Invert] ship.SetParent(t) threw: {e.Message}"); }
        Debug.Log($"[Invert] After reparent ship→player → ship.parent={(ship.parent ? ship.parent.name : "null")}");

        ship.position   = shipPos;
        ship.rotation   = shipRot;
        ship.localScale = shipWorldSc;

        var move = t.GetComponentInChildren<DynamicMoveProvider>();
        if (move != null) move.enabled = true;

        Debug.Log($"[Invert] FINAL — player.parent={(t.parent ? t.parent.name : "null")} ship.parent={(ship.parent ? ship.parent.name : "null")} player.worldPos={t.position} ship.worldPos={ship.position}");
    }

    System.Collections.IEnumerator LogShipPosNextFrame(Transform ship)
    {
        Vector3 firstPos = ship.position;
        yield return null;
        Vector3 secondPos = ship.position;
        bool snappedBack = (secondPos - firstPos).sqrMagnitude > 0.0001f;
        Debug.Log($"[Invert] One frame later: ship.parent={(ship.parent ? ship.parent.name : "null")} pos={secondPos} snappedBack={snappedBack}");
    }
}
