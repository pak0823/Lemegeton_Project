using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Passives/LastVorg/Passive_1",
                 fileName = "Passive_LastVorgResearch")]
public class ResearchPassive : PassiveAsset
{
    [Header("Settings")]
    public int maxStacks = 3; // 연구 완료 기준 스택
    public StatusId researchStatusId = StatusId.Research; // 연구 상태 ID

    // BattleUnit.NotifySkillUsed에 의해 호출됨
    private void HandleSkillUsed(SkillAsset skill)
    {
        // 현재 이벤트를 발생시킨 유닛 구하기 (이벤트 구독 방식이므로 sender가 필요하지만, 
        // 여기선 delegate 구조상 owner를 클로저로 잡거나, 이벤트를 owner 멤버로 사용 중)
        // 위 OnAttach 구조상 owner 변수는 캡처되지 않으므로, 
        // BattleUnit에 이벤트를 연결할 때, 핸들러 메서드 내부에서 owner를 특정하기 어렵습니다.
        // 따라서, 아래와 같이 런타임 딕셔너리를 사용하거나, 이벤트를 (SkillAsset) -> (BattleUnit, SkillAsset)으로 확장해야 합니다.
        // 하지만 기존 구조를 최소한으로 건드리기 위해, OnAttach 시점에 람다로 연결합니다.
    }

    // 수정된 OnAttach / OnDetach 로직 (Owner 캡처를 위해)
    // ScriptableObject는 공유 자원이므로, 런타임 상태(어떤 유닛이 구독했는지)를 별도 관리해야 합니다.
    // ShootingInsightPassive 처럼 딕셔너리를 쓰거나, 람다 캡처를 쓸 수 있으나 메모리 누수 방지를 위해 딕셔너리가 안전합니다.

    private Dictionary<BattleUnit, System.Action<SkillAsset>> _handlers = new Dictionary<BattleUnit, System.Action<SkillAsset>>();

    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        if (owner == null) return;

        // 핸들러 생성 (클로저로 owner 캡처)
        System.Action<SkillAsset> handler = (skill) => OnUnitUsedSkill(owner, skill);

        if (!_handlers.ContainsKey(owner))
        {
            _handlers.Add(owner, handler);
            owner.OnSkillUsed += handler;
        }
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        if (owner == null) return;

        if (_handlers.TryGetValue(owner, out var handler))
        {
            owner.OnSkillUsed -= handler;
            _handlers.Remove(owner);
        }
    }

    private void OnUnitUsedSkill(BattleUnit owner, SkillAsset skill)
    {
        // "연구 기술"인지 확인 (SelfStateSkill 타입 체크)
        // 만약 특정 SelfStateSkill만 해당된다면 별도 필터링 필요 (여기선 모든 SelfStateSkill 대상)
        if (skill is SelfStateSkill)
        {
            var statusCtrl = owner.GetComponent<StatusController>();
            if (statusCtrl == null) return;

            int currentStacks = statusCtrl.GetStacks(researchStatusId);

            if (currentStacks >= maxStacks)
            {
                // 3스택 상태에서 사용함 -> 스택 모두 제거 (초기화)
                Debug.Log($"[Research] {owner.name}: 연구 완료 상태에서 기술 사용. 스택 초기화.");
                statusCtrl.SetStacks(researchStatusId, 0);
            }
            else
            {
                // 3스택 미만 -> 스택 +1
                int nextStack = currentStacks + 1;
                Debug.Log($"[Research] {owner.name}: 연구 진행 ({nextStack}/{maxStacks})");
                statusCtrl.SetStacks(researchStatusId, nextStack);

                // 만약 3스택 도달 시 알림이 필요하면 여기서 처리
                if (nextStack == maxStacks)
                {
                    owner.AnnouncePassive("연구 완료"); // "비용 0" 상태 돌입 알림
                }
            }
        }
    }
}