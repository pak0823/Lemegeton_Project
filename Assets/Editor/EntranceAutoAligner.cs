using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class EntranceAutoAlignerEditor : EditorWindow
{
    [MenuItem("Tools/Lemegeton/Map/Align Entrances")]
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
        EditorGUILayout.HelpBox("입구/출구 오브젝트(Transform)를 하나 이상 선택한 상태에서 클릭하세요.\n씬이나 프리팹 내에 Tilemap이 있어야 합니다.", MessageType.Info);
    }

    private void AlignSelectedEntrances()
    {
        int cnt = 0;
        foreach (var obj in Selection.transforms)
        {
            // 1. 오브젝트의 부모 계층에서 Tilemap 찾기
            Tilemap tilemap = obj.GetComponentInParent<Tilemap>();

            // 2. 부모에 없다면, 현재 오브젝트가 속한 프리팹(또는 씬 구조)의 최상위 루트에서 다시 하위로 Tilemap 검색
            if (tilemap == null)
            {
                tilemap = obj.root.GetComponentInChildren<Tilemap>(true);
            }

            // 3. 그래도 없다면, 씬 전체에서 활성화된 임의의 Tilemap 하나 가져오기
            if (tilemap == null)
            {
                tilemap = FindObjectOfType<Tilemap>();
            }

            if (tilemap == null)
            {
                Debug.LogWarning($"{obj.name}: 기준이 될 Tilemap을 찾지 못했습니다.");
                continue;
            }

            // 현 위치에서 셀 좌표 계산 후 셀 중심으로 정렬
            Vector3Int cell = tilemap.WorldToCell(obj.position);
            Vector3 center = tilemap.GetCellCenterWorld(cell);

            // 중요: 오브젝트의 원래 Z축(깊이) 혹은 Y오프셋을 보존하기 위해 타일 중앙의 X, Y만 적용하고 Z는 기존 값 유지
            center.z = obj.position.z;

            Undo.RecordObject(obj, "Entrance Auto Align");
            obj.position = center;
            cnt++;
            Debug.Log($"{obj.name}: 셀({cell})의 중심({center})에 정렬 완료 (기준 Tilemap: {tilemap.name})");
        }
        Debug.Log($"총 {cnt}개 오브젝트 자동 정렬 완료");
    }
}
