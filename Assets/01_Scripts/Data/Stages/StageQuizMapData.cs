using UnityEngine;

[CreateAssetMenu(menuName = "Game/StageQuizMapData")]
public class StageQuizMapData : ScriptableObject
{
    [Tooltip("스테이지 번호")] public int stageNumber;
    [Tooltip("퀴즈맵 프리팹 리스트")] public GameObject[] quizMapPrefabs;
}