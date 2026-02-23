using UnityEngine;

/// <summary>
/// 포탈로 도착했을 때 플레이어가 스폰될 위치를 정의합니다.
/// 어느 맵에서 왔는지에 따라 스폰 위치를 다르게 지정할 수 있습니다.
/// </summary>
[System.Serializable]
public class PortalArrivalPoint
{
    [Tooltip("어느 맵 ID에서 도착했을 때 이 스폰 포인트를 사용할지 (예: \"moat\", \"camp\"). 비워두면 기본 도착 위치로 사용됩니다.")]
    public string fromMapId;

    [Tooltip("플레이어가 스폰될 위치의 Transform")]
    public Transform spawnTransform;
}

/// <summary>
/// 숨겨진 포탈 데이터를 정의합니다.
/// 특정 타일에 플레이어가 도달하면 숨겨진 맵으로 이동하고,
/// 숨겨진 맵에서 나올 때 이 맵의 exitSpawnTransform 위치로 복귀합니다.
/// </summary>
[System.Serializable]
public class HiddenPortalData
{
    [Tooltip("이 숨겨진 포탈이 연결하는 맵 ID (ExplorationMapData.mapId와 일치)")]
    public string hiddenMapId;

    [Tooltip("숨겨진 맵 프리팹 (MapConnectionData에 등록 없이 직접 참조)")]
    public GameObject hiddenMapPrefab;

    [Tooltip("숨겨진 맵에서 복귀할 때 플레이어가 나타날 이 맵의 위치")]
    public Transform exitSpawnTransform;

    [Tooltip("이 포탈을 활성화하는 타일 좌표 (Grid 기준). HiddenPortalController에서 설정합니다.")]
    public Vector3Int triggerTileCell;
}
