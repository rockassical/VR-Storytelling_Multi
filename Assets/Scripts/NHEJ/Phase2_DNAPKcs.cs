using System.Collections;
using UnityEngine;

// Phase 2: DNA-PKcs docking onto Ku rings + autophosphorylation animation
public class Phase2_DNAPKcs : NHEJPhaseHandler
{
    [Header("DNA-PKcs Prefab")]
    [SerializeField] GameObject dnaPKcsPrefab;

    [Header("Spawn & Target Positions")]
    [SerializeField] Transform leftSpawnPoint;
    [SerializeField] Transform leftDockPoint;
    [SerializeField] Transform rightSpawnPoint;
    [SerializeField] Transform rightDockPoint;

    [Header("Timing")]
    [SerializeField] float dockDuration = 2f;
    [SerializeField] float phosphorylationDuration = 2.5f;

    GameObject leftPKcs;
    GameObject rightPKcs;
    Coroutine activeCoroutine;

    public override bool IsAutomatic => true;

    public GameObject LeftPKcs => leftPKcs;
    public GameObject RightPKcs => rightPKcs;

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
        if (dnaPKcsPrefab != null)
        {
            Vector3 leftStart = leftSpawnPoint != null ? leftSpawnPoint.position : manager.LeftDNAEnd.position + Vector3.up * 2f;
            Vector3 leftTarget = leftDockPoint != null ? leftDockPoint.position : manager.LeftDNAEnd.position + Vector3.up * 0.3f;
            Vector3 rightStart = rightSpawnPoint != null ? rightSpawnPoint.position : manager.RightDNAEnd.position + Vector3.up * 2f;
            Vector3 rightTarget = rightDockPoint != null ? rightDockPoint.position : manager.RightDNAEnd.position + Vector3.up * 0.3f;

            leftPKcs = Instantiate(dnaPKcsPrefab, leftStart, Quaternion.identity);
            rightPKcs = Instantiate(dnaPKcsPrefab, rightStart, Quaternion.identity);

            var leftNPC = leftPKcs.GetComponent<NHEJProteinNPC>();
            var rightNPC = rightPKcs.GetComponent<NHEJProteinNPC>();

            if (leftNPC != null) StartCoroutine(leftNPC.MoveToTarget(leftTarget, dockDuration));
            if (rightNPC != null) StartCoroutine(rightNPC.MoveToTarget(rightTarget, dockDuration));

            yield return new WaitForSeconds(dockDuration);

            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();

            if (leftNPC != null) StartCoroutine(leftNPC.PulseGlow(phosphorylationDuration));
            if (rightNPC != null) StartCoroutine(rightNPC.PulseGlow(phosphorylationDuration));

            yield return new WaitForSeconds(phosphorylationDuration);
        }
        else
        {
            yield return new WaitForSeconds(4.5f);
        }

        if (manager != null && manager.IsServer)
        {
            manager.AdvancePhase();
        }
    }
}
