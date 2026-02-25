using UnityEngine;
using System.Collections.Generic;



[CreateAssetMenu(menuName = "Data/Stage/NormalMap")]

public class StageNormalMapData : ScriptableObject

{

    [Tooltip("스테이지 번호")] public int stageNumber;

    [Tooltip("일반 맵 프리팹 리스트")] public GameObject[] normalMapPrefabs;

    [Header("스테이지 단위 오브젝트 배치 설정")]
    [Tooltip("활성화 시 이 스테이지의 전체 맵에 걸쳐 총 상자/함정 개수를 분배하여 생성합니다.")]
    public bool useGlobalObjectCount = false;

    [Tooltip("스테이지 전체에 배치될 총 상자의 개수")]
    public int totalStageChestCount = 5;

    [Tooltip("스테이지 전체에 배치될 총 함정의 개수")]
    public int totalStageTrapCount = 3;

    [Header("맵 연결 구조")]
    [Tooltip("이 스테이지의 맵 간 연결 관계를 정의하는 SO. 포탈 이동 시 사용됩니다.")]
    public MapConnectionData mapConnectionData;

    // 전투 웨이브 세트 (전투씬 자동 할당용)

    [Header("Basic Info")]
    public string stageId; // 문자열 ID (예: "1-1")

    [System.Serializable]
    public class BattleContextData
    {
        public BattleContext contextType;
        [Tooltip("하위 호환성을 유지하기 위한 구버전 단일 웨이브 필드입니다. 가급적 아래 Wave Pool을 사용해주세요.")]
        public WaveSet waveSet;
        [Tooltip("해당 컨텍스트 발생 시 무작위로 추첨할 웨이브 후보군 타임라인 배열입니다.")]
        public List<WaveSet> wavePool = new();
    }

    [Header("Waves")]
    public List<BattleContextData> contextWaves = new();


    // 전투 웨이브 세트 (전투씬 자동 할당용)
    public WaveSet GetWaveSet(BattleContext ctx)
    {
        // 리스트에서 Context에 맞는 데이터 번들 반환
        var found = contextWaves.Find(x => x.contextType == ctx);

        if (found != null)
        {
            if (found.wavePool != null && found.wavePool.Count > 0)
            {
                // 여러 개의 웨이브 세트가 준비되어 있다면 그 중 하나를 무작위로 반환
                int randomIndex = Random.Range(0, found.wavePool.Count);
                return found.wavePool[randomIndex];
            }
            // wavePool이 비어있으면 레거시 waveSet 반환
            return found.waveSet;
        }

        return null;
    }

}

