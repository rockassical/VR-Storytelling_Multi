using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Setup requirements:
//   - This GameObject needs a NetworkObject component.
//   - All tool and DNA piece prefabs need a NetworkObject component.
//   - All prefabs must be registered in NetworkManager → Network Prefabs.
public class ProteinMenuUI : NetworkBehaviour
{
    public Transform GO_spawnPos;

    [Header("Tool Prefabs (spawned on button press)")]
    public GameObject ScannerPrefab;
    public GameObject BlasterPrefab;
    public GameObject ArtimisPrefab;
    public GameObject LigasePrefab;

    [Header("DNA Piece Prefabs")]
    [SerializeField] List<GameObject> DNA_Pieces = new List<GameObject>();

    [Header("UI")]
    public GameObject DNA_Menu;
    public Canvas canvas;

    public InputActionProperty buttonAction;

    bool canvasActive   = false;
    bool DNA_MenuActive = false;

    [SerializeField] public bool separateTasks;

    private void OnEnable()  { buttonAction.action.Enable(); }
    private void OnDisable() { buttonAction.action.Disable(); }

    private void Awake() { canvas.enabled = canvasActive; }

    void Update()
    {
        if (buttonAction.action.WasPressedThisFrame())
        {
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
            case 2: SpawnTool(ScannerPrefab); Menu(); break;
            case 3: 
                if(separateTasks && !IsServer){
                    SpawnTool(BlasterPrefab);  
                    Menu();
                }else if(!separateTasks){
                    SpawnTool(BlasterPrefab);  
                    Menu();
                }
                break;
            case 4: SpawnTool(ArtimisPrefab);  Menu(); break;
            case 5: 
                if(separateTasks && !IsServer){
                    SpawnTool(LigasePrefab);   
                    Menu(); 
                }else if(!separateTasks){
                    SpawnTool(LigasePrefab);   
                    Menu(); 
                }
                break;
            case 6:
                if (DNA_Pieces.Count > 0) setDNA_menuActive();
                else Debug.Log("DNA List is Empty");
                break;

            case 7:  
                if(separateTasks && IsServer){    
                    SpawnDNAPiece(0); 
                    Menu();
                }else if(!separateTasks){
                    SpawnDNAPiece(0); 
                    Menu();
                }
                break;
            case 8:
                if(separateTasks && IsServer){    
                    SpawnDNAPiece(1); 
                    Menu(); 
                }else if(!separateTasks){
                    SpawnDNAPiece(1); 
                    Menu(); 
                }
                break;
            case 9:  
                if(separateTasks && IsServer){    
                    SpawnDNAPiece(2); 
                    Menu(); 
                }else if(!separateTasks){
                    SpawnDNAPiece(2); 
                    Menu();
                }
                break;
            case 10: 
                if(separateTasks && IsServer){    
                    SpawnDNAPiece(3); 
                    Menu(); 
                }else if(!separateTasks){
                    SpawnDNAPiece(3); 
                    Menu();
                }
                break;
            case 11:
                if(separateTasks && IsServer){    
                    SpawnDNAPiece(4); 
                    Menu(); 
                }else if(!separateTasks){
                    SpawnDNAPiece(4); 
                    Menu(); 
                }
                break;
        }
    }

    // ── Tool spawning ─────────────────────────────────────────────────────────

    void SpawnTool(GameObject prefab)
    {
        if (prefab == null) { Debug.LogWarning("[ProteinMenuUI] Tool prefab not assigned."); return; }
        SpawnPrefabServerRpc(GetPrefabIndex(prefab), GO_spawnPos.position);
    }

    // ── DNA piece spawning ────────────────────────────────────────────────────

    void SpawnDNAPiece(int index)
    {
        if (index >= DNA_Pieces.Count || DNA_Pieces[index] == null) { Debug.Log("DNA piece null/missing"); return; }
        SpawnDNAPieceServerRpc(index, GO_spawnPos.position);
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnDNAPieceServerRpc(int index, Vector3 position)
    {
        if (index >= DNA_Pieces.Count || DNA_Pieces[index] == null) return;
        NetworkSpawn(DNA_Pieces[index], position);
    }

    // ── Shared tool spawn RPC ─────────────────────────────────────────────────

    // Tools are identified by index rather than passing a GameObject over the network.
    int GetPrefabIndex(GameObject prefab)
    {
        if (prefab == ScannerPrefab) return 0;
        if (prefab == BlasterPrefab) return 1;
        if (prefab == ArtimisPrefab) return 2;
        if (prefab == LigasePrefab)  return 3;
        return -1;
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnPrefabServerRpc(int toolIndex, Vector3 position)
    {
        GameObject prefab = toolIndex switch
        {
            0 => ScannerPrefab,
            1 => BlasterPrefab,
            2 => ArtimisPrefab,
            3 => LigasePrefab,
            _ => null
        };

        if (prefab == null) return;
        NetworkSpawn(prefab, position);
    }

    void NetworkSpawn(GameObject prefab, Vector3 position)
    {
        var go = Instantiate(prefab, position, Quaternion.identity);
        var no = go.GetComponent<NetworkObject>();

        if (no == null)
        {
            // NetworkObject might be on a child — NGO requires it on the root.
            var child = go.GetComponentInChildren<NetworkObject>();
            string hint = child != null
                ? $"NetworkObject found on child '{child.gameObject.name}' — move it to the root prefab GameObject."
                : "NetworkObject component is missing — add it to the root and register the prefab in NetworkManager.";
            Debug.LogError($"[ProteinMenuUI] '{prefab.name}': {hint}");
            Destroy(go);
            return;
        }

        no.Spawn();
    }
}
