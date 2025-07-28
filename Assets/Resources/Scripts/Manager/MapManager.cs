using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapManager : MonoBehaviour
{
    public List<GameObject> mapPrefabs;  // 맵 프리팹 리스트
    public Transform gridParent;         // 맵이 배치될 부모 오브젝트
    public GameObject playerPrefab;      // 플레이어 프리팹

    private GameObject currentMap;       // 현재 생성된 맵
    //private Tilemap []allMaps = null;      // 생성된 맵이 가지고 있는 타일맵들의 정보


    MapToggleManager mapToggle;

    void Awake()
    {
        // MapToggleManager 컴포넌트 가져오기
        mapToggle = GetComponent<MapToggleManager>();
    }

    void Start()
    {
        // 랜덤으로 맵 생성
        GenerateRandomMap();
    }

    // 랜덤 맵 생성 함수
    void GenerateRandomMap()
    {
        // 랜덤으로 맵 프리팹 선택
        GameObject selectedPrefab = mapPrefabs[Random.Range(0, mapPrefabs.Count)];

        // 선택된 프리팹 배치
        currentMap = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity, gridParent);

        // 현재 맵 내의 Tilemap 컴포넌트 가져오기
        var allMaps = currentMap.GetComponentsInChildren<Tilemap>();  // Tilemap 컴포넌트 가져오기
        Tilemap floorMap = null;
        Tilemap wallMap = null;

        foreach (var tm in allMaps)
        {
            var n = tm.gameObject.name.ToLower();
            if (floorMap == null && n.Contains("floor"))
                floorMap = tm;
            if (wallMap == null && n.Contains("wall"))
                wallMap = tm;
            if (floorMap != null && wallMap != null)
                break;
        }

        if (floorMap == null)
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다!");
        if (wallMap == null)
            Debug.LogWarning("Wall 타일맵을 찾을 수 없습니다. 벽은 무시됩니다.");


        //  MapToggleManager에 현재 맵, Grid 할당
        mapToggle.mainMap = currentMap;
        mapToggle.gridParent = gridParent;

        var spawner = currentMap.GetComponentInChildren<MapObjectSpawner>();
        var spawnPoint = currentMap.transform.Find("PlayerStart");

        //Barrier 범위값 가져오기
        var barrierObj = currentMap.transform.Find("Barrier");
        BoxCollider2D barrierCol = null;
        if (barrierObj != null)
            barrierCol = barrierObj.GetComponent<BoxCollider2D>();

        // 타일맵이 제대로 가져와졌는지 확인 후
        // 셀 좌표 계산 후 오브젝트 스폰
        if (floorMap != null && spawner != null && spawnPoint != null && barrierCol != null)
        {
            // SpawnPoint 오브젝트 찾아서 월드→셀 좌표로 변환
            Vector3Int spawnCell = floorMap.WorldToCell(spawnPoint.position);
            spawner.Spawn(floorMap, spawnCell, barrierCol);
        }
        else
        {
            if(spawner == null)
                Debug.LogError("MapObjectSpawner 컴포넌트를 찾을 수 없습니다!");
            else if(spawnPoint == null)
                Debug.LogError("PlayerStart 오브젝트를 찾을 수 없습니다!");
            else if(barrierObj == null)
                        Debug.LogError("barrierObj 오브젝트를 찾을 수 없습니다!");
        }
            

        // 플레이어 인스턴스화 및 타일맵 주입
        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint != null
                                                           ? spawnPoint.position
                                                           : Vector3.zero,
                                                           Quaternion.identity);
        var pm = playerInstance.GetComponent<PlayerMovement>();
        if (pm != null && allMaps != null)
            pm.SetTilemap(floorMap,wallMap);

        //  MapToggleManager에 Player 위치 할당
        mapToggle.playerTransform = playerInstance.transform;
    }
}
