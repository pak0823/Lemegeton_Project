using System.Collections.Generic;

using UnityEngine;



[CreateAssetMenu(menuName = "Battle/Wave/WaveSet")]

public class WaveSet : ScriptableObject

{
    [System.Serializable]
    public class EnemySpawnInfo
    {
        public GameObject enemyPrefab; // 소환할 몬스터(박쥐, 슬라임 등)
        public Vector3Int spawnCell;   // 생성될 그리드 좌표
    }

    [System.Serializable]

    public class WaveDef

    {

        [Header("Legacy Spawns (통짜 프리팹)")]
        [Tooltip("적들이 미리 배치된 통짜 프리팹. 이 값이 있으면 아래 동적 스폰 목록을 무시하고 이를 최우선으로 사용합니다.")]
        public GameObject enemyLayoutPrefab; // 적 유닛들을 미리 배치해 둔 프리팹(자식에 BattleUnit)

        public string label;                 // (옵션) “정예 정찰대” 등


        [Header("Data-Driven Spawns (동적 스폰)")]
        [Tooltip("개별 몬스터와 소환될 타일(그리드) 좌표 목록을 설정합니다. (enemyLayoutPrefab이 비어있을 때 작동)")]
        public List<EnemySpawnInfo> enemySpawns = new();
    }

    public List<WaveDef> waves = new();

    [System.Serializable]
    public class RewardProfile
    {
        [Tooltip("기본 보상 몬스터 드랍량(및 확률) 배수 (1.0 = 기본)")]
        public float rewardMultiplier = 1.0f;

        [Tooltip("해당 웨이브 세트(전투) 클리어 시 무조건 확정으로 얻는 보상 목록")]
        public List<RewardData> guaranteedRewards = new();
    }

    [Header("Reward Config (클리어 보상 세팅)")]
    public RewardProfile rewardProfile = new RewardProfile();
}

