using UnityEngine;
using System.Collections.Generic;



[CreateAssetMenu(menuName = "Data/Stage/NormalMap")]

public class StageNormalMapData : ScriptableObject

{

    [Tooltip("스테이지 번호")] public int stageNumber;

    [Tooltip("일반 맵 프리팹 리스트")] public GameObject[] normalMapPrefabs;



    // 전투 웨이브 세트 (전투씬 자동 할당용)

    [Header("Basic Info")]
    public string stageId; // 문자열 ID (예: "1-1")

    [System.Serializable]
    public class BattleContextData
    {
        public BattleContext contextType;
        public WaveSet waveSet;
    }

    [Header("Waves")]
    public List<BattleContextData> contextWaves = new();


    // 전투 웨이브 세트 (전투씬 자동 할당용)
    public WaveSet GetWaveSet(BattleContext ctx)
    {
        // 리스트에서 Context에 맞는 웨이브 세트 반환
        var found = contextWaves.Find(x => x.contextType == ctx);
        if (found != null && found.waveSet != null) return found.waveSet;

        return null;
    }

}

