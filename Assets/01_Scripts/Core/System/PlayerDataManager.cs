using UnityEngine;
using System.Collections.Generic;
using System;

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

    [Header("보유 유닛 리스트")]
    public List<UnitData> ownedUnits = new List<UnitData>();

    [Header("전투 진형 (0~18번 인덱스)")]
    // Key: 타일 인덱스(0~18), Value: 배치된 유닛 데이터
    public UnitData[] formation = new UnitData[19];

    // 진형이 변경될 때마다 호출될 이벤트
    public event Action OnFormationChanged;

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
        // 게임 시작 시 자동으로 로드 시도
        LoadGame();
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