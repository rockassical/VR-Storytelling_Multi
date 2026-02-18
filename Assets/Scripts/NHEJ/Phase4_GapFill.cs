using System.Collections;
using UnityEngine;

// Phase 4 (not implemented fully yet): Gap fill — players place nucleotides into gap positions (to test, make sure Include Gap Fill is true in NHEJManager)
public class Phase4_GapFill : NHEJPhaseHandler
{
    [Header("Gap Sockets")]
    [SerializeField] GameObject[] player1GapSockets;
    [SerializeField] GameObject[] player2GapSockets;

    [Header("Nucleotide Prefabs")]
    [SerializeField] GameObject nucleotidePrefab;
    [SerializeField] Transform[] nucleotideSpawnPoints;

    [Header("Feedback")]
    [SerializeField] float wrongPlacementWobbleDuration = 0.5f;

    public override bool IsAutomatic => false;

    bool[] player1SocketFilled;
    bool[] player2SocketFilled;

    public override void Setup()
    {
        player1SocketFilled = new bool[player1GapSockets != null ? player1GapSockets.Length : 0];
        player2SocketFilled = new bool[player2GapSockets != null ? player2GapSockets.Length : 0];
    }

    public override void StartPhase()
    {
        SetSocketsActive(player1GapSockets, true);
        SetSocketsActive(player2GapSockets, true);

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseAdvance();
    }

    public override void UpdatePhase()
    {
        // Check if all sockets are filled
        if (AllFilled(player1SocketFilled) && AllFilled(player2SocketFilled))
        {
            if (manager != null && manager.IsServer)
            {
                manager.AdvancePhase();
            }
        }
    }

    public override void CompletePhase()
    {
        SetSocketsActive(player1GapSockets, false);
        SetSocketsActive(player2GapSockets, false);
    }


    public void FillSocket(GameObject socket, ulong clientId)
    {
        int role = manager.GetPlayerRole(clientId);

        if (role == 1 && TryFillSocket(player1GapSockets, player1SocketFilled, socket))
        {
            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();
            if (AllFilled(player1SocketFilled))
                manager.ReportPlayerCompleteServerRpc(clientId);
        }
        else if (role == 2 && TryFillSocket(player2GapSockets, player2SocketFilled, socket))
        {
            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();
            if (AllFilled(player2SocketFilled))
                manager.ReportPlayerCompleteServerRpc(clientId);
        }
    }

    bool TryFillSocket(GameObject[] sockets, bool[] filled, GameObject socket)
    {
        if (sockets == null) return false;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] == socket && !filled[i])
            {
                filled[i] = true;
                return true;
            }
        }
        return false;
    }

    bool AllFilled(bool[] filled)
    {
        if (filled == null || filled.Length == 0) return true;
        foreach (bool f in filled)
        {
            if (!f) return false;
        }
        return true;
    }

    void SetSocketsActive(GameObject[] sockets, bool active)
    {
        if (sockets == null) return;
        foreach (var s in sockets)
        {
            if (s != null) s.SetActive(active);
        }
    }
}
