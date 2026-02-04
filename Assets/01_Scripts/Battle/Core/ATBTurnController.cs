using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 턴의 시간 흐름과 순서를 전담하는 클래스
public class ATBTurnController : MonoBehaviour
{
    // === 설정 값 ===
    [Header("Settings")]
    [SerializeField] private bool _isPaused = false; // 시간 정지 여부

    // === 데이터 ===
    private List<BattleUnit> _allUnits = new();
    private readonly System.Random _rng = new();

    // === 이벤트 ===
    // 유닛의 턴이 온 것을 매니저에게 알려주는 신호
    public event Action<BattleUnit> OnTurnReady;
    // UI 갱신용 (매 프레임 호출될 수 있음)
    public event Action<BattleUnit> OnATBTicked;

    // === 초기화 ===
    public void RegisterUnits(IEnumerable<BattleUnit> units)
    {
        _allUnits = units.ToList();
        // AGI 변화에 따른 속도 재계산 등은 여기서 하거나 유닛별 이벤트로 처리
        RefreshATBSpeeds();
    }

    public void RefreshATBSpeeds()
    {
        if (_allUnits.Count == 0) return;

        var alive = _allUnits.Where(u => !u.IsDead).ToList();
        if (alive.Count == 0) return;

        float min = alive.Min(u => u.EffectiveAGI);
        float max = alive.Max(u => u.EffectiveAGI);

        foreach (var u in alive)
        {
            u.InitializeATB(min, max);
        }
    }

    // === 핵심 루프 ===
    void Update()
    {
        if (_isPaused) return; // 턴 진행 중(행동 중)이면 시간 멈춤
        if (_allUnits.Count == 0) return;

        // 1. 모든 유닛 ATB 충전
        float deltatime = Time.deltaTime;
        foreach (var unit in _allUnits)
        {
            if (unit == null) continue;
            if (unit.IsDead) continue;
            unit.UpdateATB(deltatime);
            OnATBTicked?.Invoke(unit); // UI 작동
        }

        // 2. 행동 가능한 유닛 찾기 (ATB >= 100)
        var candidates = _allUnits
                    .Where(u => u != null && u.IsTurnReady && !u.IsDead)
                    .ToList();

        if (candidates.Count > 0)
        {
            // 3. 우선순위 판정 (기존 로직 유지: Overfill -> AGI -> Random)
            var selected = candidates
                .OrderByDescending(u => u.Overfill)
                .ThenByDescending(u => u.EffectiveAGI)
                .ThenBy(u => _rng.NextDouble())
                .First();

            // 4. 시간 멈추고 턴 넘기기
            PauseTime();
            OnTurnReady?.Invoke(selected);
        }
    }

    // === 제어 메서드 ===
    public void PauseTime() => _isPaused = true;
    public void ResumeTime() => _isPaused = false;

    // 턴 종료 시 호출 (BattleManager가 부를 것)
    public void CompleteTurn(BattleUnit unit)
    {
        if (unit != null)
        {
            unit.ResetATB();
            unit.TickAllCooldowns(); // 쿨타임 감소도 여기서 처리하는 게 깔끔함
        }
        ResumeTime();
    }

    // 웨이브 바뀔 때 초기화
    public void ResetAllATB()
    {
        foreach (var u in _allUnits) u.ResetATB();
        _isPaused = false;
    }
}