// StageWaveRegistry.cs

using System.Collections.Generic;

using System.Linq;

using UnityEngine;



[CreateAssetMenu(menuName = "Battle/Wave/Registry")]

public class StageWaveRegistry : ScriptableObject

{

    //entries 리스트에 stageId(키)와 WaveSet(값) 쌍을 등록하여 BattleManager에서 찾아 자동으로 할당됨

    [System.Serializable]

    public class Entry

    {

        public string stageId;   // 예: "Stage_1_1" 혹은 씬 이름

        public WaveSet waveSet;

    }



    public List<Entry> entries = new();



    public WaveSet Find(string id)

    {

        if (string.IsNullOrEmpty(id)) return null;

        // 완전 일치 우선 → (옵션) 대소문자 무시

        var e = entries.FirstOrDefault(x => x != null && x.stageId == id)

             ?? entries.FirstOrDefault(x => x != null && x.stageId.Equals(id, System.StringComparison.OrdinalIgnoreCase));

        return e != null ? e.waveSet : null;

    }

}

