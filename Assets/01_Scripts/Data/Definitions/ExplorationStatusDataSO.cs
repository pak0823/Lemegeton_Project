using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ExplorationStatusData", menuName = "Data/Definition/ExplorationStatus")]
public class ExplorationStatusDataSO : ScriptableObject
{
    [System.Serializable]
    public class StatusData
    {
        public ExplorationStatusID id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public bool isDebuff; // 디버프 여부 (UI 색상 구분 등 용도)
    }

    public List<StatusData> statusList = new List<StatusData>();

    public StatusData GetData(ExplorationStatusID id)
    {
        return statusList.Find(x => x.id == id);
    }
}
