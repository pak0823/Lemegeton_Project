using UnityEngine;

[CreateAssetMenu(menuName = "Game/StageDatabase")]
public class StageDatabase : ScriptableObject
{
    [Tooltip("퀴즈 맵 데이터")] public StageQuizMapData[] quizStages;
    [Tooltip("일반 맵 데이터")] public StageNormalMapData[] normalStages;
}