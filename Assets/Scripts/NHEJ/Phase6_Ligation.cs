using Unity.Netcode;
using UnityEngine;

// Phase 6: Ligase IV orbits the DNA break site like a conveyor. Players grab it
// as it passes and place it at their assigned LigationPoint to seal the nick.
// Placing within snapRadius of the correct point auto-fires the ligation;
// once both players' nicks are sealed the phase advances automatically.
public class Phase6_Ligation : NHEJPhaseHandler
{
    [Header("Tool Prefab (NetworkObject — must also have LigaseOrbitController)")]
    [SerializeField] GameObject ligaseToolPrefab;

    [Header("Ligation Points")]
    [SerializeField] LigationPoint[] player1LigationPoints;
    [SerializeField] LigationPoint[] player2LigationPoints;

    public override bool IsAutomatic => false;

    int player1Sealed;
    int player2Sealed;
    NetworkObject ligaseObject; // single shared Ligase

    public override void Setup()
    {
        player1Sealed = 0;
        player2Sealed = 0;
    }

    public override void StartPhase()
    {
        SetPointsActive(player1LigationPoints, true);
        SetPointsActive(player2LigationPoints, true);

        if (manager.IsServer && ligaseToolPrefab != null)
        {
            // Spawn one Ligase at the DNA center; LigaseOrbitController handles orbit + placement.
            Vector3 spawnPos = GetDNACenter();
            var go = Instantiate(ligaseToolPrefab, spawnPos, Quaternion.identity);
            ligaseObject = go.GetComponent<NetworkObject>();
            ligaseObject.Spawn(); // server-owned until a player grabs it
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseAdvance();
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (manager.IsServer && ligaseObject != null && ligaseObject.IsSpawned)
            ligaseObject.Despawn();
    }

    public bool ValidateLigation(int pointIndex, int playerRole)
    {
        var points = playerRole == 1 ? player1LigationPoints : player2LigationPoints;
        if (points == null) return false;

        foreach (var lp in points)
        {
            if (lp.PointIndex == pointIndex && !lp.IsSealed)
                return true;
        }
        return false;
    }

    public void ServerMarkSealed(int pointIndex, int role)
    {
        if (role == 1)
        {
            player1Sealed++;
            if (player1Sealed >= (player1LigationPoints != null ? player1LigationPoints.Length : 0))
                manager.ServerMarkPlayerComplete(1);
        }
        else if (role == 2)
        {
            player2Sealed++;
            if (player2Sealed >= (player2LigationPoints != null ? player2LigationPoints.Length : 0))
                manager.ServerMarkPlayerComplete(2);
        }
    }

    public void OnLigationConfirmed(int pointIndex, ulong clientId)
    {
        LigationPoint[] allPoints = CombineArrays(player1LigationPoints, player2LigationPoints);
        foreach (var lp in allPoints)
        {
            if (lp != null && lp.PointIndex == pointIndex)
            {
                lp.PerformSeal();
                break;
            }
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayLigationSuccess();
    }

    Vector3 GetDNACenter()
    {
        if (manager.LeftDNAEnd != null && manager.RightDNAEnd != null)
            return (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
        if (manager.LeftDNAEnd != null) return manager.LeftDNAEnd.position;
        if (manager.RightDNAEnd != null) return manager.RightDNAEnd.position;
        return Vector3.zero;
    }

    void SetPointsActive(LigationPoint[] points, bool active)
    {
        if (points == null) return;
        foreach (var lp in points)
        {
            if (lp != null) lp.gameObject.SetActive(active);
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
