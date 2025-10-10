using UnityEngine;

[CreateAssetMenu(menuName = "Game/StageDatabase")]
public class StageDatabase : ScriptableObject
{
    [Tooltip("ÄûÁî ¸Ê µ¥ÀÌÅÍ")] public StageQuizMapData[] quizStages;
    [Tooltip("ÀÏ¹İ ¸Ê µ¥ÀÌÅÍ")] public StageNormalMapData[] normalStages;
}