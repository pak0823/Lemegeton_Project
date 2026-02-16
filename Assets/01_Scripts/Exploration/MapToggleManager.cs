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



    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

