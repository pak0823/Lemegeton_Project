using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class MapManager : MonoBehaviour
{
    public List<GameObject> mapPrefabs;  // 맵 프리팹 리스트
    public Transform gridParent;         // 맵이 배치될 부모 오브젝트
    public GameObject playerPrefab;      // 플레이어 프리팹

    private GameObject currentMap;       // 현재 생성된 맵
    private Tilemap currentTilemap;      // 현재 타일맵


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
        Vector3 spawnPosition = new Vector3(0, 0, 0);  // 배치 위치
        currentMap = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity, gridParent);

        // 현재 맵 내의 Tilemap 컴포넌트 가져오기
        currentTilemap = currentMap.GetComponentInChildren<Tilemap>();  // Tilemap 컴포넌트 가져오기

        var spawner = currentMap.GetComponentInChildren<MapObjectSpawner>();
        var spawnPoint = currentMap.transform.Find("PlayerStart");

        // 타일맵이 제대로 가져와졌는지 확인
        if (currentTilemap != null)
        {
            //오브젝트 랜덤 배치
            if (spawner != null)
            {
                // 1) SpawnPoint 오브젝트 찾아서 월드→셀 좌표로 변환
                Vector3Int spawnCell = Vector3Int.zero;
                if (spawnPoint != null)
                    spawnCell = currentTilemap.WorldToCell(spawnPoint.position);

                spawner.Spawn(currentTilemap, spawnCell);
            }
            else
                Debug.LogError("MapObjectSpawner 컴포넌트를 찾을 수 없습니다!");

                // 타일맵 정보를 플레이어에게 전달
                PlayerMovement playerMovement = playerPrefab.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.SetTilemap(currentTilemap);  // 플레이어 이동 스크립트에 타일맵 전달
            }
        }


        // 2) 플레이어 생성
        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, Quaternion.identity);


        // 2) 인스턴스에서 컴포넌트 가져오기
        PlayerMovement pm = playerInstance.GetComponent<PlayerMovement>();
        if (pm != null && currentTilemap != null)
        {
            // 3) 인스턴스에 타일맵 주입
            pm.SetTilemap(currentTilemap);
        }
    }
}
