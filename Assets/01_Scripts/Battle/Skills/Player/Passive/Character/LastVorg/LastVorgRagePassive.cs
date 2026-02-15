using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Battle/Skill/Player/Passive/LastVorg/Rage")]
public class LastVorgRagePassive : PassiveAsset
{
    public float rageRegenRatio = 0.5f;
    // 런타임 상태 관리용 (유닛별 스킬 사용 여부 & 핸들러 보관)

    private class UnitRuntimeData

    {

        public bool usedResearchSkillThisTurn = false;

        public System.Action<SkillAsset> onSkillUsed;

        public System.Action<BattleUnit> onTurnEnd;

        public System.Action<BattleUnit> onTurnStart;

        public System.Action<BattleUnit> onOverwork;

    }



    private readonly Dictionary<BattleUnit, UnitRuntimeData> _runtimeDataMap = new Dictionary<BattleUnit, UnitRuntimeData>();



    public override void OnAttach(BattleUnit owner, BattleManager battle)

    {

        if (owner == null || battle == null) return;



        // 데이터 초기화

        var data = new UnitRuntimeData();

        _runtimeDataMap[owner] = data;



        // 1. 스킬 사용 감지 핸들러

        data.onSkillUsed = (skill) =>

        {

            // 연구 기술(SelfStateSkill)인지 확인

            // (이전 패시브 구현에서 연구 기술을 SelfStateSkill로 정의함)

            if (skill is SelfStateSkill)

            {

                data.usedResearchSkillThisTurn = true;

                // Debug.Log($"[Passive:Rage] {owner.name} 연구 기술 사용함. 턴 종료 보너스 무효화.");

            }

        };



        // 2. 턴 종료 핸들러

        data.onTurnEnd = (unit) =>

        {

            if (unit == owner)

            {

                // 연구 기술을 안 썼다면 분노 회복

                if (!data.usedResearchSkillThisTurn)

                {

                    // 소수점 버림 처리

                    float rawAmount = owner.MaxRage * rageRegenRatio;

                    float amount = Mathf.Floor(rawAmount);

                    if (amount > 0)

                    {

                        owner.AddRage(amount);

                        owner.AnnouncePassive(displayName); // 발동 알림

                        Debug.Log($"[Passive:Rage] {owner.name}: 연구 기술 미사용 -> Rage 회복 (+{amount:F1})");

                    }

                }

            }

        };



        // 3. 턴 시작 핸들러 (플래그 리셋)

        data.onTurnStart = (unit) =>

        {

            if (unit == owner)

            {

                data.usedResearchSkillThisTurn = false;

            }

        };



        // 과로 핸들러 (턴 정산 + 다음 턴 초기화 동시 수행)

        data.onOverwork = (unit) =>

        {

            if (unit == owner)

            {

                // 지금까지의 행동에 대한 보상 정산

                data.onTurnEnd(unit);

                // 새로운 추가 턴을 위해 플래그 초기화

                data.onTurnStart(unit);



                Debug.Log($"[Passive:Rage] {owner.name} 과로 발동 -> 패시브 턴 정산 및 리셋 완료");

            }

        };



        // 이벤트 구독

        owner.OnSkillUsed += data.onSkillUsed;

        battle.OnUnitEndTurn += data.onTurnEnd;

        BattleManager.OnAnyUnitTurnStarted += data.onTurnStart;

        battle.OnOverworkTriggered += data.onOverwork;

    }



    public override void OnDetach(BattleUnit owner, BattleManager battle)

    {

        if (owner == null) return;



        if (_runtimeDataMap.TryGetValue(owner, out var data))

        {

            // 이벤트 해제

            owner.OnSkillUsed -= data.onSkillUsed;

            if (battle != null) battle.OnUnitEndTurn -= data.onTurnEnd;

            BattleManager.OnAnyUnitTurnStarted -= data.onTurnStart;

            battle.OnOverworkTriggered -= data.onOverwork;



            _runtimeDataMap.Remove(owner);

        }

    }

}
