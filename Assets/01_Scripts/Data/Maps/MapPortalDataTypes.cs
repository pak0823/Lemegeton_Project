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

