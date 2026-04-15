using System;
using System.Collections;
using UnityEngine;

public class NHEJProteinNPC : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float snapDistance = 0.05f;
    [SerializeField] AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Effects")]
    [SerializeField] float glowIntensity = 2f;
    [SerializeField] Color glowColor = Color.white;

    Renderer[] renderers;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    public IEnumerator MoveToTarget(Vector3 targetPosition, float duration)
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            yield return null;
        }
        transform.position = targetPosition;
    }

    public IEnumerator MoveToTarget(Transform target, float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(startPos, target.position, t);
            transform.rotation = Quaternion.Slerp(startRot, target.rotation, t);
            yield return null;
        }
        transform.position = target.position;
        transform.rotation = target.rotation;
    }
    public void SetGlow(bool active)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor(EmissionColor, active ? glowColor * glowIntensity : Color.black);
            rend.SetPropertyBlock(block);
        }
    }

    public IEnumerator PulseGlow(float duration, float pulseSpeed = 2f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float intensity = (Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f * glowIntensity;
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                var block = new MaterialPropertyBlock();
                rend.GetPropertyBlock(block);
                block.SetColor(EmissionColor, glowColor * intensity);
                rend.SetPropertyBlock(block);
            }
            yield return null;
        }
        SetGlow(false);
    }

    public IEnumerator Depart(Vector3 direction, float distance, float duration)
    {
        Vector3 target = transform.position + direction.normalized * distance;
        yield return MoveToTarget(target, duration);
        gameObject.SetActive(false);
    }
}
