using System.Collections.Generic;
using System.Linq;
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
    // excludeColliders 배열에 들어있는 콜라이더들의 영역 위엔 오브젝트를 스폰하지 않음
    public void Spawn(Tilemap tilemap, params Collider2D[] excludeColliders)
    {
        // 컨테이너 찾기
        Transform container = tilemap.transform.parent.Find("Object");
        if (container == null)
        {
            Debug.LogWarning("MapObjectSpawner: 'Object' 컨테이너를 찾을 수 없습니다. 스포너 자신을 부모로 사용합니다.");
            container = this.transform;
        }

        var floorCells = new List<Vector3Int>();
        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            // 1) 바닥 타일인지
            if (tilemap.GetTile(pos)?.name.Contains("Floor") != true)
                continue;

            // 2) excludeColliders 에 들어온 모든 콜라이더 영역 제외
            Vector3 worldPos = tilemap.GetCellCenterWorld(pos);
            if (excludeColliders.Any(col => col != null && col.OverlapPoint(worldPos)))
                continue;

            floorCells.Add(pos);
        }

        // 함정 배치
        for (int i = 0; i < trapCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);
            // 프리팹 선택
            var prefab = trapPrefabs[Random.Range(0, trapPrefabs.Count)];

            GameObject obj = Instantiate(
                prefab,
                worldPos,
                Quaternion.identity,
                container);

            // pid 관련 처리
            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();
            // prefabName 매칭을 위해 이름 고정(스냅샷의 prefabName 키로 사용할 것)
            obj.name = prefab.name;

            floorCells.RemoveAt(idx);
        }

        // 상자 배치
        for (int i = 0; i < chestCount && floorCells.Count > 0; i++)
        {
            int idx = Random.Range(0, floorCells.Count);
            Vector3 worldPos = tilemap.GetCellCenterWorld(floorCells[idx]);

            // 프리팹 선택
            var prefab = chestPrefabs[Random.Range(0, chestPrefabs.Count)];
            GameObject obj = Instantiate(
                prefab,
                worldPos,
                Quaternion.identity,
                container);

            // pid 관련 처리
            var pid = obj.GetComponent<ExplorationPersistId>();
            if (pid == null) pid = obj.AddComponent<ExplorationPersistId>();
            obj.name = prefab.name; // 스냅샷 prefabName 키와 동일하게

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
