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
    public GameObject P53Ship;
    public GameObject ATMShip;

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

    /// <summary>
    /// Use after a move-sequence has completed and the ship was inverted under the player.
    /// Undoes the inversion and re-seats the player inside the ship for the next spline ride.
    /// Call from a Timeline event (or manually) when HR completes, before the NHEJ ride.
    /// </summary>
    public void ReBoardShips() => BoardShips();

    void BoardShipLocal(Transform shipSeat)
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null) return;

        Transform t = xrOrigin.transform;

        // If the ship is currently parented under the player (post-invert state),
        // unparent it first so we can reverse the hierarchy cleanly.
        if (shipSeat.IsChildOf(t))
            shipSeat.SetParent(null, true);

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
        InvertShipParentingLocal();
    }

    void InvertShipParentingLocal()
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null) return;

        Transform t = xrOrigin.transform;
        Transform ship = t.parent;
        if (ship == null) return; // not currently a child of a ship

        Vector3 worldPos = t.position;
        Quaternion worldRot = t.rotation;

        // Unparent player, then reparent ship under the player at the same world position.
        t.SetParent(null, true);
        ship.SetParent(t, true);

        t.position = worldPos;
        t.rotation = worldRot;

        var move = t.GetComponentInChildren<DynamicMoveProvider>();
        if (move != null) move.enabled = true;
    }
}
