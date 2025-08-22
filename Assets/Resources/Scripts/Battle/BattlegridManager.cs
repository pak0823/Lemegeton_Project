//헥사 이웃/BFS, 차지/점유
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleGridManager : MonoBehaviour
{
    IBattleMapProvider provider;
    HashSet<Vector3Int> playerOcc = new();
    HashSet<Vector3Int> enemyOcc = new();

    private void Awake()
    {
        Shared.battleGridManager = this;
        if (Shared.battleMapManager == null)
            Shared.battleMapManager = FindObjectOfType<BattleMapManager>();
        provider = (IBattleMapProvider)Shared.battleMapManager;
    }
    public Tilemap GetMap(Team team)
    {
        if (team == Team.Player)
            return provider.PlayerFloor;
        else
            return provider.EnemyFloor;
    }

    public IEnumerable<Vector3Int> GetReachable(Team team, Vector3Int start, int range)
    {
        var map = GetMap(team);
        var visited = new HashSet<Vector3Int> { start };
        var q = new Queue<(Vector3Int, int)>();
        q.Enqueue((start, 0));
        while (q.Count > 0)
        {
            var (c, cost) = q.Dequeue();
            yield return c;
            if (cost == range) continue;

            bool odd = Mathf.Abs(c.y) % 2 == 1;
            Vector3Int[] dirs = {
                new(1,0,0), new(-1,0,0), new(0,1,0), new(0,-1,0), new(1,-1,0), new(-1,1,0)
            };

            foreach (var d in dirs)
            {
                var n = c + d;
                if (visited.Contains(n)) continue;
                if (!map.cellBounds.Contains(n) || !map.HasTile(n)) continue;
                if (IsOccupied(team, n)) continue;
                visited.Add(n);
                q.Enqueue((n, cost + 1));
            }
        }
    }
    // 현재 셀 기준으로 6방향 인접칸(1칸) 중 '이동 가능' 칸만 반환
    public IEnumerable<Vector3Int> GetAdjacentWalkable(Team team, Vector3Int center)
    {
        var map = GetMap(team); // 기존에 사용 중인 팀→타일맵 매핑 함수
        var results = new List<Vector3Int>();

        bool odd = Mathf.Abs(center.y) % 2 == 1;
        // PlayerMovement/PushObject와 동일한 포인티드-탑 헥사 오프셋
        Vector3Int[] offsets = odd
            ? new[] {
            new Vector3Int(-1, 0, 0), // W
            new Vector3Int( 1, 0, 0), // E
            new Vector3Int( 0, 1, 0), // NW
            new Vector3Int( 1, 1, 0), // NE
            new Vector3Int( 0,-1, 0), // SW
            new Vector3Int( 1,-1, 0), // SE
            }
            : new[] {
            new Vector3Int(-1, 0, 0), // W
            new Vector3Int( 1, 0, 0), // E
            new Vector3Int(-1, 1, 0), // NW
            new Vector3Int( 0, 1, 0), // NE
            new Vector3Int(-1,-1, 0), // SW
            new Vector3Int( 0,-1, 0), // SE
            };

        foreach (var d in offsets)
        {
            var n = center + d;
            if (!map.cellBounds.Contains(n)) continue;
            if (!map.HasTile(n)) continue;                // 바닥 없음
            if (IsOccupied(team, n)) continue;            // 우리 진영 점유 칸은 제외(겹침 방지)

            results.Add(n);
        }

        return results;
    }

    public bool InRange(Vector3Int a, Vector3Int b, int range)
    {
        int dist = (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs((a.x + a.y) - (b.x + b.y))) / 2;
        return dist <= range;
    }

    public int CrossMapDistance(
    Tilemap reference,           // 기준 타일맵(권장: PlayerFloor)
    Tilemap fromMap, Vector3Int fromCell,
    Tilemap toMap, Vector3Int toCell)
    {
        Vector3 aW = fromMap.GetCellCenterWorld(fromCell);
        Vector3 bW = toMap.GetCellCenterWorld(toCell);
        Vector3Int aRef = reference.WorldToCell(aW);
        Vector3Int bRef = reference.WorldToCell(bW);
        return (Mathf.Abs(aRef.x - bRef.x)
              + Mathf.Abs(aRef.y - bRef.y)
              + Mathf.Abs((aRef.x + aRef.y) - (bRef.x + bRef.y))) / 2;
    }

    //public bool InRangeAcrossMaps(
    //Tilemap reference,           // 기준으로 삼을 타일맵 (보통 PlayerFloor)
    //Tilemap fromMap, Vector3Int fromCell,
    //Tilemap toMap, Vector3Int toCell,
    //int range)
    //{
    //    return CrossMapDistance(reference, fromMap, fromCell, toMap, toCell) <= range;
    //}

    public void SetOccupied(Team t, Vector3Int cell, bool on)
    {
        var set = (t == Team.Player) ? playerOcc : enemyOcc;
        if (on) set.Add(cell); else set.Remove(cell);
    }
    public bool IsOccupied(Team t, Vector3Int cell)
    {
        var set = (t == Team.Player) ? playerOcc : enemyOcc;
        return set.Contains(cell);
    }
}

