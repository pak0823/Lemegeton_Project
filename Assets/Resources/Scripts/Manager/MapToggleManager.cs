using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapToggleManager : MonoBehaviour
{
    [Header("맵 참조")]
    public Transform gridParent;
    public GameObject mainMap;
    [SerializeField] private Transform playerTransform;

    [Header("StageData")]
    public StageDatabase stageDB;
    public int currentStage = 1;

    private GameObject activeQuizMap;

    void Awake()
    {
        if (Shared.MapToggleManager == null) Shared.MapToggleManager = this;
        else Destroy(gameObject);

    }

    public void EnterQuizMap()
    {
        Input.ResetInputAxes();
        Shared.PuzzleManager.IsPuzzleActive = true;

        if (mainMap != null) mainMap.SetActive(false);

        if (activeQuizMap != null)
            Destroy(activeQuizMap);

        GameObject quizMapPrefab = GetRandomQuizMapPrefab();
        if (quizMapPrefab == null)
        {
            Debug.LogWarning($"Stage {currentStage}의 퀴즈맵을 찾을 수 없습니다.");
            return;
        }

        activeQuizMap = Instantiate(quizMapPrefab, Vector3.zero, Quaternion.identity, gridParent);
        SetupQuizMap(activeQuizMap);
    }

    public void ExitQuizMap()
    {
        StartCoroutine(Shared.SceneTransitionManager.FadeCoroutine());

        if (activeQuizMap != null)
        {
            Destroy(activeQuizMap);
            activeQuizMap = null;
        }

        if (mainMap != null)
        {
            mainMap.SetActive(true);
            MovePlayerToSpawnPoint(mainMap);
        }
    }

    GameObject GetRandomQuizMapPrefab()
    {
        var stageData = stageDB.quizStages.FirstOrDefault(s => s.stageNumber == currentStage);
        if (stageData == null || stageData.quizMapPrefabs.Length == 0)
            return null;

        int index = Random.Range(0, stageData.quizMapPrefabs.Length);
        return stageData.quizMapPrefabs[index];
    }

    void SetupQuizMap(GameObject quizMap)
    {
        var (floorMap, wallMap) = FindTilemaps(quizMap);

        if (floorMap == null)
        {
            Debug.LogError("Floor 타일맵을 찾을 수 없습니다!");
            return;
        }

        if (Shared.PlayerMovement != null)
        {
            Shared.PlayerMovement.SetTilemap(floorMap, wallMap);
            Shared.PlayerMovement.ClearPath();
            Debug.Log("[MapToggleManager] SetTilemap 호출 완료");
        }
        else
        {
            Debug.LogWarning("[MapToggleManager] PlayerMovement 인스턴스가 아직 준비되지 않음");
        }

        Shared.PuzzleManager?.SetMaps(floorMap, wallMap);
        MovePlayerToSpawnPoint(quizMap);
    }

    (Tilemap floor, Tilemap wall) FindTilemaps(GameObject map)
    {
        Tilemap floor = null, wall = null;
        foreach (var tm in map.GetComponentsInChildren<Tilemap>())
        {
            var name = tm.gameObject.name.ToLower();
            if (name.Contains("floor")) floor = tm;
            if (name.Contains("wall")) wall = tm;
            if (floor != null && wall != null) break;
        }
        return (floor, wall);
    }

    void MovePlayerToSpawnPoint(GameObject map)
    {
        var spawn = map.transform.Find("PlayerStart");
        if (spawn != null)
        {
            playerTransform.position = spawn.position;
            Shared.PuzzleManager?.CacheInitialPlayerPosition(spawn.position);
        }
        else
        {
            Debug.LogWarning("맵에 'PlayerStart' 오브젝트가 없습니다.");
        }
    }

    public Vector3 GetPlayerStartPosition()
    {
        return playerTransform.position;
    }
    public void SetPlayerStartPosition(Transform _playerPos)
    {
         playerTransform = _playerPos;
    }
}
