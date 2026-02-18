using System.Collections;
using UnityEngine;

// Phase 7: Scaffold disassembly + Ku removal animation
// XRCC4/XLF segments detach and float away, VCP/ATPase removes Ku
public class Phase7_Cleanup : NHEJPhaseHandler
{
    [Header("References to Earlier Phases")]
    [SerializeField] Phase1_KuBinding phase1Handler;
    [SerializeField] Phase5_Alignment phase5Handler;

    [Header("VCP/ATPase Prefab")]
    [SerializeField] GameObject vcpAtpasePrefab;
    [SerializeField] Transform vcpSpawnPoint;

    [Header("Repaired DNA")]
    [SerializeField] GameObject repairedDNAVisual;

    [Header("Timing")]
    [SerializeField] float scaffoldDismantleDuration = 2f;
    [SerializeField] float vcpArrivalDuration = 1.5f;
    [SerializeField] float kuRemovalDuration = 2f;
    [SerializeField] float postCleanupPause = 1f;

    Coroutine activeCoroutine;

    public override bool IsAutomatic => true;

    public override void Setup() { }

    public override void StartPhase()
    {
        activeCoroutine = StartCoroutine(RunPhase());
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }

    IEnumerator RunPhase()
    {
        // Dismantle XRCC4/XLF scaffold
        if (phase5Handler != null && phase5Handler.ScaffoldSegments != null)
        {
            float segDelay = scaffoldDismantleDuration / Mathf.Max(1, phase5Handler.ScaffoldSegments.Count);
            foreach (var seg in phase5Handler.ScaffoldSegments)
            {
                if (seg == null) continue;
                var npc = seg.GetComponent<NHEJProteinNPC>();
                if (npc != null)
                {
                    Vector3 dir = (seg.transform.position - (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f).normalized;
                    StartCoroutine(npc.Depart(dir + Vector3.up, 3f, 1.5f));
                }
                yield return new WaitForSeconds(segDelay);
            }
        }
        else
        {
            yield return new WaitForSeconds(scaffoldDismantleDuration);
        }

        // VCP/ATPase arrives to remove Ku
        GameObject vcp = null;
        if (vcpAtpasePrefab != null)
        {
            Vector3 spawnPos = vcpSpawnPoint != null ? vcpSpawnPoint.position : Vector3.up * 2f;
            vcp = Instantiate(vcpAtpasePrefab, spawnPos, Quaternion.identity);
            var vcpNPC = vcp.GetComponent<NHEJProteinNPC>();

            // Move to center of DNA break site
            Vector3 center = (manager.LeftDNAEnd.position + manager.RightDNAEnd.position) * 0.5f;
            if (vcpNPC != null) yield return StartCoroutine(vcpNPC.MoveToTarget(center, vcpArrivalDuration));
        }
        else
        {
            yield return new WaitForSeconds(vcpArrivalDuration);
        }

        // Remove Ku rings
        if (phase1Handler != null)
        {
            if (phase1Handler.LeftKu != null)
            {
                var npc = phase1Handler.LeftKu.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.Depart(Vector3.left + Vector3.up, 4f, kuRemovalDuration));
            }
            if (phase1Handler.RightKu != null)
            {
                var npc = phase1Handler.RightKu.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.Depart(Vector3.right + Vector3.up, 4f, kuRemovalDuration));
            }
        }
        yield return new WaitForSeconds(kuRemovalDuration);

        // VCP leaves
        if (vcp != null)
        {
            var vcpNPC = vcp.GetComponent<NHEJProteinNPC>();
            if (vcpNPC != null) StartCoroutine(vcpNPC.Depart(Vector3.up, 3f, 1f));
        }

        // Show repaired DNA (do we have a repaired DNA Visual?)
        if (repairedDNAVisual != null)
            repairedDNAVisual.SetActive(true);

        yield return new WaitForSeconds(postCleanupPause);

        if (manager != null && manager.IsServer)
        {
            manager.AdvancePhase();
        }
    }
}
