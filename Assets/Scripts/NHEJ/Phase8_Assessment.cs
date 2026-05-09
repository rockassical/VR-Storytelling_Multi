using System.Collections;
using UnityEngine;
using TMPro;

// Phase 8: Assessment — displays before/after DNA comparison, score, educational summary.
public class Phase8_Assessment : NHEJPhaseHandler
{
    [Header("UI")]
    [SerializeField] GameObject assessmentPanel;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text summaryText;
    [SerializeField] TMP_Text phaseTitle;

    [Header("DNA Comparison")]
    [SerializeField] GameObject originalDNAModel;
    [SerializeField] GameObject repairedDNAModel;

    [Header("Scoring")]
    [SerializeField] float maxScore = 100f;

    [Header("Timing")]
    [SerializeField] float displayDuration = 15f;
    [SerializeField] bool autoAdvance = true;

    Coroutine activeCoroutine;
    float startTime;

    public override bool IsAutomatic => true;

    public override void Setup()
    {
        startTime = Time.time;
    }

    public override void StartPhase()
    {
        float totalTime = Time.time - startTime;

        // Show assessment panel
        if (assessmentPanel != null)
            assessmentPanel.SetActive(true);

        if (phaseTitle != null)
            phaseTitle.text = "NHEJ Repair Complete";

        // Scores from each phase
        float placementScore = NHEJManager.Instance != null
            ? NHEJManager.Instance.GapFillScore : maxScore;
        float trimScore = NHEJManager.Instance != null
            ? NHEJManager.Instance.TrimmingScore * 100f : 100f;

        // Weighted: 60% gap fill accuracy + 25% trim precision + 15% time bonus
        float timeBonus  = Mathf.Clamp(15f - totalTime * 0.08f, 0f, 15f);
        float finalScore = Mathf.Clamp(placementScore * 0.6f + trimScore * 0.25f + timeBonus, 0f, maxScore);

        string trimRating = trimScore >= 80f ? "Clean cut" :
                            trimScore >= 50f ? "Some waste" : "Excessive trim";

        string placementRating = placementScore >= 80f ? "Excellent" :
                                 placementScore >= 60f ? "Good" :
                                 placementScore >= 40f ? "Fair" : "Needs Work";

        if (scoreText != null)
            scoreText.text = $"Score: {finalScore:F0} / {maxScore:F0}\n" +
                             $"Trim precision:    {trimScore:F0}%  ({trimRating})\n" +
                             $"Placement accuracy: {placementScore:F0}%  ({placementRating})\n" +
                             $"Time: {totalTime:F1}s";

        if (summaryText != null)
        {
            summaryText.text =
                "Non-Homologous End Joining (NHEJ) is the primary pathway for repairing " +
                "DNA double-strand breaks in human cells.\n\n" +
                "Steps completed in this session:\n" +
                "1. Artemis trimmed the incompatible overhang from the broken end\n" +
                "2. DNA wall segments were manually repositioned to bridge the gap\n" +
                "3. LigaseIV sealed each segment in place, locking the repair\n\n" +
                "The closer your segments were to the correct positions when sealed, " +
                "the higher your placement accuracy score.\n\n" +
                "NHEJ is fast but error-prone — small insertions or deletions " +
                "may occur at the repair site, especially when overhangs are trimmed.";
        }

        // Show DNA comparison
        if (originalDNAModel != null) originalDNAModel.SetActive(true);
        if (repairedDNAModel != null) repairedDNAModel.SetActive(true);

        // Play Alysia summary dialogue
        if (NHEJAudio.Instance != null)
        {
            float clipLen = NHEJAudio.Instance.PlayAlysiaDialogue(8);
            if (clipLen > displayDuration) displayDuration = clipLen + 2f;
        }

        if (NHEJAudio.Instance != null)
            NHEJAudio.Instance.PlayPhaseComplete();

        if (autoAdvance)
            activeCoroutine = StartCoroutine(WaitAndAdvance());
    }

    public override void UpdatePhase() { }

    public override void CompletePhase()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (assessmentPanel != null)
            assessmentPanel.SetActive(false);
    }

    IEnumerator WaitAndAdvance()
    {
        yield return new WaitForSeconds(displayDuration);

        if (manager != null && manager.IsServer)
        {
            manager.AdvancePhase();
        }
    }
}
