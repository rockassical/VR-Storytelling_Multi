using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Artemis trimmer blade.
// Grab it with the VR controller. When the blade tip trigger enters a DNAPair
// segment that is glowing red (overhang), it cuts it — the segment falls away
// and is destroyed after a short delay.
//
// Setup:
//   - Add a child GameObject "BladeTip" with a Trigger Collider covering the cutting edge.
//   - Assign that child to the Blade Tip field, OR leave it empty to use this object's collider.
//   - The DNA wall segments must have Colliders on them (or their children).
[RequireComponent(typeof(XRGrabInteractable))]
public class NHEJTool : MonoBehaviour
{
    [Header("Tool Settings")]
    [Tooltip("Child transform whose trigger collider is the cutting edge. Leave empty to use this object.")]
    [SerializeField] Transform bladeTip;
    [Tooltip("Seconds before a cut segment is destroyed.")]
    [SerializeField] float despawnDelay = 2f;
    [Tooltip("Force applied to the cut segment as it falls away.")]
    [SerializeField] float cutForce = 1.5f;

    [Header("Debug")]
    [SerializeField] bool debugMode = false;

    // ── Collision ─────────────────────────────────────────────────────────────
    // The trigger must be on the bladeTip child (or this GameObject).
    // Unity routes OnTriggerEnter to the script on the same GameObject as the collider,
    // so we use a small helper component on the bladeTip to forward events here.

    void Start()
    {
        Transform tip = bladeTip != null ? bladeTip : transform;

        // Attach forwarder to whichever object owns the trigger collider.
        var forwarder = tip.gameObject.GetComponent<BladeTipForwarder>();
        if (forwarder == null)
            forwarder = tip.gameObject.AddComponent<BladeTipForwarder>();
        forwarder.Init(this);
    }

    // Called by BladeTipForwarder when it detects a trigger enter.
    public void OnBladeContact(Collider other)
    {
        // Walk up to find a DNAPair on this object or its parents.
        DNAPair pair = other.GetComponentInParent<DNAPair>();
        if (pair == null) return;
        if (!pair.IsOverhang) return;

        if (debugMode)
            Debug.Log($"[NHEJTool] Cutting overhang: {pair.gameObject.name}");

        CutSegment(pair.gameObject);
    }

    // ── Cut ───────────────────────────────────────────────────────────────────

    void CutSegment(GameObject seg)
    {
        // Clear overhang state immediately so phase checks don't count this segment.
        var pair = seg.GetComponent<DNAPair>();
        if (pair != null) pair.enabled = false;

        // Detach from parent so physics doesn't drag siblings.
        seg.transform.SetParent(null);

        // Add physics and fling it away.
        var rb = seg.GetComponent<Rigidbody>() ?? seg.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity  = true;

        Vector3 fallDir = (seg.transform.position - transform.position).normalized + Vector3.down * 0.5f;
        rb.AddForce(fallDir.normalized * cutForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 1.5f, ForceMode.Impulse);

        // Disable any grab interactable so it can't be picked up while falling.
        var grab = seg.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        Destroy(seg, despawnDelay);

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayLigationSuccess();

        StartCoroutine(CheckAllOverhangsCut());
    }

    IEnumerator CheckAllOverhangsCut()
    {
        // Wait a frame so the DNAPair component on the cut segment is gone.
        yield return null;

        foreach (var pair in FindObjectsOfType<DNAPair>())
            if (pair.enabled && pair.IsOverhang) yield break; // still overhangs remaining

        // All overhangs cut — advance the phase.
        NHEJManager.Instance?.AdvancePhase();
    }

    void OnGUI()
    {
        if (!debugMode) return;
        GUILayout.BeginArea(new Rect(10, 340, 200, 40));
        GUILayout.Label("Artemis: swing blade at red segments");
        GUILayout.EndArea();
    }
}

// ── Forwarder ─────────────────────────────────────────────────────────────────
// Sits on the bladeTip child and routes OnTriggerEnter up to NHEJTool.
// Added at runtime — no manual setup needed.
public class BladeTipForwarder : MonoBehaviour
{
    NHEJTool owner;
    public void Init(NHEJTool tool) => owner = tool;

    void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnBladeContact(other);
    }
}
