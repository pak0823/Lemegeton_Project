using UnityEngine;
using System.Collections.Generic;

public class PlayerDataManager : MonoBehaviour
{
    // 싱글톤 패턴 (어디서든 접근 가능하게)
    public static PlayerDataManager Instance;

    [Header("보유 유닛 리스트")]
    public List<UnitData> ownedUnits = new List<UnitData>();

    [Header("전투 진형 (0~18번 인덱스)")]
    // Key: 타일 인덱스(0~18), Value: 배치된 유닛 데이터
    public UnitData[] formation = new UnitData[19];

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

            // 서로 자리를 바꾼다.
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
    }

    // 해당 인덱스에 누가 있는지 확인
    public UnitData GetUnitAt(int index)
    {
        if (index < 0 || index >= formation.Length) return null;
        return formation[index];
    }
}