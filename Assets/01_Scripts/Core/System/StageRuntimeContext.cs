// StageRuntimeContext.cs (DontDestroyOnLoad)
using UnityEngine;

public enum BattleContext { TrapEncounter, NormalEncounter, FieldBossEncounter }

public class StageRuntimeContext : MonoBehaviour
{
    private static StageRuntimeContext _instance;
    public static StageRuntimeContext Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<StageRuntimeContext>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[Auto] StageRuntimeContext");
                    _instance = go.AddComponent<StageRuntimeContext>();
                }
            }
            return _instance;
        }
    }

    public int CurrentStageNumber { get; private set; } = -1;
    public string CurrentStageID { get; private set; } = ""; // 문자열 ID 추가

    public BattleContext CurrentBattleContext { get; private set; } = BattleContext.TrapEncounter;

    void Awake()
    {
        if (_instance && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetStageNumber(int n) => CurrentStageNumber = n;
    public void SetStageID(string id) => CurrentStageID = id;
    public void SetBattleContext(BattleContext ctx) => CurrentBattleContext = ctx;

    // --- State Persistence (Map Snapshots) ---
    private System.Collections.Generic.Dictionary<string, ExplorationSnapshot> _mapSnapshots = new();

    public void SaveMapSnapshot(string mapId, ExplorationSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(mapId)) return;
        _mapSnapshots[mapId] = snapshot;
        Debug.Log($"[StageRuntimeContext] Saved Snapshot for Map: {mapId} (Objects: {snapshot?.objects?.Count ?? 0})");
    }

    public ExplorationSnapshot GetMapSnapshot(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return null;
        if (_mapSnapshots.TryGetValue(mapId, out var snap)) return snap;
        return null;
    }

    public void ClearAllSnapshots()
    {
        _mapSnapshots.Clear();
        _mappedChestCounts.Clear();
        _mappedTrapCounts.Clear();
        Debug.Log("[StageRuntimeContext] Cleared all map snapshots and distributions.");
    }

    // --- Object Distribution ---
    private System.Collections.Generic.Dictionary<string, int> _mappedChestCounts = new();
    private System.Collections.Generic.Dictionary<string, int> _mappedTrapCounts = new();

    public void InitializeDistribution(StageNormalMapData data)
    {
        _mappedChestCounts.Clear();
        _mappedTrapCounts.Clear();

        if (data == null || !data.useGlobalObjectCount || data.normalMapPrefabs == null) return;

        int totalChests = data.totalStageChestCount;
        int totalTraps = data.totalStageTrapCount;

        var mapIds = new System.Collections.Generic.List<string>();
        foreach (var prefab in data.normalMapPrefabs)
        {
            if (prefab == null) continue;
            var mapData = prefab.GetComponent<ExplorationMapData>();
            if (mapData != null && !string.IsNullOrEmpty(mapData.mapId))
            {
                mapIds.Add(mapData.mapId);
                _mappedChestCounts[mapData.mapId] = 0;
                _mappedTrapCounts[mapData.mapId] = 0;
            }
        }

        int mapCount = mapIds.Count;
        if (mapCount == 0) return;

        // Random distribution across maps
        for (int i = 0; i < totalChests; i++)
        {
            string randomId = mapIds[Random.Range(0, mapCount)];
            _mappedChestCounts[randomId]++;
        }
        for (int i = 0; i < totalTraps; i++)
        {
            string randomId = mapIds[Random.Range(0, mapCount)];
            _mappedTrapCounts[randomId]++;
        }

        Debug.Log($"[StageRuntimeContext] Distributed Objects: Chests={totalChests}, Traps={totalTraps} among {mapCount} maps.");
    }

    public int GetChestQuota(string mapId, int fallbackValue)
    {
        if (_mappedChestCounts.TryGetValue(mapId, out int count)) return count;
        return fallbackValue;
    }

    public int GetTrapQuota(string mapId, int fallbackValue)
    {
        if (_mappedTrapCounts.TryGetValue(mapId, out int count)) return count;
        return fallbackValue;
    }
}
