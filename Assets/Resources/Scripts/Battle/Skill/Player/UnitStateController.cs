using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnitStateId 
{ 
    Support = 1, //지원
    Steam = 2,  //증기
    Isolation = 3   //쇄국
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
    private readonly HashSet<UnitStateId> _active = new();

    public event Action OnStatesChanged;

    private BattleUnit _owner;

    void OnEnable()
    {
        _owner = GetComponent<BattleUnit>();
        if (_owner != null) _owner.OnDied += OnOwnerDied;
    }

    void OnDisable()
    {
        if (_owner != null) _owner.OnDied -= OnOwnerDied;
    }

    private void OnOwnerDied(BattleUnit dead) => RemoveAll();

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
        if (removed) OnStatesChanged?.Invoke();
        return removed;
    }

    /// <summary>모든 상태 제거</summary>
    public void RemoveAll()
    {
        if (_active.Count == 0) return;
        _active.Clear();
        OnStatesChanged?.Invoke();
    }

    /// <summary>특정 상태 보유 여부</summary>
    public bool Has(UnitStateId id) => _active.Contains(id);

    /// <summary>활성 상태 열람</summary>
    public IReadOnlyCollection<UnitStateId> GetAll() => _active;

    /// <summary>UI 등 간단 태그 문자열</summary>
    public IEnumerable<string> GetActiveTags()
    {
        foreach (var s in _active) yield return s.ToString();
    }
}
