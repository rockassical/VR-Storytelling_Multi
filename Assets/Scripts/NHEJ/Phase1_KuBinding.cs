using System.Collections;
using UnityEngine;


// Phase 1: Ku70/80 NPC fly-in animation. Two Ku rings slide onto each DNA end. Currently yellow Spheres.
public class Phase1_KuBinding : NHEJPhaseHandler
{
    [Header("Ku70/80 Prefab")]
    [SerializeField] GameObject ku7080Prefab;

    [Header("Spawn & Target Positions")]
    [SerializeField] Transform leftSpawnPoint;
    [SerializeField] Transform leftTargetPoint;
    [SerializeField] Transform rightSpawnPoint;
    [SerializeField] Transform rightTargetPoint;

    [Header("Timing")]
    [SerializeField] float flyInDuration = 2f;
    [SerializeField] float snapPause = 0.5f;
    [SerializeField] float glowDuration = 1.5f;

    GameObject leftKu;
    GameObject rightKu;
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
        // Spawn Ku proteins
        if (ku7080Prefab != null)
        {
            Vector3 leftStart = leftSpawnPoint != null ? leftSpawnPoint.position : manager.LeftDNAEnd.position + Vector3.left * 2f;
            Vector3 leftTarget = leftTargetPoint != null ? leftTargetPoint.position : manager.LeftDNAEnd.position;
            Vector3 rightStart = rightSpawnPoint != null ? rightSpawnPoint.position : manager.RightDNAEnd.position + Vector3.right * 2f;
            Vector3 rightTarget = rightTargetPoint != null ? rightTargetPoint.position : manager.RightDNAEnd.position;

            leftKu = Instantiate(ku7080Prefab, leftStart, Quaternion.identity);
            rightKu = Instantiate(ku7080Prefab, rightStart, Quaternion.identity);

            var leftNPC = leftKu.GetComponent<NHEJProteinNPC>();
            var rightNPC = rightKu.GetComponent<NHEJProteinNPC>();

            // Animate fly-in
            if (leftNPC != null) StartCoroutine(leftNPC.MoveToTarget(leftTarget, flyInDuration));
            if (rightNPC != null) StartCoroutine(rightNPC.MoveToTarget(rightTarget, flyInDuration));

            yield return new WaitForSeconds(flyInDuration);

            // Snap effect
            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();
            yield return new WaitForSeconds(snapPause);

            // Glow
            if (leftNPC != null) leftNPC.SetGlow(true);
            if (rightNPC != null) rightNPC.SetGlow(true);
            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlayGlow();
            yield return new WaitForSeconds(glowDuration);
        }
        else
        {
            yield return new WaitForSeconds(3.5f);
        }

        if (manager != null && manager.IsServer)
        {
            manager.AdvancePhase();
        }
    }

    public GameObject LeftKu => leftKu;
    public GameObject RightKu => rightKu;
}
