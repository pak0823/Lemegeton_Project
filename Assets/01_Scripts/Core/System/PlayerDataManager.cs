using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

[System.Serializable]
public class SaveData
{
    public List<InventoryItem> inventory;
    public int gold;
}

[System.Serializable]
public class RuntimeUnitData
{
    public float currentHP;
    public float currentMP;
    public float currentRage;
    public bool isDead;

    public RuntimeUnitData(float hp, float mp, float rage)
    {
        currentHP = hp;
        currentMP = mp;
        currentRage = rage;
        isDead = false;
    }
}

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    [Header("1. [설정용] 시작 시 보유할 유닛 (주소값)")]
    public List<AssetReferenceT<UnitData>> startingUnitRefs = new List<AssetReferenceT<UnitData>>();

    [Header("2. [런타임] 실제 로딩된 유닛들")]
    public List<UnitData> ownedUnits = new List<UnitData>();

    // [New] 런타임 상태 저장소
    private Dictionary<UnitData, RuntimeUnitData> unitStates = new Dictionary<UnitData, RuntimeUnitData>();

    [Header("전투 진형")]
    public UnitData[] formation = new UnitData[19];

    public event Action OnFormationChanged;
    public event Action OnUnitsLoaded;

    public bool IsLoading { get; private set; } = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 이미 유닛이 있다면(인스펙터 할당 등) 로딩 완료 처리
        if (ownedUnits.Count > 0)
        {
            IsLoading = false;
            // 기존 유닛들에 대해 런타임 데이터 초기화 보장
            foreach (var unit in ownedUnits)
            {
                InitializeRuntimeData(unit);
            }
        }
        else
        {
            // 유닛이 없다면 어드레서블 로딩 시도
            LoadStartingUnitsByLabel();
        }

        LoadGame();
    }

    public async void LoadStartingUnitsByLabel()
    {
        var handle = Addressables.LoadAssetsAsync<UnitData>("StartingUnit", (loadedUnit) =>
        {
            if (loadedUnit != null)
            {
                ownedUnits.Add(loadedUnit);
                InitializeRuntimeData(loadedUnit); // [추가] 초기 상태 생성
                Debug.Log($"[라벨 로딩] 유닛 획득: {loadedUnit.DisplayName}");
            }
        });

        await handle.Task;
        IsLoading = false;
        OnUnitsLoaded?.Invoke();
        Debug.Log("초기 유닛 로딩 완료!");
    }

    public async void AddUnitByAddress(AssetReferenceT<UnitData> unitRef)
    {
        if (unitRef == null) return;

        var handle = unitRef.LoadAssetAsync();
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            ownedUnits.Add(handle.Result);
            InitializeRuntimeData(handle.Result); // [추가] 초기 상태 생성
            Debug.Log($"[UnitGet] 신규 유닛 획득: {handle.Result.DisplayName}");
        }
    }

    public UnitData GetOwnedUnit(int index)
    {
        if (index < 0 || index >= ownedUnits.Count)
        {
            Debug.LogWarning($"[PlayerData] 인덱스 {index}에 해당하는 유닛이 없습니다.");
            return null;
        }
        return ownedUnits[index];
    }
    
    public int GetOwnedUnitCount()
    {
        return ownedUnits.Count;
    }

    public void SetFormation(int targetIndex, UnitData incomingUnit)
    {
        int oldIndex = -1;
        for (int i = 0; i < formation.Length; i++)
        {
            if (formation[i] == incomingUnit)
            {
                oldIndex = i;
                break;
            }
        }

        UnitData unitAtTarget = formation[targetIndex];

        if (oldIndex != -1)
        {
            if (oldIndex == targetIndex) return;

            formation[targetIndex] = incomingUnit;
            formation[oldIndex] = unitAtTarget;

            Debug.Log($"[진형 변경] {incomingUnit.DisplayName}({oldIndex}) <-> {(unitAtTarget != null ? unitAtTarget.DisplayName : "빈칸")}({targetIndex}) 위치 교체 완료.");
        }
        else
        {
            formation[targetIndex] = incomingUnit;
            Debug.Log($"[진형 배치] {targetIndex}번에 {incomingUnit.DisplayName} 신규 배치됨.");
        }

        OnFormationChanged?.Invoke();
    }

    public UnitData GetUnitAt(int index)
    {
        if (index < 0 || index >= formation.Length) return null;
        return formation[index];
    }

    // ========================================================================
    // [Data Persistence] 전투 <-> 탐험 데이터 동기화
    // ========================================================================

    public void SyncToBattle(BattleUnit battleUnit)
    {
        if (battleUnit == null || battleUnit.data == null) return;

        if (unitStates.TryGetValue(battleUnit.data, out RuntimeUnitData savedState))
        {
            float applyHP = savedState.currentHP;
            if (applyHP <= 0 && !savedState.isDead) applyHP = 1;

            float maxHP = battleUnit.MaxHP;
            float maxMP = battleUnit.MaxMP;
            float maxRage = battleUnit.MaxRage;

            battleUnit.Stats.SetHP(Mathf.Clamp(applyHP, 0, maxHP));
            battleUnit.Stats.SetMP(Mathf.Clamp(savedState.currentMP, 0, maxMP));
            battleUnit.Stats.SetRage(Mathf.Clamp(savedState.currentRage, 0, maxRage));
            
            Debug.Log($"[SyncToBattle] {battleUnit.name} 상태 로드: HP {battleUnit.HP}/{maxHP}");
        }
        else
        {
            Debug.Log($"[SyncToBattle] {battleUnit.name} 신규 데이터. (초기화 상태 유지)");
        }
    }

    public void SyncFromBattle(BattleUnit battleUnit)
    {
        if (battleUnit == null || battleUnit.data == null) return;

        if (!unitStates.ContainsKey(battleUnit.data))
        {
            unitStates[battleUnit.data] = new RuntimeUnitData(battleUnit.HP, battleUnit.MP, battleUnit.Rage);
        }
        else
        {
            var data = unitStates[battleUnit.data];
            data.currentHP = battleUnit.HP;
            data.currentMP = battleUnit.MP;
            data.currentRage = battleUnit.Rage;
            data.isDead = battleUnit.IsDead;
        }

        Debug.Log($"[SyncFromBattle] {battleUnit.name} 상태 저장 완료: HP {battleUnit.HP}");
    }

    public RuntimeUnitData GetRuntimeData(UnitData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[PlayerData] GetRuntimeData called with null UnitData!");
            return null;
        }

        if (unitStates.TryGetValue(data, out RuntimeUnitData savedState))
        {
            // Debug.Log($"[PlayerData] Returning saved data for {data.DisplayName}. HP:{savedState.currentHP}");
            return savedState;
        }
        
        Debug.Log($"[PlayerData] No saved data found for {data.DisplayName}. Total States: {unitStates.Count}");
        return null; 
    }

    private void InitializeRuntimeData(UnitData data)
    {
        if (data == null) return;
        if (unitStates.ContainsKey(data)) return; // 이미 있으면 패스

        // UnitData의 Helper 메서드 사용
        var (maxHP, maxMP, maxRage) = data.CalcMaxStats();
        
        // [수정] MP도 꽉 찬 상태로 시작하도록 변경 (Rage는 0 유지)
        unitStates[data] = new RuntimeUnitData(maxHP, maxMP, 0); 
        Debug.Log($"[PlayerData] {data.DisplayName} 초기 RuntimeData 생성 (HP: {maxHP}, MP: {maxMP})");
    }

    // ========================================================================

    public void SaveGame()
    {
        SaveData data = new SaveData();
        if (InventoryManager.Instance != null)
            data.inventory = InventoryManager.Instance.GetSaveData();
        if (CurrencyManager.Instance != null)
            data.gold = CurrencyManager.Instance.gold;

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("SaveSlot_1", json);
        PlayerPrefs.Save();
    }

    public void LoadGame()
    {
        if (!PlayerPrefs.HasKey("SaveSlot_1")) return;

        string json = PlayerPrefs.GetString("SaveSlot_1");
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.LoadData(data.inventory);
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.gold = data.gold;

        Debug.Log("데이터 로드 완료.");
    }

    private void InitNewGame()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.gold = 500;
        SaveGame();
    }
}