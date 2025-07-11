using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    [Header("프리팹 레퍼런스")]
    public List<GameObject> mapPrefabs;   // 맵 프리팹 리스트
    public List<GameObject> corridorPrefabs; // 통로 프리팹 리스트

    [Header("그리드 세팅")]
    public Transform gridParent;           // 모든 MapPiece/통로가 들어갈 부모 Grid 오브젝트
    public int pieceCount = 5;             // 맵-통로-맵 순서로 만들 때 총 방 개수

    [Header("플레이어 프리팹")]
    public GameObject playerPrefab;        // 플레이어 프리팹

    private List<GameObject> placedPieces = new List<GameObject>();
    private List<EntranceInfo> openEntrances = new List<EntranceInfo>();

    void Start()
    {
        Generate();
    }

    void Generate()
    {
        placedPieces.Clear();
        openEntrances.Clear();
        Vector3 spawnPos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        GameObject lastPiece = null;
        HexDirection lastExitDir = HexDirection.Right; // 첫번째 입구 방향 기본값

        for (int i = 0; i < pieceCount; i++)
        {
            bool isMap = (i % 2 == 0);
            var prefabList = isMap ? mapPrefabs : corridorPrefabs;
            GameObject prefab = prefabList[Random.Range(0, prefabList.Count)];
            GameObject piece = Instantiate(prefab, spawnPos, rot, gridParent);
            placedPieces.Add(piece);

            // 입구/출구 연결 처리
            var entrances = piece.GetComponent<MapPiece>() ? piece.GetComponent<MapPiece>().entrances : piece.GetComponent<CorridorPiece>().entrances;

            if (lastPiece != null)
            {
                // 이전 조각의 출구와 현 조각의 입구를 연결
                EntranceInfo prevExit = FindEntrance(lastPiece, lastExitDir);
                EntranceInfo currEntrance = FindOppositeEntrance(entrances, lastExitDir);
                if (prevExit != null && currEntrance != null)
                {
                    Vector3 delta = prevExit.entranceTransform.position - currEntrance.entranceTransform.position;
                    piece.transform.position += delta;
                }
            }

            // 다음 출구 방향 결정 (랜덤 or 규칙)
            lastPiece = piece;
            lastExitDir = GetRandomNextDirection(entrances, lastExitDir);
        }

        // 플레이어 및 타일맵 세팅
        SetPlayerAndTilemaps();
    }
    void SetPlayerAndTilemaps()
    {
        // (1) 플레이어 위치 지정
        var firstMap = placedPieces[0];
        Transform playerStart = firstMap.transform.Find("PlayerStart");
        Vector3 spawnPos = playerStart != null ? playerStart.position : firstMap.transform.position;

        CharacterMove player = FindObjectOfType<CharacterMove>();
        if (player == null && playerPrefab != null)
            player = Instantiate(playerPrefab, spawnPos, Quaternion.identity).GetComponent<CharacterMove>();
        else if (player != null)
            player.transform.position = spawnPos;

        // (2) 모든 맵/통로에서 바닥/벽 타일맵 리스트업 후 SetTilemaps 호출
        List<Tilemap> floorList = new List<Tilemap>();
        List<Tilemap> wallList = new List<Tilemap>();
        foreach (Transform child in gridParent)
        {
            foreach (var tm in child.GetComponentsInChildren<Tilemap>())
            {
                if (tm.gameObject.name.Contains("Floor") || tm.gameObject.name.Contains("movable") || tm.gameObject.name.Contains("Layer0"))
                    floorList.Add(tm);
                if (tm.gameObject.name.Contains("Wall") || tm.gameObject.name.Contains("Layer10"))
                    wallList.Add(tm);
            }
        }
        if (player != null)
            player.SetTilemaps(floorList, wallList);
    }

    EntranceInfo FindEntrance(GameObject go, HexDirection dir)
    {
        var mp = go.GetComponent<MapPiece>();
        if (mp) return mp.entrances.Find(e => e.direction == dir);
        var cp = go.GetComponent<CorridorPiece>();
        if (cp) return cp.entrances.Find(e => e.direction == dir);
        return null;
    }

    EntranceInfo FindOppositeEntrance(List<EntranceInfo> entrances, HexDirection dir)
    {
        HexDirection opp = GetOpposite(dir);
        return entrances.Find(e => e.direction == opp);
    }

    HexDirection GetOpposite(HexDirection dir)
    {
        // 0-3, 1-4, 2-5 방향이 반대
        return (HexDirection)(((int)dir + 3) % 6);
    }

    HexDirection GetRandomNextDirection(List<EntranceInfo> entrances, HexDirection prevExit)
    {
        // 다음 출구 방향 랜덤 (본인 필요에 맞게 조정)
        List<HexDirection> dirs = new List<HexDirection>();
        foreach (var e in entrances)
        {
            if (e.direction != GetOpposite(prevExit)) dirs.Add(e.direction);
        }
        return dirs.Count > 0 ? dirs[Random.Range(0, dirs.Count)] : prevExit;
    }
}
