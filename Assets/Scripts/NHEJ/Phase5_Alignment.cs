using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Phase 5: DNA-PKcs open & drift away, XRCC4/XLF scaffold spawns as bridge, DNA ends pull together
public class Phase5_Alignment : NHEJPhaseHandler
{
    [Header("References to Earlier Phase Objects")]
    [SerializeField] Phase2_DNAPKcs phase2Handler;

    [Header("XRCC4/XLF Scaffold")]
    [SerializeField] GameObject xrcc4XlfSegmentPrefab;
    [SerializeField] int scaffoldSegmentCount = 4;
    [SerializeField] Transform scaffoldStartPoint;
    [SerializeField] Transform scaffoldEndPoint;

    [Header("DNA End Movement")]
    [SerializeField] Transform leftEndAlignTarget;
    [SerializeField] Transform rightEndAlignTarget;
    [SerializeField] float alignDuration = 2f;

    [Header("Timing")]
    [SerializeField] float pkcsDepartDuration = 1.5f;
    [SerializeField] float scaffoldBuildDuration = 2f;
    [SerializeField] float postAlignPause = 1f;

    readonly List<GameObject> scaffoldSegments = new();
    Coroutine activeCoroutine;

    public override bool IsAutomatic => true;
    public List<GameObject> ScaffoldSegments => scaffoldSegments;

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
        // DNA-PKcs drift away
        if (phase2Handler != null)
        {
            if (phase2Handler.LeftPKcs != null)
            {
                var npc = phase2Handler.LeftPKcs.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.Depart(Vector3.up + Vector3.left, 3f, pkcsDepartDuration));
            }
            if (phase2Handler.RightPKcs != null)
            {
                var npc = phase2Handler.RightPKcs.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.Depart(Vector3.up + Vector3.right, 3f, pkcsDepartDuration));
            }
        }
        yield return new WaitForSeconds(pkcsDepartDuration);

        // Build XRCC4/XLF scaffold bridge
        if (xrcc4XlfSegmentPrefab != null && scaffoldSegmentCount > 0)
        {
            Vector3 start = scaffoldStartPoint != null ? scaffoldStartPoint.position : manager.LeftDNAEnd.position;
            Vector3 end = scaffoldEndPoint != null ? scaffoldEndPoint.position : manager.RightDNAEnd.position;

            float segmentDelay = scaffoldBuildDuration / scaffoldSegmentCount;

            for (int i = 0; i < scaffoldSegmentCount; i++)
            {
                float t = (float)i / (scaffoldSegmentCount - 1);
                Vector3 pos = Vector3.Lerp(start, end, t);
                var seg = Instantiate(xrcc4XlfSegmentPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
                scaffoldSegments.Add(seg);

                var npc = seg.GetComponent<NHEJProteinNPC>();
                if (npc != null) StartCoroutine(npc.MoveToTarget(pos, segmentDelay * 0.8f));

                yield return new WaitForSeconds(segmentDelay);
            }

            if (NHEJAudio.Instance != null) NHEJAudio.Instance.PlaySnap();
        }

        // Pull DNA ends together
        if (manager.LeftDNAEnd != null && leftEndAlignTarget != null)
            StartCoroutine(MoveTransform(manager.LeftDNAEnd, leftEndAlignTarget.position, alignDuration));
        if (manager.RightDNAEnd != null && rightEndAlignTarget != null)
            StartCoroutine(MoveTransform(manager.RightDNAEnd, rightEndAlignTarget.position, alignDuration));

        yield return new WaitForSeconds(alignDuration + postAlignPause);

        if (manager != null && manager.IsServer)
        {
            manager.AdvancePhase();
        }
    }

    IEnumerator MoveTransform(Transform t, Vector3 target, float duration)
    {
        Vector3 start = t.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            t.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        t.position = target;
    }
}
