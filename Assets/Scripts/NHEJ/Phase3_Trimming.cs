using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Phase 3: Both players trim their assigned DNA end using the Artemis tool.
// Player 1 trims the left end, Player 2 trims the right end. (right?)
public class Phase3_Trimming : NHEJPhaseHandler
{
    [Header("Tool Prefabs (NetworkObject)")]
    [SerializeField] GameObject artemisToolPrefab;

    [Header("Tool Spawn Positions")]
    [SerializeField] Transform player1ToolSpawn;
    [SerializeField] Transform player2ToolSpawn;

    [Header("Trim Points")]
    [SerializeField] TrimPoint[] player1TrimPoints;
    [SerializeField] TrimPoint[] player2TrimPoints;

    public override bool IsAutomatic => false;

    int player1Trimmed;
    int player2Trimmed;
    NetworkObject player1Tool;
    NetworkObject player2Tool;

    public override void Setup()
    {
        player1Trimmed = 0;
        player2Trimmed = 0;
    }

    public override void StartPhase()
    {
        DSBScenario scenario = manager.Scenario;
        bool p1NeedsTrim = scenario == DSBScenario.LeftOverhangOnly || scenario == DSBScenario.BothOverhangs;
        bool p2NeedsTrim = scenario == DSBScenario.RightOverhangOnly || scenario == DSBScenario.BothOverhangs;

        Debug.Log($"[NHEJ] Phase3 scenario={scenario} p1Trim={p1NeedsTrim} p2Trim={p2NeedsTrim}");

        // Activate only relevant trim point visuals
        SetTrimPointsActive(player1TrimPoints, p1NeedsTrim);
        SetTrimPointsActive(player2TrimPoints, p2NeedsTrim);

        // Server spawns tools only for players who need to trim,
        // and auto-completes players whose end is blunt
        if (manager.IsServer)
        {
            if (artemisToolPrefab != null)
            {
                if (p1NeedsTrim)
                {
                    Vector3 p1Pos = player1ToolSpawn != null ? player1ToolSpawn.position : manager.LeftDNAEnd.position + Vector3.up * 0.1f;
                    var p1Obj = Instantiate(artemisToolPrefab, p1Pos, Quaternion.identity);
                    player1Tool = p1Obj.GetComponent<NetworkObject>();
                    player1Tool.SpawnWithOwnership(manager.Player1Id);
                }

                if (p2NeedsTrim)
                {
                    Vector3 p2Pos = player2ToolSpawn != null ? player2ToolSpawn.position : manager.RightDNAEnd.position + Vector3.up * 0.1f;
                    var p2Obj = Instantiate(artemisToolPrefab, p2Pos, Quaternion.identity);
                    player2Tool = p2Obj.GetComponent<NetworkObject>();
                    player2Tool.SpawnWithOwnership(manager.Player2Id);
                }
            }

            if (!p1NeedsTrim) manager.ServerMarkPlayerComplete(1);
            if (!p2NeedsTrim) manager.ServerMarkPlayerComplete(2);
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseAdvance();
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (manager.IsServer)
        {
            if (player1Tool != null && player1Tool.IsSpawned) player1Tool.Despawn();
            if (player2Tool != null && player2Tool.IsSpawned) player2Tool.Despawn();
        }
    }

    public bool ValidateTrim(int pointIndex, int playerRole)
    {
        var points = playerRole == 1 ? player1TrimPoints : player2TrimPoints;
        if (points == null) return false;

        foreach (var tp in points)
        {
            if (tp.PointIndex == pointIndex && !tp.IsTrimmed)
                return true;
        }
        return false;
    }

    public void ServerMarkTrimmed(int pointIndex, ulong clientId)
    {
        int role = manager.GetPlayerRole(clientId);

        if (role == 1 && player1TrimPoints.Length > 0)
        {
            player1Trimmed++;
            if (player1Trimmed >= player1TrimPoints.Length)
                manager.ServerMarkPlayerComplete(1);
        }
        else if (role == 2 && player2TrimPoints.Length > 0)
        {
            player2Trimmed++;
            if (player2Trimmed >= player2TrimPoints.Length)
                manager.ServerMarkPlayerComplete(2);
        }
    }

    public void OnTrimConfirmed(int pointIndex, ulong clientId)
    {
        // Find and perform the trim on the correct point
        TrimPoint[] allPoints = CombineArrays(player1TrimPoints, player2TrimPoints);
        foreach (var tp in allPoints)
        {
            if (tp != null && tp.PointIndex == pointIndex)
            {
                tp.PerformTrim();
                break;
            }
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayTrimSuccess();
    }

    void SetTrimPointsActive(TrimPoint[] points, bool active)
    {
        if (points == null) return;
        foreach (var tp in points)
        {
            if (tp != null) tp.gameObject.SetActive(active);
        }
    }

    static T[] CombineArrays<T>(T[] a, T[] b)
    {
        if (a == null && b == null) return new T[0];
        if (a == null) return b;
        if (b == null) return a;
        var result = new T[a.Length + b.Length];
        a.CopyTo(result, 0);
        b.CopyTo(result, a.Length);
        return result;
    }
}
