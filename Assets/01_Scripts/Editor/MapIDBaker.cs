using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapIDBaker : EditorWindow
{
    [MenuItem("Tools/Lemegeton/Map/Bake Map IDs")]
    public static void Init()
    {
        GetWindow<MapIDBaker>("Map ID Baker");
    }

    private GameObject targetMapPrefab;

    void OnGUI()
    {
        GUILayout.Label("Map ID Baker", EditorStyles.boldLabel);
        GUILayout.Space(10);

        targetMapPrefab = (GameObject)EditorGUILayout.ObjectField("Target Map Prefab", targetMapPrefab, typeof(GameObject), false);

        GUILayout.Space(10);

        if (GUILayout.Button("Bake IDs (Assign Persistent IDs)"))
        {
            if (targetMapPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Map Prefab.", "OK");
                return;
            }
            BakeIDs(targetMapPrefab);
        }
    }

    static void BakeIDs(GameObject prefabAsset)
    {
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(path))
        {
            // Scene Object case (Optional support)
            BakeHierarchy(prefabAsset);
            return;
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            GameObject root = editScope.prefabContentsRoot;
            BakeHierarchy(root);
        }
    }

    static void BakeHierarchy(GameObject root)
    {
        var ids = root.GetComponentsInChildren<ExplorationPersistId>(true);
        int count = 0;
        int updated = 0;
        HashSet<string> usedIds = new HashSet<string>();

        // 1. Collect existing
        foreach (var pid in ids)
        {
            if (!string.IsNullOrEmpty(pid.Id))
            {
                if (usedIds.Contains(pid.Id))
                {
                    // Clean duplicate
                    var so = new SerializedObject(pid);
                    so.FindProperty("id").stringValue = "";
                    so.ApplyModifiedProperties();
                }
                else
                {
                    usedIds.Add(pid.Id);
                }
            }
        }

        // 2. Assign new
        foreach (var pid in ids)
        {
            if (string.IsNullOrEmpty(pid.Id))
            {
                string newId = System.Guid.NewGuid().ToString("N");
                while (usedIds.Contains(newId)) 
                    newId = System.Guid.NewGuid().ToString("N");

                var so = new SerializedObject(pid);
                so.FindProperty("id").stringValue = newId;
                so.ApplyModifiedProperties();
                
                usedIds.Add(newId);
                updated++;
            }
            count++;
        }

        Debug.Log($"[MapIDBaker] Processed {count} objects. Updated {updated} IDs in '{root.name}'");
    }
}
