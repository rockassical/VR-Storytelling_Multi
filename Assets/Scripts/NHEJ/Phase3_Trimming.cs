using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Phase 3: Artemis orbits the DNA break site like a conveyor. Players grab it
// as it passes and place it at their assigned TrimPoint to trim the overhang.
// Placing it within snapRadius of the correct TrimPoint auto-fires the trim;
// once both players' ends are trimmed the phase advances automatically.
public class Phase3_Trimming : NHEJPhaseHandler
{
    [Header("Tool Prefab (NetworkObject — must also have ArtemisOrbitController)")]
    [SerializeField] GameObject artemisToolPrefab;

    [Header("Trim Points")]
    [SerializeField] TrimPoint[] player1TrimPoints;
    [SerializeField] TrimPoint[] player2TrimPoints;

    public override bool IsAutomatic => false;

    int player1Trimmed;
    int player2Trimmed;
    NetworkObject artemisObject; // single shared Artemis

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

        // Activate only the TrimPoints relevant to this scenario
        SetTrimPointsActive(player1TrimPoints, p1NeedsTrim);
        SetTrimPointsActive(player2TrimPoints, p2NeedsTrim);

        if (manager.IsServer)
        {
            // Spawn one Artemis for whichever ends need trimming.
            // ArtemisOrbitController handles orbit + grab + placement on all clients.
            if ((p1NeedsTrim || p2NeedsTrim) && artemisToolPrefab != null)
            {
                Vector3 spawnPos = GetDNACenter();
                var go = Instantiate(artemisToolPrefab, spawnPos, Quaternion.identity);
                artemisObject = go.GetComponent<NetworkObject>();
                artemisObject.Spawn(); // server-owned until a player grabs it
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
        if (manager.IsServer && artemisObject != null && artemisObject.IsSpawned)
            artemisObject.Despawn();
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

    public void ServerMarkTrimmed(int pointIndex, int role)
    {
        if (role == 1 && player1TrimPoints != null && player1TrimPoints.Length > 0)
        {
            player1Trimmed++;
            if (player1Trimmed >= player1TrimPoints.Length)
                manager.ServerMarkPlayerComplete(1);
        }
        else if (role == 2 && player2TrimPoints != null && player2TrimPoints.Length > 0)
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

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        if (manager.LeftDNAEnd != null) return manager.LeftDNAEnd.position;
        if (manager.RightDNAEnd != null) return manager.RightDNAEnd.position;
        return Vector3.zero;
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
