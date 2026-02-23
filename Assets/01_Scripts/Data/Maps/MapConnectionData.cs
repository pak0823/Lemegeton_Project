using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 맵 연결 구조를 정의하는 ScriptableObject.
/// 새 스테이지 추가 시 이 SO 에셋만 새로 만들면 되며, 코드 수정이 불필요합니다.
/// </summary>
[CreateAssetMenu(menuName = "Data/Map/MapConnectionData")]
public class MapConnectionData : ScriptableObject
{
    [System.Serializable]
    public class MapNode
    {
        [Tooltip("이 맵의 로컬 ID (예: \"moat\", \"camp\"). ExplorationMapData.mapId와 일치해야 합니다.")]
        public string mapId;

        [Tooltip("이 맵에 해당하는 프리팹")]
        public GameObject mapPrefab;

        [Tooltip("이 맵에서 이동 가능한 맵 ID 목록")]
        public List<string> connectedMaps = new List<string>();
    }

    [Tooltip("이 스테이지에 속한 모든 맵 노드 목록")]
    public List<MapNode> mapNodes = new List<MapNode>();

    /// <summary>
    /// from 맵에서 to 맵으로 이동이 가능한지 확인합니다.
    /// </summary>
    public bool CanTravel(string from, string to)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return false;

        var node = GetNode(from);
        if (node == null) return false;

        return node.connectedMaps.Contains(to);
    }

    /// <summary>
    /// 맵 ID에 해당하는 프리팹을 반환합니다.
    /// </summary>
    public GameObject GetPrefab(string mapId)
    {
        var node = GetNode(mapId);
        return node?.mapPrefab;
    }

    /// <summary>
    /// 맵 ID에 해당하는 MapNode를 반환합니다.
    /// </summary>
    public MapNode GetNode(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return null;
        return mapNodes.Find(n => n.mapId == mapId);
    }

    /// <summary>
    /// 해당 맵 ID가 이 스테이지에 존재하는지 확인합니다.
    /// </summary>
    public bool Contains(string mapId)
    {
        return GetNode(mapId) != null;
    }
}
