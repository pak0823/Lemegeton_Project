using UnityEngine;



[CreateAssetMenu(menuName = "Data/Stage/NormalMap")]

public class StageNormalMapData : ScriptableObject

{

    [Tooltip("스테이지 번호")] public int stageNumber;

    [Tooltip("일반 맵 프리팹 리스트")] public GameObject[] normalMapPrefabs;



    // 전투 웨이브 세트 (전투씬 자동 할당용)

    [Header("Battle")]

    public WaveSet trapEncounterWave;   // 함정 게이지로 전투 진입 시

    public WaveSet postPuzzleWave;      // 퍼즐 클리어 후 전투 진입 시

}

