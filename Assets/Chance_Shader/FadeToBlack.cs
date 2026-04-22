using UnityEngine;
using System.Collections;

public class FadeToBlack : MonoBehaviour
{
    public CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup.alpha = 0f;
    }

    public void FadeIn(float duration) => StartCoroutine(Fade(0, 1, duration));
    public void FadeOut(float duration) => StartCoroutine(Fade(1, 0, duration));

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = endAlpha;

        // Optional: Block raycasts only when visible
        canvasGroup.blocksRaycasts = (endAlpha > 0);
    }

    public void FadeInAndOut()
    {
        StartCoroutine(_FadeInAndOut());
    }
    
    public IEnumerator _FadeInAndOut()
    {
        FadeIn(2);
        yield return new WaitForSeconds(2);
        yield return new WaitForSeconds(1);
        FadeOut(2);
        yield return new WaitForSeconds(2);
    }
}