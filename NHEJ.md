# NHEJ Interaction & Networking — Implementation Status

## Project Location
`C:\Users\olive\StudioFallingDebris\VR-Storytelling_Multi`

## Unity Version & Key Packages
- **Unity 2022.3.57f1** (NOT Unity 6 — use `rb.velocity` not `rb.linearVelocity`)
- **XR Interaction Toolkit 3.0.5** (namespaces: `UnityEngine.XR.Interaction.Toolkit.Interactables`, `.Interactors`)
- **Netcode for GameObjects (NGO)** — version ~1.9-1.12 range
- **Unity Gaming Services**: Lobby, Relay, Vivox, Authentication
- **VRMP Assembly**: `Assets/VRMPAssets/VRMP.asmdef` (autoReferenced=true, namespace `XRMultiplayer`)

## What's Done (Scripts — ALL COMPLETE)

All 23 C# scripts in **`Assets/Scripts/NHEJ/`** (single folder, no assembly def — compiles in Assembly-CSharp, can reference VRMP types via `using XRMultiplayer`):

### Core Framework (3 files)
| File | Purpose |
|---|---|
| `NHEJPhase.cs` | Enum: WaitingForPlayers, Phase0_Trigger through Phase8_Assessment, Complete (11 values) |
| `NHEJPhaseHandler.cs` | Abstract base: Setup(), StartPhase(), UpdatePhase(), CompletePhase(), virtual IsAutomatic |
| `NHEJManager.cs` | NetworkBehaviour singleton — state machine, player roles, phase transitions, trim/ligation RPCs |

### Support Scripts (9 files)
| File | Purpose |
|---|---|
| `NHEJProteinNPC.cs` | Drives protein movement: MoveToTarget(), SetGlow(), PulseGlow(), Depart() coroutines |
| `NHEJTool.cs` | Extends `NetworkBaseInteractable` + requires `XRGrabInteractable`. `Activated()` is a no-op — both tool types use placement-on-release, not trigger |
| `DNAWallSegment.cs` | NetworkObject + XRGrabInteractable. Pre-placed in scene at broken positions. Has `correctPlacementMarker` child Transform. Sealed by `LigaseSprayCan`; records placement score at seal time. |
| `LigaseSprayCan.cs` | Extends `NetworkBaseInteractable`. Spawned by Phase4. Trigger-activated spray can: seals nearest unsealed `DNAWallSegment` within range. Drifts back to spawn position when released. |
| `ArtemisOrbitController.cs` | NetworkBehaviour on Artemis prefab. Orbits DNA midpoint (circular path). Pauses when grabbed; on release snaps to nearest active `TrimPoint` within `snapRadius` and fires `OnToolActivated`. Owner-driven position, synced via NetworkTransform |
| `LigaseOrbitController.cs` | Same pattern as `ArtemisOrbitController` but targets `LigationPoint` objects and checks `lp.IsSealed` |
| `ProteinOrbitController.cs` | Generic orbit+grab+placement for interactive protein phases (0,1,2,5,7). Configure() sets role+snapPos before Spawn(). On release within snapRadius: ConfirmPlacementClientRpc (snap+glow+OnProteinPlacedLocal) + ReportProteinPickup (server routes to phase handler). |
| `ProteinPlacementPoint.cs` | Editor-only scene marker (playerRole 1 or 2) showing where to place each protein. Referenced by phase handlers for snap positions. |
| `TrimPoint.cs` | MonoBehaviour on DNA cut points. Validates player role, calls ReportTrimServerRpc, detaches nucleotide fragment on trim |
| `LigationPoint.cs` | MonoBehaviour on nick points. Validates player role, calls ReportLigationServerRpc, shows seal VFX |
| `NHEJAudio.cs` | Singleton audio manager. Serialized AudioClip fields for SFX + Alysia dialogue per phase |

### Phase Handlers (9 files)
| File | Type | Interaction |
|---|---|---|
| `Phase0_Trigger.cs` | **Player** | Alysia dialogue plays; 2 "ready" proteins orbit. Both players pick up and place at their target to confirm ready → advance |
| `Phase1_KuBinding.cs` | **Player** | 2 Ku70/80 proteins orbit. P1 places at left DNA end, P2 at right. Snap+glow on placement. Both placed → advance |
| `Phase2_DNAPKcs.cs` | **Player** | 2 DNA-PKcs orbit. P1 docks left side, P2 right side. PulseGlow (autophosphorylation) on placement. Both placed → advance |
| `Phase3_Trimming.cs` | **Player** | Spawns ONE server-owned Artemis that orbits DNA. Players grab it as it passes, place it at their TrimPoint. Phase auto-advances when both ends are trimmed |
| `Phase4_GapFill.cs` | **Player** | Show baseline → reveal broken segments → spawn LigaseIV spray can(s) → players grab & place DNAWallSegment objects → spray to seal → score by accuracy |
| `Phase5_Alignment.cs` | **Player** | Auto: PKcs drift away. Then 2 XRCC4/XLF proteins orbit. P1 places at scaffold start, P2 at scaffold end. Both placed → scaffold builds + DNA ends align locally. Server advances after animation |
| `Phase6_Ligation.cs` | **Player** | Spawns ONE server-owned Ligase that orbits DNA. Players grab it as it passes, place it at their LigationPoint. Phase auto-advances when both nicks are sealed |
| `Phase7_Cleanup.cs` | **Player** | 2 VCP/ATPase proteins orbit. P1 places on left Ku, P2 on right. Per-player Ku departure animation. Both placed → scaffold dismantles, repaired DNA reveals. Server advances after delay |
| `Phase8_Assessment.cs` | Auto | Score display, educational summary text, Alysia dialogue |

## What's NOT Done (Requires Unity Editor)

### 1. Scene Setup — "NHEJ Interaction" scene
- **Action**: Duplicate `Assets/_Scenes/Devon_Interaction.unity` → rename to `NHEJ Interaction.unity`
- **Strip out**: RepairManager, sockets, Scanner, existing DNA models, LLM Communication group, Pyramid prefab
- **Keep**: Environment (walls/floor/light), XR Origin prefab, EventSystem, Network Manager
- **Add**: Empty GameObject "---NHEJ Manager---" with `NHEJManager` component
- **Add**: Empty GameObject "---NHEJ Audio---" with `NHEJAudio` component + 2 AudioSources (SFX + Dialogue)
- **Add**: DNA break site objects at interaction zone (~(-0.1, 0.45, 0.82)):
  - Two separated DNA strand end GameObjects (left + right halves)
  - Assign to NHEJManager's `leftDNAEnd` / `rightDNAEnd` serialized fields
- **Register scene in Build Settings**

### 2. Visual-Only Protein Prefabs — `Assets/_Prefabs/NHEJ/`
These are local (non-networked), driven by `NHEJProteinNPC` for animations:

| Prefab | Used By | Visual | Color |
|---|---|---|---|
| `XRCC4_XLF_Segment.prefab` | Phase5 scaffold build (after both placed) | Elongated capsule | Cyan |

### 3. Interactive Protein Prefabs — `Assets/_Prefabs/NHEJ/`
Every phase with player interaction needs prefabs with:
`NetworkObject` + `NetworkTransform` + `XRGrabInteractable` + `ProteinOrbitController` + `NHEJProteinNPC` + `SphereCollider`
(No `NHEJTool` needed — `ProteinOrbitController` handles ownership transfer directly.)

| Prefab | Phase | Visual | Color |
|---|---|---|---|
| `Phase0_ReadyToken.prefab` | 0 — both players | Small sphere | White/cyan |
| `Ku70Ku80_Interactive.prefab` | 1 — both players | Torus mesh | Gold/bronze |
| `DNAPKcs_Interactive.prefab` | 2 — both players | Hemisphere | Blue-gray |
| `XRCC4XLF_Interactive.prefab` | 5 — both players | Capsule | Cyan |
| `VCP_ATPase_Interactive.prefab` | 7 — both players | Cylinder | Orange |
| `Artemis.prefab` | 3 — shared | Small sphere | Red — uses `ArtemisOrbitController` |
| `LigaseIV.prefab` | 6 — shared | Small sphere | Green — uses `LigaseOrbitController` |

**All** interactive prefabs must be registered in the NetworkManager's NetworkPrefabs list.
`ProteinOrbitController` inspector fields: `orbitRadius` (0.35m), `orbitSpeed` (45°/s), `orbitHeight` (0.05m), `snapRadius` (0.20m) — tune per prefab.
All spawned server-owned — ownership transfers to the grabbing player via `NHEJTool` → `NetworkBaseInteractable`.

### 4. Interaction Point Prefabs — `Assets/_Prefabs/NHEJ/`
| Prefab | Visual | Script | Notes |
|---|---|---|---|
| `TrimPoint.prefab` | Red glowing sphere + child "cut line" | `TrimPoint.cs` | SphereCollider (trigger), assign pointIndex + assignedPlayerRole in inspector |
| `LigationPoint.prefab` | Blue glowing sphere + child "nick indicator" | `LigationPoint.cs` | SphereCollider (trigger), assign pointIndex + assignedPlayerRole |
| `NucleotideFragment.prefab` | Small colored cube | (none — just a visual) | Rigidbody gets added at runtime by TrimPoint |

### 5. Wire Up Inspector References
On the "---NHEJ Manager---" GameObject:
- `NHEJManager.phaseHandlers[]` array (size 9): assign Phase0 through Phase8 handler components (can be on same or child GameObjects)
- `NHEJManager.leftDNAEnd` / `rightDNAEnd`: assign the two DNA strand transforms

On each Phase handler component:
- Phase0: assign `pickupProteinPrefab` (Phase0_ReadyToken), `player1PlacementTarget`, `player2PlacementTarget`
- Phase1: assign `kuPickupPrefab` (Ku70Ku80_Interactive), `leftDNATarget`, `rightDNATarget`
- Phase2: assign `dnaPKcsPickupPrefab` (DNAPKcs_Interactive), `leftDockTarget`, `rightDockTarget`
- Phase3: assign `artemisToolPrefab`, TrimPoint arrays (`player1TrimPoints` = left end, `player2TrimPoints` = right end)
- Phase4: assign gap socket GameObjects, nucleotide prefab
- Phase5: assign `phase2Handler` reference, `xrcc4PickupPrefab` (XRCC4XLF_Interactive), `xrcc4XlfSegmentPrefab` (visual-only), scaffold start/end transforms, align targets
- Phase6: assign `ligaseToolPrefab`, LigationPoint arrays (`player1LigationPoints` = strand 1, `player2LigationPoints` = strand 2)
- Phase7: assign `phase1Handler` + `phase5Handler` references, `vcpPickupPrefab` (VCP_ATPase_Interactive), `leftKuTarget`, `rightKuTarget`, `repairedDNAVisual`
- Phase8: assign UI panel, TMP_Text fields, DNA comparison models

**Placement target Transforms** (Phases 0, 1, 2, 5, 7) should have a `ProteinPlacementPoint` component for in-editor gizmo visualization. The `playerRole` on each matches the player who should place there.

## Architecture Quick Reference

### State Machine Flow (current — simplified 2025-03-17)
```
WaitingForPlayers → (2 players join) → Phase0 → Phase3 (Artemis trim) → Phase4 (gap fill) → Phase8 → Complete
```
Phases 1, 2, 5, 6, 7 are **bypassed** in `NHEJManager.AdvancePhase()` but code is preserved for easy reversion.

### Phase 4 Gap Fill — Detail
```
Server shows baselineDNAVisual (intact target) for ~4s
  → hides baseline, reveals DNAWallSegment scene objects at broken positions
  → spawns LigaseIV spray can(s)
  → players grab & reposition wall segments (no snap; yellow glow hints proximity)
  → player pulls trigger on LigaseSprayCan near a segment to seal it
  → score per segment = 1 - (distance_from_correct / 0.25m), clamped 0–1
  → all sealed → average score → Phase8_Assessment
```

### Networking Pattern
- **Server owns all state**: `NetworkVariable<NHEJPhase>` with server-write
- **Phase changes**: `currentPhase.OnValueChanged` fires on all clients → each client runs local handler
- **Auto phases**: Server runs coroutine timing, calls `AdvancePhase()` directly
- **Player phases**: Both `player1PhaseComplete` + `player2PhaseComplete` must be true → server advances
- **Tool orbit**: Tool spawns server-owned. Orbit runs in `Update()` gated by `IsOwner` — server drives it until grabbed. `NetworkBaseInteractable` transfers ownership to the grabbing client; they drive position during hold. On release, placement check fires locally → `[ServerRpc]` → server validates → `[ClientRpc]` broadcasts result to all clients
- **Tool placement (Phase 3/6)**: No trigger press needed. Player releases near interaction point → `ArtemisOrbitController` / `LigaseOrbitController` fires `OnToolActivated(LocalClientId)` → ServerRpc/ClientRpc chain
- **Protein pickup (Phases 0/1/2/5/7)**: `ProteinOrbitController` — release within `snapRadius` of `snapTargetPosition` → `PlaceProteinServerRpc` → `ConfirmPlacementClientRpc` (snap + glow + `OnProteinPlacedLocal` on all clients) → `NHEJManager.ReportProteinPickup` → phase handler's `OnProteinPlaced` (server-only)
- **`NHEJPhaseHandler.OnProteinPlaced(role)`** (server): default calls `ServerMarkPlayerComplete`. Override in Phase5/Phase7 for deferred advance after animations.
- **`NHEJPhaseHandler.OnProteinPlacedLocal(role)`** (all clients): override for per-player visual/audio cues (Phase2 PulseGlow, Phase7 Ku departure, Phase5 scaffold build).
- **`NHEJManager.OnPlayerRoleMarkedComplete`** event: fires on all clients via ClientRpc from `ServerMarkPlayerComplete`. Subscribe in phase handlers for additional networked callbacks.

### Player Roles
- Player 1 (first to join) = p53 role — trims LEFT end, seals strand 1 nick
- Player 2 (second to join) = ATM role — trims RIGHT end, seals strand 2 nick
- Role assigned via `XRINetworkGameManager.Instance.playerStateChanged` event

### Key Base Classes Used
- `NetworkBaseInteractable` (namespace `XRMultiplayer`) — NHEJTool extends this for ownership transfer, grab sync, Activated() override
- `MiniGameManager` pattern — referenced for NetworkVariable state machine design (but NOT extended)
- `DNARepairPlacement` — referenced for socket-fill pattern in Phase4

## Key Files to Reference (DO NOT modify)
| File | Path | Why |
|---|---|---|
| XRINetworkGameManager | `VRMPAssets/Scripts/Network/NetworkManagers/XRINetworkGameManager.cs` | Singleton, player tracking, GetPlayerByID() |
| MiniGameManager | `VRMPAssets/MiniGames/MiniGameScripts/MiniGameManager.cs` | Pattern reference for NetworkVariable state machine |
| NetworkBaseInteractable | `VRMPAssets/Scripts/Network/NetworkInteractions/NetworkBaseInteractable.cs` | Base class for NHEJTool |
| NetworkPhysicsInteractable | `VRMPAssets/Scripts/Network/NetworkInteractions/NetworkPhysicsInteractable.cs` | Reference for physics interactable pattern |
| XRINetworkPlayer | `VRMPAssets/Scripts/Network/NetworkPlayer/XRINetworkPlayer.cs` | LocalPlayer static ref |
| DNARepairPlacement | `Assets/Scripts/Interactions/DNARepairPlacement.cs` | Socket-fill pattern reference |
| Devon_Interaction | `Assets/_Scenes/Devon_Interaction.unity` | Scene layout template (copy, don't modify) |

## Existing Project Scripts (outside NHEJ)
Located in `Assets/Scripts/` (no namespace, no asmdef):
- `DNARepairPlacement.cs`, `TemplateScanner.cs` (in Interactions/)
- `TalkToAI.cs` (in LLM Conversation/)
- `WorldShake.cs`, plus some `-Copy` files

## VRMP Assembly Info
- `Assets/VRMPAssets/VRMP.asmdef` — autoReferenced=true
- Namespace: `XRMultiplayer`, sub-namespace `XRMultiplayer.MiniGames`
- Our scripts in `Assets/Scripts/NHEJ/` are NOT in any asmdef → compile in Assembly-CSharp → can `using XRMultiplayer;`

## Gotchas / Lessons
- **Unity 2022.3, NOT Unity 6**: Use `rb.velocity` not `rb.linearVelocity`
- **XRI 3.0.5 namespaces**: Types are in `UnityEngine.XR.Interaction.Toolkit.Interactables` and `.Interactors` (not old flat namespace)
- **NetworkBaseInteractable requires XRBaseInteractable** on same GameObject. NHEJTool also requires XRGrabInteractable (which IS an XRBaseInteractable, so no conflict)
- **Tool prefabs MUST be registered** in NetworkManager's NetworkPrefabs list or `Spawn()` will fail
- **Phase handlers are assigned in inspector array** indexed 0-8 (Phase0=index0, Phase8=index8)
- **Cross-phase references** (Phase5→Phase2, Phase7→Phase1+Phase5) are serialized inspector refs, not runtime lookups
- **Orbit controllers find interaction points via `FindObjectsOfType`** called in `OnNetworkSpawn`. Inactive GameObjects are excluded, so TrimPoints/LigationPoints deactivated by the scenario check in Phase3/Phase6 are automatically filtered out. Timing is safe: phase handlers run `SetActive` before `Spawn()` on server; clients receive the phase-change RPC before the tool spawn message
- **`NHEJTool.Activated()` is a no-op** — do not add trigger-based detection back. Both tool types use release-to-place via their orbit controllers

## Verification Plan (when scene is ready)
1. Open project in Unity 2022.3.57f1, confirm no compile errors
2. Single-player: Enter NHEJ scene in Play mode, verify phase state machine advances through all auto phases
3. Two-player: Build+run two instances, connect to same lobby, confirm:
   - First player auto-assigned p53, second ATM
   - Phase 3: Artemis orbits DNA; each player can grab it and only successfully place it at their assigned end; wrong end is rejected by role check
   - Phase 6: Ligase orbits DNA; same placement mechanic; wrong nick rejected
   - Phase transitions sync across both clients
4. Tune orbit feel: adjust `orbitRadius`, `orbitSpeed`, `snapRadius` on both tool prefabs in the inspector
5. Edge cases: player disconnect mid-phase, late joiner
