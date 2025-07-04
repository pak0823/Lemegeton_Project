using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum HexDirection
{
    Up,        // 0
    UpRight,   // 1
    DownRight, // 2
    Down,      // 3
    DownLeft,  // 4
    UpLeft,     // 5
    Left,       //6
    Right       //7
}

// �� �Ա�/�ⱸ�� ����� ��ġ
[System.Serializable]
public class EntranceInfo
{
    public HexDirection direction;        // �Ա�/�ⱸ ����
    public Transform entranceTransform;   // �ش� ������ ���� ��ġ (�� ������Ʈ)
}

public class MapPiece : MonoBehaviour
{
    public List<EntranceInfo> entrances = new List<EntranceInfo>();


    private void Start()
    {
        Tilemap tilemap = GetComponentInChildren<Tilemap>();
        if (tilemap == null)
        {
            Debug.LogWarning("Ÿ�ϸ��� �����ϴ�!");
            return;
        }

        Vector3 cellCenter = tilemap.GetCellCenterWorld(Vector3Int.zero);
    }

    // (����) �����Ϳ��� �ڵ����� ã�� �ϰ� ������ �Ʒ� �޼��带 Ȱ��ȭ�ؼ� ���
    /*
    void Reset()
    {
        entrances.Clear();
        foreach (Transform t in transform.Find("Entrances")) // "Entrances"��� �ڽ� ������Ʈ �Ʒ��� Entrance ������Ʈ ��ġ
        {
            // ��: "Entrance_Up", "Entrance_UpRight" �̷� ������ ���̹�
            foreach (HexDirection dir in System.Enum.GetValues(typeof(HexDirection)))
            {
                if (t.name.Contains(dir.ToString()))
                {
                    EntranceInfo info = new EntranceInfo();
                    info.direction = dir;
                    info.entranceTransform = t;
                    entrances.Add(info);
                    break;
                }
            }
        }
    }
    */
}
