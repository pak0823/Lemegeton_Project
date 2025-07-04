using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class EntranceSnapper : EditorWindow
{
    [MenuItem("Tools/Entrance 자동 셀 스냅퍼")]
    public static void ShowWindow()
    {
        GetWindow(typeof(EntranceSnapper));
    }

    void OnGUI()
    {
        if (GUILayout.Button("선택한 Entrance 오브젝트, 셀 중심으로 스냅"))
        {
            SnapEntrances();
        }
    }

    void SnapEntrances()
    {
        foreach (var obj in Selection.gameObjects)
        {
            // 부모-조상 계층에서 Tilemap 자동 탐색
            var tilemap = FindTilemapInParents(obj.transform);
            if (tilemap == null)
            {
                Debug.LogWarning($"{obj.name}의 부모 계층에 Tilemap이 없습니다.");
                continue;
            }

            // localPosition → 월드좌표 → 셀좌표 → 셀 중심 월드좌표 → 프리팹의 localPosition 변환
            Vector3 worldPos = obj.transform.position;
            Vector3Int cellPos = tilemap.WorldToCell(worldPos);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cellPos);
            Vector3 parentLocal = obj.transform.parent.InverseTransformPoint(cellCenter);

            Undo.RecordObject(obj.transform, "Entrance 스냅");
            obj.transform.localPosition = parentLocal;
        }
        Debug.Log("Entrance 자동 스냅 완료!");
    }

    // 조상 계층에서 Tilemap 자동 탐색 함수
    Tilemap FindTilemapInParents(Transform t)
    {
        while (t != null)
        {
            var tilemap = t.GetComponent<Tilemap>();
            if (tilemap != null) return tilemap;
            t = t.parent;
        }
        return null;
    }
}
