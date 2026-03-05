using UnityEngine;

// Temporary diagnostic — attach to a protein prefab, enter Play, read the console stack trace,
// then DELETE this component once you know what's calling SetActive(false).
public class DisableWatcher : MonoBehaviour
{
    void OnDisable()
    {
        Debug.LogError($"[DisableWatcher] '{gameObject.name}' was disabled!\n{System.Environment.StackTrace}");
    }
}
