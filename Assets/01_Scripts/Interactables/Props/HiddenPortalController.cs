using UnityEngine;

/// <summary>
/// 숨겨진 포탈 컨트롤러.
/// 맵 프리팹에 배치하며, OnEnable 시 PlayerMovement.OnTileStepped를 구독합니다.
/// 플레이어가 triggerTileCell에 도달하면 hiddenMapId의 맵으로 이동합니다.
///
/// [설정 방법]
/// 1. 맵 프리팹에 빈 오브젝트를 생성하고 이 컴포넌트를 부착합니다.
/// 2. Inspector에서 아래 항목을 설정합니다:
///    - triggerTileCell : 플레이어가 밟으면 발동되는 타일 셀 좌표 (Grid 기준)
///    - hiddenMapId     : 이동할 숨겨진 맵의 ID (ExplorationMapData.mapId)
///    - hiddenMapPrefab : 숨겨진 맵의 프리팹 참조
///    - exitSpawn       : 이 맵으로 복귀할 때 플레이어가 나타날 위치
/// </summary>
public class HiddenPortalController : MonoBehaviour
{
    [Header("트리거 설정")]
    [Tooltip("플레이어가 이 셀에 도달하면 숨겨진 맵으로 이동합니다 (Grid 셀 좌표)")]
    public Vector3Int triggerTileCell;

    [Header("목적지 숨겨진 맵")]
    [Tooltip("이동할 숨겨진 맵 ID (ExplorationMapData.mapId와 일치해야 합니다)")]
    public string hiddenMapId;

    [Tooltip("숨겨진 맵 프리팹 (MapConnectionData 등록 없이 직접 참조)")]
    public GameObject hiddenMapPrefab;

    [Header("복귀 지점")]
    [Tooltip("숨겨진 맵에서 복귀할 때 플레이어가 나타날 이 맵의 Transform 위치")]
    public Transform exitSpawnTransform;

    [Header("디버그")]
    [SerializeField] private bool showGizmo = true;

    private void OnEnable()
    {
        PlayerMovement.OnTileStepped += HandleTileStepped;
    }

    private void OnDisable()
    {
        PlayerMovement.OnTileStepped -= HandleTileStepped;
    }

    /// <summary>
    /// 플레이어가 타일에 도달할 때마다 호출됩니다.
    /// triggerTileCell과 일치하면 숨겨진 맵으로 이동합니다.
    /// </summary>
    private void HandleTileStepped(Vector3Int arrivedCell)
    {
        if (arrivedCell != triggerTileCell) return;
        if (string.IsNullOrEmpty(hiddenMapId))
        {
            Debug.LogWarning("[HiddenPortalController] hiddenMapId가 설정되지 않았습니다.");
            return;
        }
        if (hiddenMapPrefab == null)
        {
            Debug.LogWarning("[HiddenPortalController] hiddenMapPrefab이 설정되지 않았습니다.");
            return;
        }
        if (MapTransitionManager.Instance == null)
        {
            Debug.LogError("[HiddenPortalController] MapTransitionManager 인스턴스가 없습니다.");
            return;
        }

        // 복귀 태그: 이 맵 ID를 사용 (SpawnPlayerAtArrivalPoint에서 fromMapId로 검색)
        string returnMapId = MapTransitionManager.Instance.CurrentMapId;
        string returnTag = returnMapId;

        Debug.Log($"[HiddenPortalController] 발동: 셀={arrivedCell} → 숨겨진 맵={hiddenMapId}");
        MapTransitionManager.Instance.EnterHiddenMap(hiddenMapId, returnMapId, returnTag, hiddenMapPrefab);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        // Grid 찾아 월드 좌표로 변환
        var tilemap = GetComponentInParent<UnityEngine.Tilemaps.Tilemap>();
        if (tilemap == null) tilemap = FindObjectOfType<UnityEngine.Tilemaps.Tilemap>();
        if (tilemap == null) return;

        Vector3 worldPos = tilemap.GetCellCenterWorld(triggerTileCell);
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.7f);
        Gizmos.DrawSphere(worldPos, 0.25f);
        UnityEditor.Handles.Label(worldPos + Vector3.up * 0.5f,
            $"Hidden Portal\n→ {hiddenMapId}");

        // 복귀 지점 표시
        if (exitSpawnTransform != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.7f);
            Gizmos.DrawSphere(exitSpawnTransform.position, 0.2f);
            UnityEditor.Handles.Label(exitSpawnTransform.position + Vector3.up * 0.4f, "Exit Spawn");
        }
    }
#endif
}
