using UnityEngine;

[CreateAssetMenu(menuName = "Game/StageDatabase")]
public class StageDatabase : ScriptableObject
{
    [Tooltip("모든 스테이지 데이터 목록")] public StageQuizMapData[] stages;
}