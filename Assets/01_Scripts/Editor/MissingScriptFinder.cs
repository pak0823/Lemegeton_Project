using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MissingScriptFinder : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts")]
    public static void FindMissingScripts()
    {
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
        int count = 0;
        foreach (GameObject go in allObjects)
        {

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    Debug.LogError($"[Missing Script] GameObject: '{GetFullPath(go)}' has a missing script at index {i}", go);
                    count++;
                }
            }
        }
        Debug.Log($"Finished searching. Found {count} missing scripts.");
    }

    private static string GetFullPath(GameObject go)
    {
        string path = go.name;
        while (go.transform.parent != null)
        {
            go = go.transform.parent.gameObject;
            path = go.name + "/" + path;
        }
        return path;
    }

    [MenuItem("Tools/Open Exploration")]
    public static void OpenExploration()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/00_Scenes/ExplorationScene.unity");
    }

    [MenuItem("Tools/Open Title")]
    public static void OpenTitle()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/00_Scenes/TitleScene.unity");
    }
}
