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

    [Header("Timelines")]
    public PlayableDirector Timeline_Intro;
    public PlayableDirector Timeline_HR, Timeline_NHEJ, Timeline_Apoptosis, Timeline_Conclusion;

    [Header("Ships to Board")]
    public GameObject P53Ship;
    public GameObject ATMShip;


    //private int phaseCounter = 0;

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

        //BoardShipLocal(P53Ship.transform);
        //Timeline_HR.Play();

        playPhase(1);
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

        //Timeline_HR.Play();
    }

    public void playPhase(int phase)
    {
        BoardShipLocal(P53Ship.transform);
        //BoardShipLocal(ATMShip.transform);

        switch(phase){
            // Intro phase
            case 1:
                Timeline_Intro.Play();

                break;

            // HR phase
            case 2:
                Timeline_HR.Play();

                break;

            // NHEJ phase
            case 3:
               Timeline_NHEJ.Play();
               
               break;

            // Apoptosis phase
            case 4:
                Timeline_Apoptosis.Play();

                break;

            default:
                break;
                
        }
    }

    public void PauseForGameplay(PlayableDirector timeline){
        timeline.Pause();
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
