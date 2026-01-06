using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum SupportSkillMode
{
    Buff,       // 버프 부여 (가속 물약)
    Cleanse,    // 정화 (정화 물약)
    Revive      // 소생 (소생 물약)
}

[CreateAssetMenu(menuName = "Battle/Skills/Common/Parametric Support", fileName = "ParametricSupportSkill")]
public class ParametricSupportSkill : SkillAsset, ISelfCastSkill, IProjectileTileSkill
{
    [Header("Support Mode")]
    public SupportSkillMode mode = SupportSkillMode.Buff;

    [Header("Buff Settings")]
    [Tooltip("부여할 상태 (UnitStateId)")]
    public UnitStateId buffState = UnitStateId.None;
    [Tooltip("부여할 버프 (UnitStateBuffId)")]
    public UnitStateBuffId buffId = UnitStateBuffId.None;
    public int buffDuration = 3;

    [Tooltip("부여할 상태 중첩")]
    public StatusId buffStatus = StatusId.None;
    [Tooltip("부여할 스택 수")]
    public int buffStatusStack = 0;

    [Header("Cleanse Settings")]
    [Tooltip("모든 해로운 상태를 제거할지 여부")]
    public bool cleanseAllNegative = true;
    [Tooltip("특정 상태만 제거하려면 여기에 추가")]
    public List<UnitStateId> specificCleanseList = new List<UnitStateId>();

    [Header("Revive / Heal Settings")]
    [Tooltip("회복 배율 (CLV 비례)")]
    public float healPower = 1.0f;
    [Tooltip("빈사 상태(Moribundity) ID")]
    public UnitStateId moribundityStateId = UnitStateId.Moribundity;

    [Header("Effect")]
    public GameObject visualEffectPrefab; // 파티클 등

    // 투사체 설정
    [Header("Projectile Settings(투사체 설정)")]
    public ProjectileController projectilePrefab;
    public float projectileSpeed = 4f;

    // 시전 후 상태 제거 옵션
    [Header("State Consumption(상태 제거)")]
    public bool consumeStateOnCast = false;
    public List<UnitStateId> statesToConsume = new List<UnitStateId>();

    public bool SelfCastOnSelect => targetAlignment == SkillTargetAlignment.Self;

    public ProjectileController GetProjectilePrefab(BattleUnit caster)
    {
        return projectilePrefab;
    }

    public float GetProjectileSpeed(BattleUnit caster)
    {
        return projectileSpeed;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (target == null) yield break;

        // 시각 효과
        if (visualEffectPrefab != null)
        {
            Instantiate(visualEffectPrefab, target.transform.position, Quaternion.identity);
        }

        var usc = target.GetComponent<UnitStateController>();
        var status = target.GetComponent<StatusController>(); // 혹시 스택형 상태 제거가 필요할 경우

        switch (mode)
        {
            case SupportSkillMode.Buff:
                // 상태 부여
                if (buffState != UnitStateId.None && usc != null)
                {
                    usc.ApplyForTurns(buffState, buffDuration);
                }
                // 버프(스탯) 부여
                if (buffId != UnitStateBuffId.None && usc != null)
                {
                    usc.ApplyBuffForTurns(buffId, buffDuration);
                }
                if (buffStatus != StatusId.None && buffStatusStack > 0)
                {
                    var statusCtrl = target.GetComponent<StatusController>();
                    if (statusCtrl != null)
                    {
                        statusCtrl.SetStacks(buffStatus, buffStatusStack);
                    }
                }
                break;

            case SupportSkillMode.Cleanse:
                if (usc != null)
                {
                    if (cleanseAllNegative)
                    {
                        // 임시: 특정 해로운 상태들(공포, 혼란 등)을 하드코딩으로 지우거나,
                        // UnitStateController에 "해로운 상태 목록" 정의가 필요함.
                        // 일단 예시로 몇 개 지움:
                        usc.Remove(UnitStateId.Confusion);
                        usc.Remove(UnitStateId.Fear);
                        usc.Remove(UnitStateId.Moribundity); // 빈사도 부정적 상태라면

                        // StatusController의 디버프(출혈, 중독, 발화) 제거
                        if (status != null)
                        {
                            status.Clear(StatusId.Bleeding);
                            status.Clear(StatusId.Poisoning);
                            status.Clear(StatusId.Ignition);
                            status.Clear(StatusId.Slow);
                            status.Clear(StatusId.Weakness);
                            status.Clear(StatusId.Exhaustion);
                        }
                    }
                    else
                    {
                        // 지정된 상태만 제거
                        foreach (var id in specificCleanseList)
                        {
                            usc.Remove(id);
                        }
                    }
                }
                break;

            case SupportSkillMode.Revive:
                // 빈사 상태 제거
                if (usc != null)
                {
                    usc.Remove(moribundityStateId);
                }

                // 체력 회복
                int healAmount = Mathf.RoundToInt(caster.MagicDamage * healPower);
                target.Heal(healAmount);

                // 로그 출력
                bm.EmitActionLabel(target, "Revived");
                break;
        }

        // 시전 후 상태 제거
        ConsumeStates(caster);

        yield break;
    }

    // 타일 선택 시 로직 (혹시 타일로 힐/버프를 줄 경우)
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        // 범위 스킬이라면 해당 타일 위 유닛을 찾아 ResolveOnUnit 호출
        BattleUnit target = bm.GetUnitAt(cell); // BattleManager에 이런 헬퍼가 있다고 가정
        if (target != null && target.team == caster.team)
        {
            yield return ResolveOnUnit(bm, caster, target);
        }
        else
        {
            // (타일 대상이지만 유닛 없을 때도 상태 제거는 해야 함)
            ConsumeStates(caster);
        }
        yield break;
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int _originCell, bool _isOddRow)
    {
        yield break;
    }

    void ConsumeStates(BattleUnit caster)
    {
        if (!consumeStateOnCast || statesToConsume == null) return;
        var usc = caster.GetComponent<UnitStateController>();
        if (usc == null) return;

        foreach (var s in statesToConsume)
        {
            if (s != UnitStateId.None)
                usc.Remove(s);
        }
    }
}