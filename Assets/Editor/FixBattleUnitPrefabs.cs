using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FixBattleUnitPrefabs
{
    [MenuItem("Tools/Lemegeton/Fix Missing Unit Components")]
    public static void FixPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/02_Prefabs/Battle" });
        int fixedCount = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BattleUnit unit = prefab.GetComponent<BattleUnit>();
            if (unit != null)
            {
                bool changed = false;

                // Check and add missing components directly to the prefab asset
                if (prefab.GetComponent<UnitStats>() == null)
                {
                    Undo.AddComponent<UnitStats>(prefab);
                    changed = true;
                }
                if (prefab.GetComponent<UnitMover>() == null)
                {
                    Undo.AddComponent<UnitMover>(prefab);
                    changed = true;
                }
                if (prefab.GetComponent<UnitVisual>() == null)
                {
                    Undo.AddComponent<UnitVisual>(prefab);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(prefab);
                    fixedCount++;
                    Debug.Log($"[FixBattleUnitPrefabs] 강제로 컴포넌트를 저장했습니다: {prefab.name}");
                }
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[FixBattleUnitPrefabs] 총 {fixedCount}개의 프리팹을 성공적으로 수정 및 저장했습니다!");
        }
        else
        {
            Debug.Log("[FixBattleUnitPrefabs] 수정할 프리팹이 없습니다 (모두 정상).");
        }
    }
}
