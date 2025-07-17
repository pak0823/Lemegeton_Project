//// MapGenerator.cs (최적화 버전)
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Tilemaps;

//public class MapGenerator : MonoBehaviour
//{
//    [Header("프리팹 레퍼런스")]
//    public List<GameObject> mapPrefabs;
//    public List<GameObject> corridorPrefabs;

//    [Header("세팅")]
//    public Transform gridParent;
//    public int minMapCount;
//    public GameObject playerPrefab;

//    private List<GameObject> placedPieces = new();
//    private List<Bounds> pieceBoundsList = new();
//    private List<GameObject> usedMapPrefabs = new();
//    private List<Bounds> overlapBoundsList = new();

//    void Start() => GenerateMaps();

//#if UNITY_EDITOR
//    public void ClearAllGeneratedPieces()
//    {
//        foreach (var piece in placedPieces)
//            if (piece) DestroyImmediate(piece);
//        placedPieces.Clear();
//        pieceBoundsList.Clear();
//        overlapBoundsList.Clear();
//    }
//#endif

//    public void GenerateMaps()
//    {
//        const int maxTry = 1000;
//        bool success = false;

//        for (int attempt = 0; attempt < maxTry; attempt++)
//        {
//            ClearPieces();
//            usedMapPrefabs.Clear();

//            if (TryPlaceRecursive(Vector3.zero, Quaternion.identity, null, HexDirection.Right, 0))
//            {
//                success = true;
//                break;
//            }
//        }

//        if (success)
//        {
//            SetPlayerAndTilemaps();
//            Debug.Log($"맵 배치 성공! 총 {placedPieces.Count}개의 조각이 배치되었습니다.");
//        }
//        else
//        {
//            Debug.LogError("맵 배치 실패! 프리팹 구성을 확인하세요. (모든 시도 실패)");
//        }
//    }

//    bool TryPlaceRecursive(Vector3 spawnPos, Quaternion rot, GameObject lastPiece, HexDirection lastExitDir, int depth)
//    {
//        if (depth >= minMapCount) return true;

//        GameObject mapPrefab = GetRandomMapPrefab();
//        if (mapPrefab == null) return false;

//        GameObject newMap = Instantiate(mapPrefab, spawnPos, rot, gridParent);
//        EntranceInfo prevExit = lastPiece ? FindEntrance(lastPiece, lastExitDir) : null;
//        EntranceInfo currEntrance = lastPiece ? FindAnyEntrance(newMap.GetComponent<MapPiece>().entrances, lastExitDir) : null;

//        if (prevExit != null && currEntrance != null)
//            newMap.transform.position += prevExit.entranceTransform.position - currEntrance.entranceTransform.position;
//        else if (lastPiece != null)
//        {
//            DestroyImmediate(newMap);
//            return false;
//        }

//        Bounds newBounds = GetPieceBounds(newMap);
//        if (IsOverlap(newBounds))
//        {
//            DestroyImmediate(newMap);
//            return false;
//        }

//        placedPieces.Add(newMap);
//        pieceBoundsList.Add(newBounds);
//        if (!usedMapPrefabs.Contains(mapPrefab)) usedMapPrefabs.Add(mapPrefab);

//        foreach (var exit in newMap.GetComponent<MapPiece>().entrances)
//        {
//            if (exit.direction == GetOpposite(lastExitDir)) continue;
//            HexDirection nextDir = exit.direction;

//            foreach (var corridorPrefab in corridorPrefabs)
//            {
//                var corPiece = corridorPrefab.GetComponent<CorridorPiece>();
//                if (!corPiece || !corPiece.entrances.Exists(e => e.direction == GetOpposite(nextDir))) continue;

//                HexDirection corridorOutDir = GetCorridorExitDirection(corPiece.entrances, GetOpposite(nextDir));

//                foreach (var nextMapPrefab in mapPrefabs)
//                {
//                    var mapPiece = nextMapPrefab.GetComponent<MapPiece>();
//                    if (!mapPiece || !mapPiece.entrances.Exists(e => e.direction == GetOpposite(corridorOutDir))) continue;

//                    if (TryCorridorRecursive(corridorPrefab, exit.entranceTransform.position, rot, newMap, nextDir, depth + 1))
//                        return true;
//                }
//            }
//        }

//        RemoveLastPiece();
//        return false;
//    }

//    bool TryCorridorRecursive(GameObject corridorPrefab, Vector3 spawnPos, Quaternion rot, GameObject prevMap, HexDirection prevExitDir, int depth)
//    {
//        if (!corridorPrefab) return false;
//        var corComp = corridorPrefab.GetComponent<CorridorPiece>();
//        if (!corComp) return false;

//        GameObject newCorridor = Instantiate(corridorPrefab, spawnPos, rot, gridParent);
//        EntranceInfo prevExit = FindEntrance(prevMap, prevExitDir);
//        EntranceInfo corEntrance = FindAnyEntrance(corComp.entrances, prevExitDir);

//        if (prevExit != null && corEntrance != null)
//            newCorridor.transform.position += prevExit.entranceTransform.position - corEntrance.entranceTransform.position;
//        else
//        {
//            DestroyImmediate(newCorridor);
//            return false;
//        }

//        Bounds newBounds = GetPieceBounds(newCorridor);
//        if (IsOverlap(newBounds))
//        {
//            DestroyImmediate(newCorridor);
//            return false;
//        }

//        placedPieces.Add(newCorridor);
//        pieceBoundsList.Add(newBounds);

//        HexDirection nextMapEntryDir = GetCorridorExitDirection(corComp.entrances, prevExitDir);
//        EntranceInfo corridorExitInfo = FindEntrance(newCorridor, GetOpposite(nextMapEntryDir));

//        if (corridorExitInfo != null)
//        {
//            Vector3 nextSpawnPos = corridorExitInfo.entranceTransform.position;
//            if (TryPlaceRecursive(nextSpawnPos, rot, newCorridor, nextMapEntryDir, depth + 1))
//                return true;
//        }

//        RemoveLastPiece();
//        return false;
//    }

//    bool IsOverlap(Bounds check)
//    {
//        foreach (var b in pieceBoundsList)
//        {
//            if (Vector3.Distance(check.center, b.center) < 0.01f) continue;
//            if (check.Intersects(b)) return true;
//        }
//        return false;
//    }

//    //Bounds GetPieceBounds(GameObject obj)
//    //{
//    //    var col = obj.GetComponentInChildren<Collider2D>();
//    //    return col ? col.bounds : new Bounds(obj.transform.position, Vector3.zero);
//    //}

//    /// GameObject에서 Collider2D의 Bounds를 가져옴
//    Bounds GetPieceBounds(GameObject obj)
//    {
//        var colliders = obj.GetComponentsInChildren<Collider2D>();
//        if (colliders.Length == 0)
//        {
//            var renderers = obj.GetComponentsInChildren<Renderer>();
//            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);

//            Bounds rBounds = renderers[0].bounds;
//            for (int i = 1; i < renderers.Length; i++)
//                rBounds.Encapsulate(renderers[i].bounds);
//            return rBounds;
//        }

//        Bounds bounds = colliders[0].bounds;
//        for (int i = 1; i < colliders.Length; i++)
//            bounds.Encapsulate(colliders[i].bounds);
//        return bounds;
//    }

//    EntranceInfo FindEntrance(GameObject go, HexDirection dir)
//    {
//        var mp = go.GetComponent<MapPiece>();
//        if (mp) return mp.entrances.Find(e => e.direction == dir);
//        var cp = go.GetComponent<CorridorPiece>();
//        return cp?.entrances.Find(e => e.direction == dir);
//    }

//    EntranceInfo FindAnyEntrance(List<EntranceInfo> entrances, HexDirection exitDir)
//    {
//        return entrances.Find(e => e.direction == GetOpposite(exitDir));
//    }

//    HexDirection GetOpposite(HexDirection dir)
//    {
//        return (HexDirection)(((int)dir + 3) % 6);
//    }

//    HexDirection GetCorridorExitDirection(List<EntranceInfo> entrances, HexDirection entryDir)
//    {
//        HexDirection actualEntry = GetOpposite(entryDir);
//        foreach (var e in entrances)
//            if (e.direction != actualEntry) return GetOpposite(e.direction);

//        return GetOpposite(entryDir);
//    }

//    GameObject GetRandomMapPrefab()
//    {
//        List<GameObject> available = new();
//        foreach (var prefab in mapPrefabs)
//            if (!usedMapPrefabs.Contains(prefab)) available.Add(prefab);

//        if (available.Count == 0) available.AddRange(mapPrefabs);
//        if (available.Count == 0) return null;

//        return available[Random.Range(0, available.Count)];
//    }

//    void ClearPieces()
//    {
//        foreach (var p in placedPieces)
//            if (p) DestroyImmediate(p);
//        placedPieces.Clear();
//        pieceBoundsList.Clear();
//        overlapBoundsList.Clear();
//    }

//    void RemoveLastPiece()
//    {
//        if (placedPieces.Count == 0) return;
//        var last = placedPieces[^1];
//        if (last) DestroyImmediate(last);
//        placedPieces.RemoveAt(placedPieces.Count - 1);
//        if (pieceBoundsList.Count > 0) pieceBoundsList.RemoveAt(pieceBoundsList.Count - 1);
//    }

//    void SetPlayerAndTilemaps()
//    {
//        if (placedPieces.Count == 0) return;

//        var firstMap = placedPieces[0];
//        Transform playerStart = firstMap.transform.Find("PlayerStart");
//        Vector3 spawnPos = playerStart ? playerStart.position : firstMap.transform.position;

//        CharacterMove player = FindObjectOfType<CharacterMove>();
//        if (!player && playerPrefab)
//            player = Instantiate(playerPrefab, spawnPos, Quaternion.identity).GetComponent<CharacterMove>();
//        else if (player)
//            player.transform.position = spawnPos;
//    }

//    void OnDrawGizmos()
//    {
//        Gizmos.color = Color.yellow;
//        foreach (var bounds in pieceBoundsList)
//            Gizmos.DrawWireCube(bounds.center, bounds.size);

//        Gizmos.color = Color.red;
//        foreach (var bounds in overlapBoundsList)
//            Gizmos.DrawWireCube(bounds.center, bounds.size);
//    }
//}
