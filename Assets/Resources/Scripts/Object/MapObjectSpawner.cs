using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapObjectSpawner : MonoBehaviour
{
    public List<GameObject> trapPrefabs;
    public int trapCount = 3;

    public List<GameObject> chestPrefabs;
    public int chestCount = 2;

    private Tilemap tilemap;

    private void Awake()
    {
        // 자식에 붙은 타일맵을 자동으로 찾아서 저장
        tilemap = GetComponentInChildren<Tilemap>();
        if (tilemap == null)
            Debug.LogError("MapObjectSpawner: 자식에서 Tilemap을 찾을 수 없습니다.");
    }

    // 맵 생성 직후 MapManager가 호출
    public void Spawn(Tilemap tilemap, Vector3Int excludeCell, BoxCollider2D barrierExclude)
    {
        // 컨테이너 찾기
        Transform container = tilemap.transform.parent.Find("Object");
        if (container == null)
        {
            Debug.LogWarning("MapObjectSpawner: 'Object' 컨테이너를 찾을 수 없습니다. 스포너 자신을 부모로 사용합니다.");
            container = this.transform;
        }

        //var bounds = tilemap.cellBounds;
        var floorCells = new List<Vector3Int>();
        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            // 1) 바닥 타일인지
            if (tilemap.GetTile(pos)?.name.Contains("Floor") != true)
                continue;

            // 2) 플레이어 시작 지점 제외
            if (pos == excludeCell)
                continue;

            // 3) Barrier 콜라이더 영역 제외
            if (barrierExclude != null)
            {
                Vector3 worldPos = tilemap.GetCellCenterWorld(pos);
                if (barrierExclude.bounds.Contains(worldPos))
                    continue;
            }

            floorCells.Add(pos);
        }

        // 함정 배치
        for (int i = 0; i < trapCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            GameObject obj = Instantiate(
                trapPrefabs[Random.Range(0, trapPrefabs.Count)],
                worldPos,
                Quaternion.identity,
                container);
            floorCells.RemoveAt(idx);
        }

        //오브젝트 게이지 처리
        ObjectGaugeManager.Instance.RegisterTotalBoxes(chestCount);

        // 상자 배치
        for (int i = 0; i < chestCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            GameObject obj = Instantiate(
                chestPrefabs[Random.Range(0, chestPrefabs.Count)],
                worldPos,
                Quaternion.identity,
                container);

            // Flip 처리: x > 0이면 좌우 반전
            if (worldPos.x > 0f)
            {
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.flipX = true;
                else
                    obj.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            floorCells.RemoveAt(idx);
        }
    }
}
