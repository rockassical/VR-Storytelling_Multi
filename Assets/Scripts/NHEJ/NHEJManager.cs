using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

public class NHEJManager : NetworkBehaviour
{
    public static NHEJManager Instance { get; private set; }

    [Header("Phase Handlers (assign in order: Phase0 through Phase8)")]
    [SerializeField] NHEJPhaseHandler[] phaseHandlers = new NHEJPhaseHandler[9];

    [Header("Options")]
    [SerializeField] bool includeGapFill = true;
    [SerializeField] bool showPlacementIndicators = true;
    [SerializeField] DSBScenario dsbScenario = DSBScenario.LeftOverhangOnly;

    public DSBScenario Scenario => dsbScenario;

    [Header("Enemy AI")]
    [SerializeField] GameObject enemyPrefab;
    [Tooltip("Seconds both proteins must stay correctly placed before the phase advances.")]
    [SerializeField] float placementConfirmDelay = 3f;

    [Header("Debug")]
    [SerializeField] bool debugBypass = false;
    [SerializeField] bool debugAutoCompletePlayerPhases = true;
    [SerializeField] float debugPlayerPhaseDelay = 3f;

    [Header("DNA Break Site")]
    [SerializeField] Transform leftDNAEnd;
    [SerializeField] Transform rightDNAEnd;

    [Header("DNA Break Point (assign the NHEJBreakPoint on the DNA_testcuts root)")]
    [SerializeField] NHEJBreakPoint breakPoint;

    public Transform LeftDNAEnd => leftDNAEnd;
    public Transform RightDNAEnd => rightDNAEnd;
    public NHEJBreakPoint BreakPoint => breakPoint;

    readonly NetworkVariable<NHEJPhase> currentPhase = new(
        NHEJPhase.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    readonly NetworkVariable<ulong> player1Id = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    readonly NetworkVariable<ulong> player2Id = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    readonly NetworkVariable<bool> player1PhaseComplete = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    readonly NetworkVariable<bool> player2PhaseComplete = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NHEJPhase CurrentPhase => currentPhase.Value;
    public ulong Player1Id => player1Id.Value;
    public ulong Player2Id => player2Id.Value;
    public bool Player1PhaseComplete => player1PhaseComplete.Value;
    public bool Player2PhaseComplete => player2PhaseComplete.Value;
    public bool IncludeGapFill => includeGapFill;
    public bool ShowPlacementIndicators => showPlacementIndicators;
    public bool DebugBypass => debugBypass;

    // Trimming score reported after Phase 3 cut (1 = cut at junction, 0 = cut at tip)
    float trimmingScore = 1f;
    public float TrimmingScore => trimmingScore;

    // Gap fill score reported by Phase4_GapFill (placement accuracy 0–100)
    float gapFillScore = 100f;
    public float GapFillScore => gapFillScore;

    public void ReportGapFillScore(float score)
    {
        gapFillScore = score;
    }

    int assignedCount;
    readonly System.Collections.Generic.List<NetworkObject> spawnedEnemies = new();
    int enemyCount = 3;
    Coroutine pendingAdvanceCoroutine;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        foreach (var point in FindObjectsOfType<ProteinPlacementPoint>())
            point.SetIndicatorVisible(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        for (int i = 0; i < phaseHandlers.Length; i++)
        {
            if (phaseHandlers[i] != null)
                phaseHandlers[i].manager = this;
        }
        
        currentPhase.OnValueChanged += OnPhaseChanged;

        if (IsServer)
        {
            if (!debugBypass)
                XRINetworkGameManager.Instance.playerStateChanged += OnPlayerStateChanged;

            currentPhase.Value = NHEJPhase.WaitingForPlayers;

            if (debugBypass)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                player1Id.Value = localId;
                player2Id.Value = localId;
                Debug.Log($"[NHEJ DEBUG] Bypass active — both roles assigned to client {localId}");
                currentPhase.Value = NHEJPhase.Phase0_Trigger;
            }
        }

        // If reconnecting into an active phase, start it locally
        if (currentPhase.Value != NHEJPhase.WaitingForPlayers)
        {
            StartPhaseLocally(currentPhase.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentPhase.OnValueChanged -= OnPhaseChanged;

        if (IsServer && !debugBypass && XRINetworkGameManager.Instance != null)
        {
            XRINetworkGameManager.Instance.playerStateChanged -= OnPlayerStateChanged;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var handler = GetCurrentHandler();
        if (handler != null)
        {
            handler.UpdatePhase();
        }
    }

    void OnPlayerStateChanged(ulong playerId, bool joined)
    {
        if (!IsServer) return;
        if (!joined) return;

        if (currentPhase.Value != NHEJPhase.WaitingForPlayers) return;

        if (player1Id.Value == ulong.MaxValue)
        {
            player1Id.Value = playerId;
            assignedCount++;
            Debug.Log($"[NHEJ] Player 1 (p53) assigned: {playerId}");
        }
        else if (player2Id.Value == ulong.MaxValue && playerId != player1Id.Value)
        {
            player2Id.Value = playerId;
            assignedCount++;
            Debug.Log($"[NHEJ] Player 2 (ATM) assigned: {playerId}");
        }

        if (assignedCount >= 2)
        {
            currentPhase.Value = NHEJPhase.Phase0_Trigger;
        }
    }

    void OnPhaseChanged(NHEJPhase oldPhase, NHEJPhase newPhase)
    {
        Debug.Log($"[NHEJ] Phase changed: {oldPhase} -> {newPhase}");

        // Complete old phase handler
        var oldHandler = GetHandler(oldPhase);
        if (oldHandler != null)
            oldHandler.CompletePhase();

        StartPhaseLocally(newPhase);
    }

    void StartPhaseLocally(NHEJPhase phase)
    {
        var handler = GetHandler(phase);
        if (handler != null)
        {
            handler.Setup();
            handler.StartPhase();
        }
        else if (phase == NHEJPhase.Phase8_Assessment && IsServer)
        {
            // Phase 8 handler not completed yet so I have a placeholdder
            Debug.Log("[NHEJ] Phase8 handler null, skipping to Complete");
            StartCoroutine(DelayedAdvance(1f));
        }

        // Spawn enemies during Phase4 (gap fill) only.
        // Enemy behaviour during gap fill is TBD — currently orbits the DNA site.
        if (IsServer && phase == NHEJPhase.Phase4_GapFill && enemyPrefab != null)
        {
            // enemyCount++;
            Vector3 basePos = leftDNAEnd != null
                ? leftDNAEnd.position + Vector3.up * 0.5f + Vector3.back * 0.8f
                : Vector3.zero;

            for (int i = 0; i < enemyCount; i++)
            {
                // Spread spawns slightly so they don't stack.
                Vector3 offset = new Vector3(Mathf.Cos(i * 1.2f), 0f, Mathf.Sin(i * 1.2f)) * 0.4f;
                var go = Instantiate(enemyPrefab, basePos + offset, Quaternion.identity);
                var no = go.GetComponent<NetworkObject>();
                no?.Spawn();
                if (no != null) spawnedEnemies.Add(no);
            }
            Debug.Log($"[NHEJ] Spawned {enemyCount} enemies for phase {phase}");
        }

        if (debugBypass && debugAutoCompletePlayerPhases && IsServer)
        {
            bool isPlayerPhase = handler != null && !handler.IsAutomatic;
            if (isPlayerPhase)
            {
                Debug.Log($"[NHEJ DEBUG] Auto-completing player phase {phase} in {debugPlayerPhaseDelay}s");
                StartCoroutine(DebugAutoCompletePhase(debugPlayerPhaseDelay));
            }
        }
    }

    IEnumerator DelayedAdvance(float delay)
    {
        yield return new WaitForSeconds(delay);
        AdvancePhase();
    }

    IEnumerator DebugAutoCompletePhase(float delay)
    {
        yield return new WaitForSeconds(delay);
        player1PhaseComplete.Value = true;
        player2PhaseComplete.Value = true;
        AdvancePhase();
    }

    NHEJPhaseHandler GetCurrentHandler()
    {
        return GetHandler(currentPhase.Value);
    }

    NHEJPhaseHandler GetHandler(NHEJPhase phase)
    {
        int index = PhaseToIndex(phase);
        if (index < 0 || index >= phaseHandlers.Length) return null;
        return phaseHandlers[index];
    }

    int PhaseToIndex(NHEJPhase phase)
    {
        return phase switch
        {
            NHEJPhase.Phase0_Trigger => 0,
            NHEJPhase.Phase1_KuBinding => 1,
            NHEJPhase.Phase2_DNAPKcs => 2,
            NHEJPhase.Phase3_Trimming => 3,
            NHEJPhase.Phase4_GapFill => 4,
            NHEJPhase.Phase5_Alignment => 5,
            NHEJPhase.Phase6_Ligation => 6,
            NHEJPhase.Phase7_Cleanup => 7,
            NHEJPhase.Phase8_Assessment => 8,
            _ => -1
        };
    }

    public void AdvancePhase()
    {
        if (!IsServer) return;

        // Cancel any pending confirmation delay.
        if (pendingAdvanceCoroutine != null)
        {
            StopCoroutine(pendingAdvanceCoroutine);
            pendingAdvanceCoroutine = null;
        }

        // Despawn all enemies before moving to next phase.
        foreach (var enemy in spawnedEnemies)
            if (enemy != null && enemy.IsSpawned) enemy.Despawn();
        spawnedEnemies.Clear();

        // Simplified flow per client request (2025-03-17):
        //   Phase0 → Phase3 (trim) → Phase4 (gap fill) → Phase8 (assessment)
        // Phases 1, 2, 5, 6, 7 are intentionally bypassed but their code is preserved for easy reversion.
        NHEJPhase next = currentPhase.Value switch
        {
            NHEJPhase.Phase0_Trigger    => NHEJPhase.Phase3_Trimming,    // skip KuBinding + DNAPKcs
            NHEJPhase.Phase3_Trimming   => NHEJPhase.Phase4_GapFill,
            NHEJPhase.Phase4_GapFill    => NHEJPhase.Phase8_Assessment,  // skip Alignment + Ligation + Cleanup
            NHEJPhase.Phase8_Assessment => NHEJPhase.Complete,
            _ => NHEJPhase.Complete
        };

        player1PhaseComplete.Value = false;
        player2PhaseComplete.Value = false;
        currentPhase.Value = next;
    }

    [ServerRpc(RequireOwnership = false)]
    public void AdvancePhaseServerRpc()
    {
        AdvancePhase();
    }

    public void BeginSequence()
    {
        BeginSequenceServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void BeginSequenceServerRpc()
    {
        if (currentPhase.Value != NHEJPhase.WaitingForPlayers) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        player1Id.Value = localId;
        player2Id.Value = localId;
        currentPhase.Value = NHEJPhase.Phase0_Trigger;
    }


    [ServerRpc(RequireOwnership = false)]
    public void ReportPlayerCompleteServerRpc(ulong clientId)
    {
        if (clientId == player1Id.Value)
            player1PhaseComplete.Value = true;
        else if (clientId == player2Id.Value)
            player2PhaseComplete.Value = true;

        if (player1PhaseComplete.Value && player2PhaseComplete.Value)
        {
            AdvancePhase();
        }
    }

    public int GetPlayerRole(ulong clientId)
    {
        if (clientId == player1Id.Value) return 1;
        if (clientId == player2Id.Value) return 2;
        return 0;
    }

    public bool IsLocalPlayer(ulong clientId)
    {
        return NetworkManager.Singleton.LocalClientId == clientId;
    }

    public void ServerMarkPlayerComplete(int playerRole)
    {
        if (!IsServer) return;
        if (playerRole == 1) player1PhaseComplete.Value = true;
        else if (playerRole == 2) player2PhaseComplete.Value = true;

        BroadcastPlayerRoleCompleteClientRpc(playerRole);

        // Start confirmation window so enemy has time to steal before phase advances.
        if (player1PhaseComplete.Value && player2PhaseComplete.Value
            && pendingAdvanceCoroutine == null)
        {
            pendingAdvanceCoroutine = StartCoroutine(ConfirmAndAdvance());
        }
    }

    IEnumerator ConfirmAndAdvance()
    {
        yield return new WaitForSeconds(placementConfirmDelay);
        pendingAdvanceCoroutine = null;
        // Re-check in case enemy stole during the window.
        if (player1PhaseComplete.Value && player2PhaseComplete.Value)
            AdvancePhase();
    }

    /// <summary>
    /// Called server-side by ProteinOrbitController when a protein leaves its correct location.
    /// Cancels any pending phase-advance and notifies the phase handler.
    /// </summary>
    public void ServerUnmarkPlayerComplete(int playerRole)
    {
        if (!IsServer) return;
        if (playerRole == 1) player1PhaseComplete.Value = false;
        else if (playerRole == 2) player2PhaseComplete.Value = false;

        if (pendingAdvanceCoroutine != null)
        {
            StopCoroutine(pendingAdvanceCoroutine);
            pendingAdvanceCoroutine = null;
        }

        GetCurrentHandler()?.OnEnemyStolenProtein(playerRole);
    }

    /// <summary>Fires on ALL clients whenever a player role marks complete. Subscribe for local visual feedback.</summary>
    public event Action<int> OnPlayerRoleMarkedComplete;

    [ClientRpc]
    void BroadcastPlayerRoleCompleteClientRpc(int playerRole)
    {
        OnPlayerRoleMarkedComplete?.Invoke(playerRole);
    }

    /// <summary>
    /// Called server-side by ProteinOrbitController when a player places their pickup protein.
    /// Routes to the current phase handler's OnProteinPlaced().
    /// </summary>
    public void ReportProteinPickup(int playerRole)
    {
        if (!IsServer) return;
        GetCurrentHandler()?.OnProteinPlaced(playerRole);
    }

    /// <summary>Returns the handler for the currently active phase (null if none).</summary>
    public NHEJPhaseHandler GetCurrentPhaseHandler() => GetCurrentHandler();

    #region Cut RPCs (Phase 3 blade mechanic)

    /// <summary>
    /// Called server-only by Phase3_Trimming.StartPhase() to generate the break on all clients.
    /// Broadcasts the same seed so every client produces an identical break deterministically.
    /// </summary>
    public void TriggerBreakGeneration(int seed)
    {
        if (!IsServer) return;
        GenerateBreakClientRpc(seed);
    }

    [ClientRpc]
    void GenerateBreakClientRpc(int seed)
    {
        breakPoint?.GenerateBreak(seed);
    }

    /// <summary>
    /// Fired by ArtemisBlade (owner client) when the blade enters an OverhangZone.
    /// Server validates, applies the cut on all clients, and notifies Phase3_Trimming.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ReportCutServerRpc(Vector3 bladeTipWorldPos, ulong clientId, int playerRole,
        ServerRpcParams rpcParams = default)
    {
        if (currentPhase.Value != NHEJPhase.Phase3_Trimming) return;

        // In debug bypass mode, accept any role.
        int role = debugBypass ? playerRole : GetPlayerRole(clientId);
        if (role == 0) return;

        float score = breakPoint != null
            ? breakPoint.ComputeTrimScore(role, bladeTipWorldPos)
            : 1f;

        // Store trimming score (average if both players cut in BothOverhangs scenario).
        trimmingScore = (trimmingScore + score) * 0.5f;

        // Apply cut visuals on all clients.
        CutConfirmedClientRpc(bladeTipWorldPos, role);

        // Notify Phase3 handler to mark this player complete.
        var phase3 = phaseHandlers[3] as Phase3_Trimming;
        phase3?.OnCutMade(role);

        Debug.Log($"[NHEJ] Cut confirmed — role={role} score={score:P0}");
    }

    [ClientRpc]
    void CutConfirmedClientRpc(Vector3 bladeTipWorldPos, int playerRole)
    {
        breakPoint?.CutOverhang(playerRole, bladeTipWorldPos);

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayTrimSuccess();
    }

    #endregion

    #region Trim / Ligation RPCs

    /* DISABLED — snap-to-TrimPoint mechanic replaced by ArtemisBlade collision (ReportCutServerRpc).
       Preserved here for easy reversion.

    [ServerRpc(RequireOwnership = false)]
    public void ReportTrimServerRpc(int pointIndex, ulong clientId, int pointRole = 0)
    {
        if (currentPhase.Value != NHEJPhase.Phase3_Trimming) return;
        int role;
        if (debugBypass && pointRole != 0) { role = pointRole; }
        else { role = GetPlayerRole(clientId); if (role == 0) return; }
        var handler = phaseHandlers[3] as Phase3_Trimming;
        if (handler != null && handler.ValidateTrim(pointIndex, role))
        {
            ReportTrimClientRpc(pointIndex, clientId);
            handler.ServerMarkTrimmed(pointIndex, role);
        }
    }

    [ClientRpc]
    void ReportTrimClientRpc(int pointIndex, ulong clientId)
    {
        var handler = phaseHandlers[3] as Phase3_Trimming;
        handler?.OnTrimConfirmed(pointIndex, clientId);
    }
    */

    [ServerRpc(RequireOwnership = false)]
    public void ReportLigationServerRpc(int pointIndex, ulong clientId, int pointRole = 0)
    {
        if (currentPhase.Value != NHEJPhase.Phase6_Ligation) return;

        // In debug single-player mode use the point's own assignedPlayerRole.
        int role;
        if (debugBypass && pointRole != 0)
        {
            role = pointRole;
        }
        else
        {
            role = GetPlayerRole(clientId);
            if (role == 0) return;
        }

        var handler = phaseHandlers[6] as Phase6_Ligation;
        if (handler != null && handler.ValidateLigation(pointIndex, role))
        {
            ReportLigationClientRpc(pointIndex, clientId);
            handler.ServerMarkSealed(pointIndex, role);
        }
    }

    [ClientRpc]
    void ReportLigationClientRpc(int pointIndex, ulong clientId)
    {
        var handler = phaseHandlers[6] as Phase6_Ligation;
        if (handler != null)
        {
            handler.OnLigationConfirmed(pointIndex, clientId);
        }
    }

    #endregion

    #region Debug UI
    // I still don't have a VR Headset and Dr. Li has the flu this week
    void OnGUI()
    {
        if (!debugBypass) return;

        GUILayout.BeginArea(new Rect(10, 10, 220, 160));
        GUILayout.Label("=== NHEJ DEBUG ===");

        if (!NetworkManager.Singleton.IsListening)
        {
            if (GUILayout.Button("Start Host"))
                NetworkManager.Singleton.StartHost();
        }
        else
        {
            GUILayout.Label($"Phase: {currentPhase.Value}");
            GUILayout.Label($"Scenario: {dsbScenario}");
            GUILayout.Label($"Listening as: {(NetworkManager.Singleton.IsHost ? "Host" : "Client")}");

            if (IsServer && GUILayout.Button("Force Advance Phase"))
                AdvancePhase();
        }

        GUILayout.EndArea();
    }

    #endregion
}
