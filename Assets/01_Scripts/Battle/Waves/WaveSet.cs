using System.Collections.Generic;

using UnityEngine;



[CreateAssetMenu(menuName = "Battle/Wave/WaveSet")]

public class WaveSet : ScriptableObject

{

    [System.Serializable]

    public class WaveDef

    {

        public GameObject enemyLayoutPrefab; // 적 유닛들을 미리 배치해 둔 프리팹(자식에 BattleUnit)

        public string label;                 // (옵션) “정예 정찰대” 등

    }

    public List<WaveDef> waves = new();

}

