using UnityEngine;



[CreateAssetMenu(menuName = "Data/Stage/QuizMap")]

public class StageQuizMapData : ScriptableObject

{

    [Tooltip("스테이지 번호")] public int stageNumber;

    [Tooltip("퀴즈맵 프리팹 리스트")] public GameObject[] quizMapPrefabs;

}