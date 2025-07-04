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

// 각 입구/출구의 방향과 위치
[System.Serializable]
public class EntranceInfo
{
    public HexDirection direction;        // 입구/출구 방향
    public Transform entranceTransform;   // 해당 방향의 실제 위치 (빈 오브젝트)
}

public class MapPiece : MonoBehaviour
{
    public List<EntranceInfo> entrances = new List<EntranceInfo>();


    private void Start()
    {
        Tilemap tilemap = GetComponentInChildren<Tilemap>();
        if (tilemap == null)
        {
            Debug.LogWarning("타일맵이 없습니다!");
            return;
        }

        Vector3 cellCenter = tilemap.GetCellCenterWorld(Vector3Int.zero);
    }

    // (선택) 에디터에서 자동으로 찾게 하고 싶으면 아래 메서드를 활성화해서 사용
    /*
    void Reset()
    {
        entrances.Clear();
        foreach (Transform t in transform.Find("Entrances")) // "Entrances"라는 자식 오브젝트 아래에 Entrance 오브젝트 배치
        {
            // 예: "Entrance_Up", "Entrance_UpRight" 이런 식으로 네이밍
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
