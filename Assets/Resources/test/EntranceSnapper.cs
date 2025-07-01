// Assets/Editor/EntranceSnapper.cs�� ����
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class EntranceSnapper : EditorWindow
{
    [MenuItem("Tools/Entrance �ڵ� �� ������")]
    public static void ShowWindow()
    {
        GetWindow(typeof(EntranceSnapper));
    }

    void OnGUI()
    {
        if (GUILayout.Button("������ Entrance ������Ʈ, �� �߽����� ����"))
        {
            SnapEntrances();
        }
    }

    void SnapEntrances()
    {
        foreach (var obj in Selection.gameObjects)
        {
            var tilemap = obj.GetComponentInParent<Tilemap>();
            if (tilemap == null)
            {
                Debug.LogWarning($"{obj.name}�� �θ� Tilemap�� �����ϴ�.");
                continue;
            }

            // localPosition �� ������ǥ �� ����ǥ �� �� �߽� ������ǥ �� �������� localPosition ��ȯ
            Vector3 worldPos = obj.transform.position;
            Vector3Int cellPos = tilemap.WorldToCell(worldPos);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(cellPos);
            Vector3 parentLocal = obj.transform.parent.InverseTransformPoint(cellCenter);

            Undo.RecordObject(obj.transform, "Entrance ����");
            obj.transform.localPosition = parentLocal;
        }
        Debug.Log("Entrance �ڵ� ���� �Ϸ�!");
    }
}
