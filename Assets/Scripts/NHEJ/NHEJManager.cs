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
    [SerializeField] DSBScenario dsbScenario = DSBScenario.LeftOverhangOnly;

    public DSBScenario Scenario => dsbScenario;

    [Header("Debug")]
    [SerializeField] bool debugBypass = false;
    [SerializeField] bool debugAutoCompletePlayerPhases = true;
    [SerializeField] float debugPlayerPhaseDelay = 3f;

    [Header("DNA Break Site")]
    [SerializeField] Transform leftDNAEnd;
    [SerializeField] Transform rightDNAEnd;

    public Transform LeftDNAEnd => leftDNAEnd;
    public Transform RightDNAEnd => rightDNAEnd;

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
    public bool DebugBypass => debugBypass;

    int assignedCount;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

        if (debugBypass && debugAutoCompletePlayerPhases && IsServer)
        {
            bool isPlayerPhase = phase == NHEJPhase.Phase3_Trimming
                              || phase == NHEJPhase.Phase4_GapFill
                              || phase == NHEJPhase.Phase6_Ligation;
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

        NHEJPhase next = currentPhase.Value switch
        {
            NHEJPhase.Phase0_Trigger => NHEJPhase.Phase1_KuBinding,
            NHEJPhase.Phase1_KuBinding => NHEJPhase.Phase2_DNAPKcs,
            NHEJPhase.Phase2_DNAPKcs => NHEJPhase.Phase3_Trimming,
            NHEJPhase.Phase3_Trimming => includeGapFill ? NHEJPhase.Phase4_GapFill : NHEJPhase.Phase5_Alignment,
            NHEJPhase.Phase4_GapFill => NHEJPhase.Phase5_Alignment,
            NHEJPhase.Phase5_Alignment => NHEJPhase.Phase6_Ligation,
            NHEJPhase.Phase6_Ligation => NHEJPhase.Phase7_Cleanup,
            NHEJPhase.Phase7_Cleanup => NHEJPhase.Phase8_Assessment,
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

        if (player1PhaseComplete.Value && player2PhaseComplete.Value)
            AdvancePhase();
    }

    #region Trim / Ligation RPCs

    [ServerRpc(RequireOwnership = false)]
    public void ReportTrimServerRpc(int pointIndex, ulong clientId, int pointRole = 0)
    {
        if (currentPhase.Value != NHEJPhase.Phase3_Trimming) return;

        // In debug single-player mode the same clientId holds both roles,
        // so use the point's own assignedPlayerRole instead.
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
        if (handler != null)
        {
            handler.OnTrimConfirmed(pointIndex, clientId);
        }
    }

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
