using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class EntranceAutoAlignerEditor : EditorWindow
{
    [MenuItem("Tools/Entrance Auto Aligner")]
    public static void ShowWindow()
    {
        GetWindow<EntranceAutoAlignerEditor>("Entrance Auto Aligner");
    }

    private void OnGUI()
    {
        GUILayout.Label("셀 중앙 자동 정렬", EditorStyles.boldLabel);

        if (GUILayout.Button("선택된 오브젝트들 정렬"))
        {
            AlignSelectedEntrances();
        }
        EditorGUILayout.HelpBox("입구/출구 오브젝트(Transform)를 하나 이상 선택한 상태에서 클릭하세요.\n각 오브젝트의 부모에 Tilemap이 있어야 합니다.", MessageType.Info);
    }

    private void AlignSelectedEntrances()
    {
        int cnt = 0;
        foreach (var obj in Selection.transforms)
        {
            // 부모에 Tilemap이 있는지 찾기
            Tilemap tilemap = obj.GetComponentInParent<Tilemap>();
            if (tilemap == null)
            {
                Debug.LogWarning($"{obj.name}: 부모에 Tilemap이 없습니다.");
                continue;
            }

            // 현 위치에서 셀 좌표 계산 후 셀 중심으로 정렬
            Vector3Int cell = tilemap.WorldToCell(obj.position);
            Vector3 center = tilemap.GetCellCenterWorld(cell);
            Undo.RecordObject(obj, "Entrance Auto Align");
            obj.position = center;
            cnt++;
            Debug.Log($"{obj.name}: 셀({cell})의 중심({center})에 정렬 완료");
        }
        Debug.Log($"총 {cnt}개 오브젝트 자동 정렬 완료");
    }
}
