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



    public void EnterQuizMap()
    {
        Input.ResetInputAxes();
        // PuzzleManager removed
        // PuzzleManager.Instance.IsPuzzleActive = true;

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
        if (stageDB == null || stageDB.quizStages == null) return null;

        var stageData = stageDB.quizStages.FirstOrDefault(s => s.stageNumber == currentStage);
        if (stageData == null || stageData.quizMapPrefabs == null || stageData.quizMapPrefabs.Length == 0)
            return null;

        int index = Random.Range(0, stageData.quizMapPrefabs.Length);
        return stageData.quizMapPrefabs[index];
    }

    void SetupQuizMap(GameObject quizMap)
    {
        var mapData = quizMap.GetComponent<ExplorationMapData>();
        if (mapData == null)
        {
            Debug.LogError($"[MapToggleManager] QuizMap '{quizMap.name}'에 ExplorationMapData가 없습니다.");
            return;
        }

        var floorMaps = mapData.floorMaps;
        var wallMaps = mapData.wallMaps;
        var obstacleMaps = mapData.obstacleMaps;

        if (floorMaps.Count == 0)
        {
            Debug.LogError("QuizMap Floor 못 찾음 (ExplorationMapData 확인 필요)");
            return;
        }

        if (PlayerMovement.Instance != null)
        {
            if (PathfindingSystem.Instance != null)
            {
                PathfindingSystem.Instance.Initialize(floorMaps, obstacleMaps, wallMaps, 0);
            }
            PlayerMovement.Instance.ClearPath();
        }

        // PuzzleManager removed
        // PuzzleManager.Instance?.SetMaps(floorMaps[0], wallMaps);

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
            
            // PuzzleManager removed
            // PuzzleManager.Instance?.CacheInitialPlayerPosition(targetPos);
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

