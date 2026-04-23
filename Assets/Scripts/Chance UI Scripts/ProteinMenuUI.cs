using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// IMPORTANT: The GameObject this script lives on needs a NetworkObject component.
// Tools (Artemis, Ligase, Scanner, Blaster) are scene objects that also need
// NetworkObject + NetworkTransform so the server-driven position replicates to P2.
// DNA piece prefabs need NetworkObject so Spawn() replicates them to all clients.
public class ProteinMenuUI : NetworkBehaviour
{
    public Transform GO_spawnPos;
    [SerializeField] List<GameObject> DNA_Pieces = new List<GameObject>();

    public GameObject Scanner, Blaster, Artimis, Ligase, DNA_Menu;

    public Canvas canvas;
    bool canvasActive = false;
    bool DNA_MenuActive = false;

    public InputActionProperty buttonAction;

    // Cached NetworkObject refs for scene tools.
    NetworkObject scannerNet;
    NetworkObject blasterNet;
    NetworkObject artemisNet;
    NetworkObject ligaseNet;

    private void OnEnable()  { buttonAction.action.Enable(); }
    private void OnDisable() { buttonAction.action.Disable(); }

    private void Awake()
    {
        canvas.enabled = canvasActive;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        scannerNet = Scanner?.GetComponent<NetworkObject>();
        blasterNet = Blaster?.GetComponent<NetworkObject>();
        artemisNet = Artimis?.GetComponent<NetworkObject>();
        ligaseNet  = Ligase?.GetComponent<NetworkObject>();
    }

    void Update()
    {
        if (buttonAction.action.WasPressedThisFrame())
        {
            Debug.Log("Hit B");
            if (DNA_MenuActive) setDNA_menuActive();
            else Menu();
        }
    }

    void Menu()
    {
        canvasActive = !canvasActive;
        canvas.enabled = canvasActive;
    }

    void setDNA_menuActive()
    {
        DNA_MenuActive = !DNA_MenuActive;
        DNA_Menu.SetActive(DNA_MenuActive);
    }

    public void Buttons(int b)
    {
        switch (b)
        {
            case 2: TeleportTool(scannerNet); Menu(); break;
            case 3: TeleportTool(blasterNet); Menu(); break;
            case 4: TeleportTool(artemisNet); Menu(); break;
            case 5: TeleportTool(ligaseNet);  Menu(); break;

            case 6:
                if (DNA_Pieces.Count > 0) setDNA_menuActive();
                else Debug.Log("DNA List is Empty");
                break;

            case 7:  SpawnDNAPiece(0); Menu(); break;
            case 8:  SpawnDNAPiece(1); Menu(); break;
            case 9:  SpawnDNAPiece(2); Menu(); break;
            case 10: SpawnDNAPiece(3); Menu(); break;
            case 11: SpawnDNAPiece(4); Menu(); break;
        }
    }

    // ── Tool teleport ─────────────────────────────────────────────────────────

    void TeleportTool(NetworkObject tool)
    {
        if (tool == null) { Debug.LogWarning("[ProteinMenuUI] Tool has no NetworkObject — add one."); return; }
        TeleportToolServerRpc(tool.NetworkObjectId, GO_spawnPos.position);
    }

    // Server sets the position; NetworkTransform on the tool replicates it to all clients.
    [ServerRpc(RequireOwnership = false)]
    void TeleportToolServerRpc(ulong netObjId, Vector3 position)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netObjId, out var no))
            no.transform.position = position;
        else
            Debug.LogWarning($"[ProteinMenuUI] TeleportTool: NetworkObject {netObjId} not found.");
    }

    // ── DNA piece spawn ───────────────────────────────────────────────────────

    void SpawnDNAPiece(int index)
    {
        if (index >= DNA_Pieces.Count || DNA_Pieces[index] == null) { Debug.Log("DNA piece null/missing"); return; }
        SpawnDNAPieceServerRpc(index, GO_spawnPos.position);
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnDNAPieceServerRpc(int index, Vector3 position)
    {
        if (index >= DNA_Pieces.Count || DNA_Pieces[index] == null) return;
        var go = Instantiate(DNA_Pieces[index], position, Quaternion.identity);
        var no = go.GetComponent<NetworkObject>();
        if (no != null) no.Spawn();
        else Debug.LogWarning($"[ProteinMenuUI] DNA_Pieces[{index}] has no NetworkObject — P2 won't see it.");
    }
}
