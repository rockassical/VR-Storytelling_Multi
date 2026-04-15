using UnityEngine;
using UnityEditor;

public class ApplyMaterialToChildren : EditorWindow
{
    GameObject parent;
    Material material;

    [MenuItem("Tools/Apply Material To Children")]
    static void Open() => GetWindow<ApplyMaterialToChildren>("Apply Material");

    void OnGUI()
    {
        parent = (GameObject)EditorGUILayout.ObjectField("Parent", parent, typeof(GameObject), true);
        material = (Material)EditorGUILayout.ObjectField("Material", material, typeof(Material), false);

        GUI.enabled = parent != null && material != null;
        if (GUILayout.Button("Apply to All Children"))
        {
            Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
            Undo.RecordObjects(renderers, "Apply Material To Children");
            foreach (Renderer r in renderers)
                r.sharedMaterial = material;
            Debug.Log($"Applied {material.name} to {renderers.Length} renderers under {parent.name}");
        }
        GUI.enabled = true;
    }
}
