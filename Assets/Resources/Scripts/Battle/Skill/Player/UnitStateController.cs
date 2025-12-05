using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnitStateId 
{ 
    // 1 - 50: 유닛 스킬 상태
    None = 0,
    Support = 1, // 플루이드 지원 상태
    Steam = 2,  // 럭키식스 증기 상태
    Isolation = 3,   //기간트 쇄국 상태
    Guard = 4,   //기간트 경계 상태
    Ambush = 5,    //잠복

    // 51 - 100: 공통 상태
    Moribundity = 51, //빈사 상태
    Confusion = 52, // 혼란 상태
    Fury = 53, // 격양 상태
    Fear = 54,   //공포 상태
}
public enum UnitStateBuffId
{
    None = 0,
    AgiUp = 1,  //민첩 강화
    MagicUp = 2, //총명 강화
    InsightDown = 3, //통찰 약화

    Target_AgiDown = 4, // 민첩 디버프
    Self_AtkUp = 5,     // 물리 대미지 버프

    AmbushAgiCancel = 100   //잠복 AGI 페널티 상쇄용
}

/// <summary>
/// 비-스택/비-CC, 무기한 유지형 상태 관리.
/// - 같은 상태 재시전 시 '유지(덮어쓰기 개념이지만 지속시간이 없으므로 변화 없음)'.
/// - 턴틱/만료 개념 없음. 오직 스킬/코드로 Remove.
/// - 대미지/스탯 배수는 여기서 계산하지 않음(스킬에서 조회하여 처리).
/// </summary>
[DisallowMultipleComponent]
public class UnitStateController : MonoBehaviour
{
    private readonly HashSet<UnitStateId> _active = new();        // 본래 상태
    private readonly HashSet<UnitStateBuffId> _buffs = new();     // 버프 상태
    readonly Dictionary<UnitStateId, int> _durations = new();   //턴이 지나면 사라지는 상태용 duration 테이블
    readonly Dictionary<UnitStateBuffId, int> _buffDurations = new();

    public event Action OnStatesChanged;
    public event Action OnBuffsChanged;

    private BattleUnit _owner;

    int _forcedMoveImmuneTurns;

    void OnEnable()
    {
        _owner = GetComponent<BattleUnit>();
        if (_owner != null) _owner.OnDied += OnOwnerDied;
    }

    void OnDisable()
    {
        if (_owner != null) _owner.OnDied -= OnOwnerDied;
    }

    private void OnOwnerDied(BattleUnit dead)
    {
        RemoveAll();
        RemoveAllBuffs();
    }

    /// <summary>상태 부여(이미 있으면 그대로 유지)</summary>
    public bool Apply(UnitStateId id)
    {
        bool added = _active.Add(id);
        if (added) OnStatesChanged?.Invoke();
        return added;
    }

    /// <summary>단일 상태 제거</summary>
    public bool Remove(UnitStateId id)
    {
        bool removed = _active.Remove(id);
        _durations.Remove(id);
        if (removed) OnStatesChanged?.Invoke();
        return removed;
    }

    /// <summary>모든 상태 제거</summary>
    public void RemoveAll()
    {
        if (_active.Count == 0) return;
        _active.Clear();
        _durations.Clear();
        OnStatesChanged?.Invoke();
    }

    /// <summary>특정 상태 보유 여부</summary>
    public bool Has(UnitStateId id) => _active.Contains(id);

    // ===== 버프(Buff) API =====
    public bool ApplyBuff(UnitStateBuffId id)
    {
        bool added = _buffs.Add(id);

        _buffDurations.Remove(id);

        if (added) OnBuffsChanged?.Invoke();
        return added;
    }
    // turnCount 동안 유지되는 상태 부여.
    // turnCount <= 0 이면 그냥 Apply와 동일하게 취급(무기한).
    // 이미 같은 상태가 있으면 duration을 새 값으로 갱신.
    public bool ApplyForTurns(UnitStateId id, int turnCount)
    {
        if (turnCount <= 0)
        {
            // 무기한으로 그냥 등록
            return Apply(id);
        }

        bool added = _active.Add(id);
        _durations[id] = turnCount;
        OnStatesChanged?.Invoke();
        return added;
    }
    public bool ApplyBuffForTurns(UnitStateBuffId id, int turnCount)
    {
        if (turnCount <= 0)
        {
            // 0이하면 무기한 처리
            return ApplyBuff(id);
        }

        bool added = _buffs.Add(id);
        _buffDurations[id] = turnCount;
        OnBuffsChanged?.Invoke();
        return added;
    }
    public bool RemoveBuff(UnitStateBuffId id)
    {
        bool removed = _buffs.Remove(id);
        _buffDurations.Remove(id);
        if (removed) OnBuffsChanged?.Invoke();
        return removed;
    }
    public void RemoveAllBuffs()
    {
        if (_buffs.Count == 0) return;
        _buffs.Clear();
        _buffDurations.Clear();
        OnBuffsChanged?.Invoke();
    }

    public void ApplyForcedMoveImmunityForTurns(int turns)
    {
        _forcedMoveImmuneTurns = Mathf.Max(_forcedMoveImmuneTurns, turns);
    }

    public bool IsForcedMoveImmune => _forcedMoveImmuneTurns > 0;

    public int GetRemainingTurns(UnitStateId id)
    {
        if (_durations.TryGetValue(id, out var turns))
            return turns;
        return -1; // -1이면 “무기한”으로 취급
    }

    // 이 유닛의 턴이 시작될 때 호출.
    // duration이 붙은 상태들의 남은 턴 수를 1씩 감소시키고 0 이하이면 제거.
    public void OnTurnStart()
    {
        if (_durations.Count > 0)
        {
            bool changed = false;
            var toRemove = new List<UnitStateId>();

            var keys = new List<UnitStateId>(_durations.Keys);
            foreach (var id in keys)
            {
                int remain = _durations[id] - 1;
                if (remain <= 0)
                {
                    _durations.Remove(id);
                    if (_active.Remove(id))
                        changed = true;
                }
                else
                {
                    _durations[id] = remain;
                }
            }

            if (changed)
                OnStatesChanged?.Invoke();
        }

        // 버프(UnitStateBuffId) 쪽 처리 추가
        if (_buffDurations.Count > 0)
        {
            bool changedBuff = false;
            var buffKeys = new List<UnitStateBuffId>(_buffDurations.Keys);

            foreach (var id in buffKeys)
            {
                int remain = _buffDurations[id] - 1;
                if (remain <= 0)
                {
                    _buffDurations.Remove(id);
                    if (_buffs.Remove(id))
                        changedBuff = true;
                }
                else
                {
                    _buffDurations[id] = remain;
                }
            }

            if (changedBuff)
                OnBuffsChanged?.Invoke();
        }
    }

    public bool HasBuff(UnitStateBuffId id) => _buffs.Contains(id);
    public IReadOnlyCollection<UnitStateBuffId> GetAllBuffs() => _buffs;

    /// <summary>활성 상태 열람</summary>
    public IReadOnlyCollection<UnitStateId> GetAll() => _active;

    /// <summary>UI 등 간단 태그 문자열</summary>
    public IEnumerable<string> GetActiveTags()
    {
        foreach (var s in _active) yield return s.ToString();
    }
}
