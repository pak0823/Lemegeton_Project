using UnityEngine;

[CreateAssetMenu(menuName = "Game/StageNormalMapData")]
public class StageNormalMapData : ScriptableObject
{
    [Tooltip("스테이지 번호")] public int stageNumber;
    [Tooltip("일반 맵 프리팹 리스트")] public GameObject[] normalMapPrefabs;
}
