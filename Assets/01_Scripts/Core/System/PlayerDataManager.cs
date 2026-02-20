using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

[System.Serializable]
public class SaveData
{
    public List<InventoryItem> inventory;

    // [New] 패시브 해금 상태 통합 (모든 PassiveAsset PlayerPrefs 연동 대체)
    public List<string> unlockedPassiveIds = new List<string>();
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
        statModifiers = new Dictionary<string, int>();
    }

    // [New] 영구적 스탯 변화량 (음수면 감소, 양수면 증가)
    public Dictionary<string, int> statModifiers = new Dictionary<string, int>();
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

    // [New] Addressables 핸들 추적기 — AssetReference 기반 AddUnit 핸들 누수 방지
    private readonly ResourceTracker _tracker = new();

    // [Save System] 패시브 해금 상태 — PlayerPrefs 대체
    private HashSet<string> _unlockedPassiveIds = new HashSet<string>();

    /// <summary>해당 패시브 ID가 해금되었는지 확인. PassiveAsset에서 호출.</summary>
    public bool IsPassiveUnlocked(string passiveId) => _unlockedPassiveIds.Contains(passiveId);

    /// <summary>패시브를 해금하고 즉시 저장. PassiveAsset.Unlock()에서 호출.</summary>
    public void UnlockPassive(string passiveId)
    {
        if (_unlockedPassiveIds.Add(passiveId))
        {
            SaveGame();
            Debug.Log($"[Passive] {passiveId} 해금 및 저장 완료.");
        }
    }

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
            LoadStartingUnitsByLabel().Forget();
        }

        LoadGame();
    }

    private void OnDestroy()
    {
        // AddUnitByAddress를 통해 추적된 Addressables 핸들 일괄 해제
        _tracker.ReleaseAll();
    }

    public async UniTaskVoid LoadStartingUnitsByLabel()
    {
        try
        {
            var units = await _tracker.LoadAssetsAsync<UnitData>("StartingUnit", (loadedUnit) =>
            {
                if (loadedUnit != null)
                {
                    ownedUnits.Add(loadedUnit);
                    InitializeRuntimeData(loadedUnit); // [추가] 초기 상태 생성
                    Debug.Log($"[라벨 로딩] 유닛 획득: {loadedUnit.DisplayName}");
                }
            });

            IsLoading = false;
            OnUnitsLoaded?.Invoke();
            Debug.Log("초기 유닛 로딩 완료!");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            IsLoading = false;
        }
    }

    public async UniTaskVoid AddUnitByAddress(AssetReferenceT<UnitData> unitRef)
    {
        if (unitRef == null) return;

        try
        {
            // ResourceTracker를 통해 핸들을 추적 — OnDestroy시 일괄 해제됨
            var unit = await _tracker.LoadAsync<UnitData>(unitRef.RuntimeKey?.ToString());

            if (unit != null)
            {
                ownedUnits.Add(unit);
                InitializeRuntimeData(unit);
                Debug.Log($"[UnitGet] 신규 유닛 획득: {unit.DisplayName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
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
    // [Phase 1] 소비 아이템 효과 적용 (회복)
    // ========================================================================

    /// <summary>
    /// 유닛의 체력을 최대 체력 비례(ratio)로 회복합니다.
    /// </summary>
    /// <returns>실제 회복된 HP 양</returns>
    public int HealUnit(UnitData unit, float ratio)
    {
        if (unit == null) return 0;

        InitializeRuntimeData(unit);
        var runtime = unitStates[unit];
        var (maxHP, _, _) = unit.CalcMaxStats(); // UnitData의 메서드 재사용

        float oldHP = runtime.currentHP;
		// [Fix] 소수점 버림 처리하여 정수로 회복
		int healAmount = Mathf.FloorToInt(maxHP * ratio);

		// runtime.currentHP도 float이지만 UI 표시를 위해 깔끔한 정수 단위로 관리하고 싶다면 여기서도 Floor/Round 처리 고려
		// 일단은 더해지는 값(healAmount)을 정수로 만듦
		runtime.currentHP = Mathf.Min(maxHP, runtime.currentHP + healAmount);

		// 죽은 상태였다면 부활 처리 (필요 시)
		if (runtime.isDead && runtime.currentHP > 0) runtime.isDead = false;

		// 실제 회복된 양 반환 (정수 차이)
		return Mathf.FloorToInt(runtime.currentHP - oldHP);
    }

    /// <summary>
    /// 유닛의 판단력(MP)을 최대 판단력 비례(ratio)로 회복합니다.
    /// </summary>
    /// <returns>실제 회복된 MP 양</returns>
    public int RestoreMP(UnitData unit, float ratio)
    {
        if (unit == null) return 0;

        InitializeRuntimeData(unit);
        var runtime = unitStates[unit];
        var (_, maxMP, _) = unit.CalcMaxStats();

        float oldMP = runtime.currentMP;
		// [Fix] 소수점 버림 처리하여 정수로 회복
		int restoreAmount = Mathf.FloorToInt(maxMP * ratio);

		runtime.currentMP = Mathf.Min(maxMP, runtime.currentMP + restoreAmount);

		return Mathf.FloorToInt(runtime.currentMP - oldMP);
    }

    // ========================================================================
    // [Phase 2] 스탯 변동 (부상/함정)
    // ========================================================================

    /// <summary>
    /// 유닛의 특정 스탯에 영구적인 변동치(modifier)를 적용합니다.
    /// (예: 함정으로 인해 "STR" -1)
    /// </summary>
    public void ApplyStatModifier(UnitData unit, string statName, int value)
    {
        if (unit == null || value == 0) return;

        InitializeRuntimeData(unit);
        var runtime = unitStates[unit];

        if (!runtime.statModifiers.ContainsKey(statName))
        {
            runtime.statModifiers[statName] = 0;
        }

        runtime.statModifiers[statName] += value;
        Debug.Log($"[PlayerData] {unit.DisplayName}의 {statName} 스탯이 {value}만큼 변동됨. (현재 누적: {runtime.statModifiers[statName]})");
    }

    /// <summary>
    /// 유닛의 최종 스탯(기본값 + 변동치)을 반환합니다.
    /// </summary>
    public int GetFinalStat(UnitData unit, string statName)
    {
        if (unit == null) return 0;

        // 기본 스탯 조회 (Reflection 대신 switch-case 권장)
        int baseValue = 0;
        switch (statName)
        {
            case "STR": baseValue = unit.baseSTR; break;
            case "CLV": baseValue = unit.baseCLV; break;
            case "AGI": baseValue = unit.baseAGI; break;
            case "BDY": baseValue = unit.baseBDY; break;
            case "MND": baseValue = unit.baseMND; break;
            case "INS": baseValue = unit.baseINS; break;
        }

        // 변동치 적용
        if (unitStates.TryGetValue(unit, out RuntimeUnitData runtime))
        {
            if (runtime.statModifiers.TryGetValue(statName, out int mod))
            {
                baseValue += mod;
            }
        }

        return Mathf.Max(0, baseValue); // 스탯은 음수가 될 수 없음
    }

    // ========================================================================

    // [Save System] =============================================
    // PlayerPrefs 제거 → Application.persistentDataPath 기반 JSON 파일 저장
    // ============================================================

    private static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "savedata.json");

    public void SaveGame()
    {
        SaveData data = new SaveData();

        // 1. 인벤토리 저장
        if (InventoryManager.Instance != null)
            data.inventory = InventoryManager.Instance.GetSaveData();

        // 2. 패시브 해금 상태 저장 (PlayerDataManager.Instance 담당)
        data.unlockedPassiveIds = new List<string>(_unlockedPassiveIds);

        string json = JsonUtility.ToJson(data, true);
        System.IO.File.WriteAllText(SavePath, json, System.Text.Encoding.UTF8);
        Debug.Log($"[Save] 저장 완료: {SavePath}");
    }

    public void LoadGame()
    {
        if (!System.IO.File.Exists(SavePath))
        {
            Debug.Log("[Save] 저장 파일 없음. 신규 게임 시작.");
            return;
        }

        string json = System.IO.File.ReadAllText(SavePath, System.Text.Encoding.UTF8);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data == null) return;

        // 1. 인벤토리 복원
        if (InventoryManager.Instance != null && data.inventory != null)
            InventoryManager.Instance.LoadData(data.inventory);

        // 2. 패시브 해금 상태 복원
        if (data.unlockedPassiveIds != null)
            _unlockedPassiveIds = new HashSet<string>(data.unlockedPassiveIds);

        Debug.Log("[Save] 데이터 로드 완료.");
    }

    private void InitNewGame()
    {
        SaveGame();
    }
}
