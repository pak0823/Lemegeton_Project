using UnityEngine;
using System.Collections.Generic;



[CreateAssetMenu(menuName = "Data/Database/Stage")]

public class StageDatabase : ScriptableObject

{

    private static StageDatabase _instance;
    public static StageDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<StageDatabase>("DB/StageDatabase");
                if (_instance == null)
                {
                    Debug.LogError("[StageDatabase] 데이터를 찾을 수 없습니다! 반드시 'Assets/Resources/DB/StageDatabase.asset' 경로에 위치해야 합니다.");
                }
            }
            return _instance;
        }
    }

    [Tooltip("일반 맵 데이터")] public StageNormalMapData[] normalStages;


    private Dictionary<string, StageNormalMapData> _stageDict = null;

    public StageNormalMapData GetStage(string id)
    {
        if (string.IsNullOrEmpty(id) || normalStages == null) return null;

        // 딕셔너리 초기화 (Lazy Init)
        if (_stageDict == null)
        {
            _stageDict = new Dictionary<string, StageNormalMapData>();
            foreach (var stage in normalStages)
            {
                if (stage != null && !string.IsNullOrEmpty(stage.stageId))
                {
                    if (!_stageDict.ContainsKey(stage.stageId))
                        _stageDict.Add(stage.stageId, stage);
                    else
                        Debug.LogWarning($"[StageDatabase] 중복된 Stage ID 발견: {stage.stageId}");
                }
            }
        }

        if (_stageDict.TryGetValue(id, out var foundStage))
            return foundStage;

        return null;
    }

}
