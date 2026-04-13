using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Playables;
using Unity.XR.CoreUtils;

public class GameManager : NetworkBehaviour
{
    public int maxPlayers = 2;

    [Header("UI Elements")]
    public GameObject StartGameUI;
    public Button StartGameButton;

    [Header("Timeline")]
    public PlayableDirector Timeline;

    [Header("Ships to Board")]
    public GameObject P53Ship;
    public GameObject ATMShip;

    void Start(){
        StartGameButton.onClick.AddListener(() => StartGame());
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log("PLAYER CONNECTED! (Current Players: " + NetworkManager.Singleton.ConnectedClientsList.Count + ")");

        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count >= maxPlayers)
        {
            StartGameUI.SetActive(true);
        }
    }

    void StartGame()
    {
        if (!IsServer) return;

        NetworkStartGameClientRpc();
    }

    [ClientRpc]
    void NetworkStartGameClientRpc()
    {
        if (!IsOwner) return;

        BoardShipLocal(P53Ship.transform);
    }

    void BoardShipLocal(Transform shipSeat)
    {
        Transform xrOrigin = FindFirstObjectByType<XROrigin>().transform;

        // Optional: preserve world pose before parenting (prevents sudden snap bugs)
        Vector3 worldPos = xrOrigin.position;
        Quaternion worldRot = xrOrigin.rotation;

        xrOrigin.SetParent(shipSeat, false);

        // Snap cleanly into seat
        xrOrigin.localPosition = Vector3.zero;
        xrOrigin.localRotation = Quaternion.identity;

        Timeline.Play();
    }

    /*void StartGame()
    {
        // Board Ship GameObject

        Timeline.Play();
    }*/

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
