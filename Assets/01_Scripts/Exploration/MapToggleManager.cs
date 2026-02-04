using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapToggleManager : MonoBehaviour
{
    public static MapToggleManager Instance;

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
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

    }

    (List<Tilemap> floors, List<Tilemap> obstacles, List<Tilemap> walls) FindTilemapsMulti(GameObject map)
    {
        List<Tilemap> floors = new List<Tilemap>();
        List<Tilemap> obstacles = new List<Tilemap>(); // 장애물 리스트
        List<Tilemap> walls = new List<Tilemap>();

        foreach (var tm in map.GetComponentsInChildren<Tilemap>())
        {
            // 벽 (Wall)
            if (tm.CompareTag("Wall"))
            {
                walls.Add(tm);
            }
            // 장애물 (Obstacle)
            else if (tm.CompareTag("Obstacle"))
            {
                obstacles.Add(tm);
            }
            // 바닥 (Floor)
            else if (tm.CompareTag("Floor"))
            {
                floors.Add(tm);
            }
            // 태그가 없는 경우(Untagged)에 대한 처리
            else
            {
                Debug.LogWarning($"[MapManager] Tag가 설정되지 않은 타일맵 발견: {tm.name}");
            }
        }
        return (floors, obstacles, walls);
    }
    public void EnterQuizMap()
    {
        Input.ResetInputAxes();
        PuzzleManager.Instance.IsPuzzleActive = true;

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
        // stageDB가 null이거나 퀴즈 데이터가 없는 경우 안전 처리
        if (stageDB == null || stageDB.quizStages == null) return null;

        var stageData = stageDB.quizStages.FirstOrDefault(s => s.stageNumber == currentStage);
        if (stageData == null || stageData.quizMapPrefabs == null || stageData.quizMapPrefabs.Length == 0)
            return null;

        int index = Random.Range(0, stageData.quizMapPrefabs.Length);
        return stageData.quizMapPrefabs[index];
    }

    void SetupQuizMap(GameObject quizMap)
    {
        // 장애물 리스트도 함께 받아옴
        var (floorMaps, obstacleMaps, wallMaps) = FindTilemapsMulti(quizMap);

        if (floorMaps.Count == 0)
        {
            Debug.LogError("QuizMap Floor 못 찾음");
            return;
        }

        if (PlayerMovement.Instance != null)
        {
            // SetTilemaps에 obstacleMaps도 전달
            PlayerMovement.Instance.SetTilemaps(floorMaps, obstacleMaps, wallMaps);
            PlayerMovement.Instance.ClearPath();
        }

        // 퍼즐 매니저는 보통 바닥 위주로 동작하므로 기존 유지 (필요시 수정)
        // (단, PuzzleManager가 obstacles를 요구하도록 변경되지 않았다는 가정하에)
        PuzzleManager.Instance?.SetMaps(floorMaps[0], wallMaps);

        MovePlayerToSpawnPoint(quizMap);
    }
    void MovePlayerToSpawnPoint(GameObject map)
    {
        var spawn = map.transform.Find("PlayerStart");
        if (spawn != null)
        {
            Vector3 targetPos = spawn.position;
            targetPos.z = 0f;

            playerTransform.position = targetPos;
            PuzzleManager.Instance?.CacheInitialPlayerPosition(targetPos);
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
