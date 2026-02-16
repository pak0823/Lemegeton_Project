using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapDataAutoSetupTool : EditorWindow
{
    [MenuItem("Tools/Auto Setup Map Data (Selected Prefabs)")]
    public static void SetupSelectedPrefabs()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("선택된 오브젝트가 없습니다. 프로젝트 뷰에서 프리팹을 선택해주세요.");
            return;
        }

        int count = 0;
        foreach (var go in selected)
        {
            // 프리팹 에셋인지 확인 (씬 오브젝트도 가능하긴 함)
            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path))
            {
                // 씬 오브젝트인 경우
                SetupSingleObject(go);
            }
            else
            {
                // 프리팹 에셋인 경우 (인스턴스화하지 않고 수정)
                using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    SetupSingleObject(editingScope.prefabContentsRoot);
                }
            }
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MapDataAutoSetupTool] {count}개의 오브젝트 설정 완료.");
    }

    private static void SetupSingleObject(GameObject root)
    {
        var mapData = root.GetComponent<ExplorationMapData>();
        if (mapData == null)
        {
            mapData = root.AddComponent<ExplorationMapData>();
        }

        // AutoSetup 호출 (ExplorationMapData 내에 정의된 로직 재사용)
        // Public 메서드여야 함.
        mapData.AutoSetup();

        EditorUtility.SetDirty(root);
    }
}
