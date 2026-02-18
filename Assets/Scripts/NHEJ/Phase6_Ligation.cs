using Unity.Netcode;
using UnityEngine;

// Phase 6: Both players seal one strand nick each using the Ligase tool
// Player 1 seals strand 1, Player 2 seals strand 2
public class Phase6_Ligation : NHEJPhaseHandler
{
    [Header("Tool Prefabs (NetworkObject)")]
    [SerializeField] GameObject ligaseToolPrefab;

    [Header("Tool Spawn Positions")]
    [SerializeField] Transform player1ToolSpawn;
    [SerializeField] Transform player2ToolSpawn;

    [Header("Ligation Points")]
    [SerializeField] LigationPoint[] player1LigationPoints;
    [SerializeField] LigationPoint[] player2LigationPoints;

    public override bool IsAutomatic => false;

    int player1Sealed;
    int player2Sealed;
    NetworkObject player1Tool;
    NetworkObject player2Tool;

    public override void Setup()
    {
        player1Sealed = 0;
        player2Sealed = 0;
    }

    public override void StartPhase()
    {
        SetPointsActive(player1LigationPoints, true);
        SetPointsActive(player2LigationPoints, true);

        // Server spawns ligase tools
        if (manager.IsServer && ligaseToolPrefab != null)
        {
            Vector3 p1Pos = player1ToolSpawn != null ? player1ToolSpawn.position : manager.LeftDNAEnd.position + Vector3.up * 0.1f;
            Vector3 p2Pos = player2ToolSpawn != null ? player2ToolSpawn.position : manager.RightDNAEnd.position + Vector3.up * 0.1f;

            var p1Obj = Instantiate(ligaseToolPrefab, p1Pos, Quaternion.identity);
            player1Tool = p1Obj.GetComponent<NetworkObject>();
            player1Tool.SpawnWithOwnership(manager.Player1Id);

            var p2Obj = Instantiate(ligaseToolPrefab, p2Pos, Quaternion.identity);
            player2Tool = p2Obj.GetComponent<NetworkObject>();
            player2Tool.SpawnWithOwnership(manager.Player2Id);
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

    public void ServerMarkSealed(int pointIndex, ulong clientId)
    {
        int role = manager.GetPlayerRole(clientId);

        if (role == 1)
        {
            player1Sealed++;
            if (player1Sealed >= (player1LigationPoints != null ? player1LigationPoints.Length : 0))
                manager.ReportPlayerCompleteServerRpc(clientId);
        }
        else if (role == 2)
        {
            player2Sealed++;
            if (player2Sealed >= (player2LigationPoints != null ? player2LigationPoints.Length : 0))
                manager.ReportPlayerCompleteServerRpc(clientId);
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
