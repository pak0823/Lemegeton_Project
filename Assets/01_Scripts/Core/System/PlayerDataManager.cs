using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.AddressableAssets; // 어드레서블 네임스페이스
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 핸들
using System.Threading.Tasks; // async/await 사용 시

[System.Serializable]
public class SaveData
{
    public List<InventoryItem> inventory;
    public int gold;
    // 여기에 보유 유닛(ownedUnits) 정보 등도 포함시켜라
}

public class PlayerDataManager : MonoBehaviour
{
    // 싱글톤 패턴 (어디서든 접근 가능하게)
    public static PlayerDataManager Instance;

    [Header("1. [설정용] 시작 시 보유할 유닛 (주소값)")]
    // 기존: public List<UnitData> ownedUnits; -> 이건 이제 인스펙터에서 안 씀
    // 변경: AssetReferenceT<UnitData>를 써서 특정 타입만 넣게 강제함 (실수 방지)
    public List<AssetReferenceT<UnitData>> startingUnitRefs = new List<AssetReferenceT<UnitData>>();

    [Header("2. [런타임] 실제 로딩된 유닛들")]
    // 게임 로직(UI, 배틀 등)은 여전히 이 리스트를 씀. (기존 코드 호환성 100%)
    public List<UnitData> ownedUnits = new List<UnitData>();

    [Header("전투 진형")]
    public UnitData[] formation = new UnitData[19];

    public event Action OnFormationChanged;
    public event Action OnUnitsLoaded; // 로딩 완료 알림 이벤트 추가

    // 로딩 상태 확인용
    public bool IsLoading { get; private set; } = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 넘어가도 파괴 안 됨
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 생성되자마자 로딩 시작
        if(Instance == null)
            LoadStartingUnitsByLabel();

        // 게임 시작 시 자동으로 로드 시도
        LoadGame();
    }
    // === 어드레서블 로딩 로직 ===
    public async void LoadStartingUnitsByLabel()
    {
        // "StartingUnit" 라벨이 붙은 모든 UnitData를 가져와라!
        var handle = Addressables.LoadAssetsAsync<UnitData>("StartingUnit", (loadedUnit) =>
        {
            // 하나씩 로딩될 때마다 실행됨
            if (loadedUnit != null)
            {
                ownedUnits.Add(loadedUnit);
                Debug.Log($"[라벨 로딩] 유닛 획득: {loadedUnit.DisplayName}");
            }
        });

        await handle.Task; // 다 끝날 때까지 대기
        IsLoading = false;
        OnUnitsLoaded?.Invoke();
        Debug.Log("초기 유닛 로딩 완료!");
    }

    // 게임 도중 유닛을 획득했을 때 로딩하는 함수
    public async void AddUnitByAddress(AssetReferenceT<UnitData> unitRef)
    {
        if (unitRef == null) return;

        var handle = unitRef.LoadAssetAsync();
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            ownedUnits.Add(handle.Result);
            Debug.Log($"[UnitGet] 신규 유닛 획득: {handle.Result.DisplayName}");
        }
    }
    public UnitData GetOwnedUnit(int index)
    {
        // 인덱스 범위 체크 (에러 방지용)
        if (index < 0 || index >= ownedUnits.Count)
        {
            Debug.LogWarning($"[PlayerData] 인덱스 {index}에 해당하는 유닛이 없습니다.");
            return null;
        }
        return ownedUnits[index];
    }

    // 진형 설정 함수
    public void SetFormation(int targetIndex, UnitData incomingUnit)
    {
        // 들어오려는 유닛(incomingUnit)이 이미 진형 어딘가에 있는지 찾음
        int oldIndex = -1;
        for (int i = 0; i < formation.Length; i++)
        {
            if (formation[i] == incomingUnit)
            {
                oldIndex = i;
                break;
            }
        }

        // 목표 자리(targetIndex)에 원래 있던 유닛을 기억 (없으면 null)
        UnitData unitAtTarget = formation[targetIndex];

        // 로직 분기
        if (oldIndex != -1) // Case A: 이미 배치된 유닛이다 -> 스왑 (Swap)
        {
            if (oldIndex == targetIndex) return; // 제자리 클릭이면 무시

            // 스왑 로직
            formation[targetIndex] = incomingUnit; // 목표 자리에 내 유닛
            formation[oldIndex] = unitAtTarget;    // 내 원래 자리에 쫓겨난 유닛

            Debug.Log($"[진형 변경] {incomingUnit.DisplayName}({oldIndex}) <-> {(unitAtTarget != null ? unitAtTarget.DisplayName : "빈칸")}({targetIndex}) 위치 교체 완료.");
        }
        else // Case B: 진형에 없던 새 유닛이다 -> 덮어쓰기 (Overwrite)
        {
            formation[targetIndex] = incomingUnit;
            // (unitAtTarget은 갈 곳이 없으므로 그냥 사라짐 - 덮어쓰기)

            Debug.Log($"[진형 배치] {targetIndex}번에 {incomingUnit.DisplayName} 신규 배치됨.");
        }

        // 변경 사항이 생겼으니 구독자들에게 알림
        OnFormationChanged?.Invoke();
    }

    // 해당 인덱스에 누가 있는지 확인
    public UnitData GetUnitAt(int index)
    {
        if (index < 0 || index >= formation.Length) return null;
        return formation[index];
    }

    public void SaveGame()
    {
        SaveData data = new SaveData();
        data.inventory = InventoryManager.Instance.GetSaveData();
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

        // 인벤토리 매니저에 데이터 주입
        InventoryManager.Instance.LoadData(data.inventory);
        CurrencyManager.Instance.gold = data.gold;

        Debug.Log("데이터 로드 완료.");
    }
    private void InitNewGame()
    {
        // 처음 시작할 때 필요한 기본값 설정
        // 인벤토리는 InventoryManager의 Dictionary가 생성될 때 이미 비어있으므로 
        // 특별히 추가할 게 없다면 그냥 놔두면 됨 (자동으로 모든 재료 0개)

        CurrencyManager.Instance.gold = 500; // 초기 자금 정도만 설정

        // 필요하다면 초기 지급 아이템 추가
        // InventoryManager.Instance.AddItem("MAT_WOOD", 1); 

        // 초기 상태를 한 번 저장해두는 것도 방법
        SaveGame();
    }
}