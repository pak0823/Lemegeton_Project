using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛의 생명이 감소할 때 대응 중첩을 1 부여.
/// 대응 중첩이 threshold(기본 5)에 도달하면 ATB를 가득 채워 즉시 턴을 얻는다.
/// 유닛이 자신의 턴을 끝낼 때마다 대응 중첩을 모두 제거한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Passives/Counter Stack (Reactive Turn)",
    fileName = "Passive_CounterStack")]
public class CounterStackPassive : PassiveAsset
{
    [Header("Counter Stack Settings")]
    [Tooltip("대응 중첩으로 사용할 StatusId")]
    public StatusId counterStatusId = StatusId.CounterStack;

    [Tooltip("이 수치에 도달하면 즉시 턴을 얻습니다.")]
    public int stacksForExtraTurn = 5;

    [Tooltip("즉시 턴을 얻었을 때 패시브 라벨을 표시할지 여부")]
    public bool announceOnTrigger = true;

    // 여러 유닛이 이 패시브를 공유하므로, 오너별로 구분해서 관리
    private readonly HashSet<BattleUnit> _owners = new();
    private readonly Dictionary<BattleUnit, Action<int>> _damageHandlers = new();

    private BattleManager _battle;

    public override void OnAttach(BattleUnit owner, BattleManager battle)
    {
        base.OnAttach(owner, battle);
        if (owner == null || battle == null) return;

        // BattleManager 구독(한 번만)
        if (_battle == null)
        {
            _battle = battle;
            _battle.OnUnitEndTurn += HandleUnitEndTurn;
        }

        if (_owners.Contains(owner)) return;

        _owners.Add(owner);

        // HP 감소 이벤트 구독 owner를 캡처한 핸들러 저장
        Action<int> handler = amount => HandleOwnerDamaged(owner, amount);
        _damageHandlers[owner] = handler;
        owner.OnDamaged += handler;
    }

    public override void OnDetach(BattleUnit owner, BattleManager battle)
    {
        base.OnDetach(owner, battle);
        if (owner == null) return;

        // HP 감소 이벤트 구독 해제
        if (_damageHandlers.TryGetValue(owner, out var handler))
        {
            owner.OnDamaged -= handler;
            _damageHandlers.Remove(owner);
        }

        _owners.Remove(owner);

        // 더 이상 이 패시브를 가진 유닛이 없으면 BattleManager 이벤트도 해제
        if (_owners.Count == 0 && _battle != null)
        {
            _battle.OnUnitEndTurn -= HandleUnitEndTurn;
            _battle = null;
        }
    }

    /// <summary>
    /// 오너의 HP가 감소했을 때 호출됨.
    /// </summary>
    private void HandleOwnerDamaged(BattleUnit owner, int amount)
    {
        if (owner == null) return;
        if (amount <= 0) return;       // 회복/0 피해는 무시
        if (owner.IsDead) return;      // 이미 사망한 경우 무시

        var sc = owner.GetComponent<StatusController>();
        if (sc == null) return;

        // 현재 대응 중첩 읽고 +1
        int cur = sc.GetStacks(counterStatusId);
        int next = cur + 1;

        // UI 상으로도 5까지만 보이게 하고 싶으면 clamp
        if (stacksForExtraTurn > 0)
            next = Mathf.Min(next, stacksForExtraTurn);

        // 지속 턴은 0으로 두고, '턴 종료 시점에 전부 제거'만 사용
        sc.SetStacks(counterStatusId, next, 0);

        // 임계치 도달 → 즉시 턴 준비(ATB를 가득 채움)
        if (stacksForExtraTurn > 0 && next >= stacksForExtraTurn)
        {
            // 이미 준비 상태라도 한 번 더 채워두면 문제는 없음
            owner.ATB = owner.MaxATB;

            if (announceOnTrigger)
            {
                // "대응 발동" 같은 느낌으로 패시브 라벨 표시
                owner.AnnouncePassive(string.IsNullOrEmpty(displayName) ? "대응 발동" : displayName);
            }
        }
    }

    /// <summary>
    /// 유닛이 턴을 마칠 때 호출. 대응 중첩을 모두 제거한다.
    /// </summary>
    private void HandleUnitEndTurn(BattleUnit unit)
    {
        if (unit == null) return;
        if (!_owners.Contains(unit)) return;

        var sc = unit.GetComponent<StatusController>();
        if (sc == null) return;

        sc.Clear(counterStatusId);
    }
}
