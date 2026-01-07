using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // 기본 범위 설정 (기본값 Single)
    public ParametricDamageSkill.AreaPreset areaPreset = ParametricDamageSkill.AreaPreset.Single;

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

    [Header("Training")]
    [Header("범위 확대 훈련")]
    public bool trainingUseAreaOverride = false;
    [Range(-1, 2)] public int routeForAreaOverride = -1;
    public ParametricDamageSkill.AreaPreset trainingAreaPreset = ParametricDamageSkill.AreaPreset.Single;
    public bool trainingDiagUseNEAxis = true;

    [Header("적의 감소 훈련")]
    [Tooltip("훈련 시 적의 생성량을 감소시킬지 여부")]
    public bool trainingReduceHostility = false;
    [Tooltip("적의 감소를 활성화시키는 훈련 루트 인덱스 (-1이면 비활성)")]
    [Range(-1, 2)] public int routeForReduceHostility = -1;
    [Tooltip("적용될 적의 생성 배율 (예: 0.5 = 50%만 생성)")]
    public float trainingHostilityMultiplier = 0.5f;

    public bool SelfCastOnSelect => targetAlignment == SkillTargetAlignment.Self;

    public ProjectileController GetProjectilePrefab(BattleUnit caster)
    {
        return projectilePrefab;
    }

    public float GetProjectileSpeed(BattleUnit caster)
    {
        return projectileSpeed;
    }

    int GetRoute(BattleUnit _caster)
    {
        if (_caster == null) return -1;
        return _caster.GetTrainingRouteIndex(this);
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        if (target == null) yield break;

        // 범위 처리를 위해 ApplySupportArea 호출 (중심점: 타겟)
        // (기존에는 단일 대상이었지만, 훈련으로 범위가 커질 수 있으므로 Area로 처리)
        ApplySupportArea(bm, caster, target.CurrentMap, target.Cell);

        // 시전 후 상태 제거
        ConsumeStates(caster);

        yield break;
    }

    // 타일 선택 시 로직 (혹시 타일로 힐/버프를 줄 경우)
    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        // 범위 처리를 위해 ApplySupportArea 호출 (중심점: 타일)
        ApplySupportArea(bm, caster, map, cell);

        // 시전 후 상태 제거
        ConsumeStates(caster);

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

    // 단일 대상에게 효과 적용 (기존 로직 분리)
    void ApplyEffectToTarget(BattleManager bm, BattleUnit caster, BattleUnit target)
    {
        // 시각 효과
        if (visualEffectPrefab != null)
        {
            Instantiate(visualEffectPrefab, target.transform.position, Quaternion.identity);
        }

        var usc = target.GetComponent<UnitStateController>();
        var status = target.GetComponent<StatusController>();

        switch (mode)
        {
            case SupportSkillMode.Buff:
                if (buffState != UnitStateId.None && usc != null)
                    usc.ApplyForTurns(buffState, buffDuration);
                if (buffId != UnitStateBuffId.None && usc != null)
                    usc.ApplyBuffForTurns(buffId, buffDuration);
                if (buffStatus != StatusId.None && buffStatusStack > 0 && status != null)
                    status.SetStacks(buffStatus, buffStatusStack);
                break;

            case SupportSkillMode.Cleanse:
                if (usc != null)
                {
                    if (cleanseAllNegative)
                    {
                        usc.Remove(UnitStateId.Confusion);
                        usc.Remove(UnitStateId.Fear);
                        usc.Remove(UnitStateId.Moribundity);
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
                        foreach (var id in specificCleanseList)
                            usc.Remove(id);
                    }
                }
                break;

            case SupportSkillMode.Revive:
                if (usc != null) usc.Remove(moribundityStateId);
                int healAmount = Mathf.RoundToInt(caster.MagicDamage * healPower);
                target.Heal(healAmount);
                bm.EmitActionLabel(target, "Revived");
                break;
        }
    }

    // 범위 내 대상들에게 효과 적용 및 적의 처리
    void ApplySupportArea(BattleManager bm, BattleUnit caster, Tilemap map, Vector3Int centerCell)
    {
        var area = GetAreaCells(centerCell, SkillLibrary.IsOddColumn(centerCell));
        // 범위 내 모든 유닛 가져오기 (BattleManager는 기본적으로 모든 유닛을 반환)
        var allUnitsInArea = bm.GetUnitsInArea(map, area).ToList();

        // 아군만 필터링
        var targets = allUnitsInArea.Where(u => u != null && u.team == caster.team);

        // 모드에 따른 생사 필터링
        if (mode == SupportSkillMode.Revive)
            targets = targets.Where(u => u.IsDead); // 부활은 죽은 아군만
        else
            targets = targets.Where(u => !u.IsDead); // 버프/정화는 산 아군만

        int route = GetRoute(caster);

        foreach (var target in targets)
        {
            // 실제 효과 적용
            ApplyEffectToTarget(bm, caster, target);

            // --- 적의(Hostility) 생성 로직 ---
            float hostilityGained = 0f;

            // 부활(Revive)인 경우 회복량 기반 적의 생성
            if (mode == SupportSkillMode.Revive)
            {
                int healAmount = Mathf.RoundToInt(caster.MagicDamage * healPower);
                hostilityGained = HostilityRules.FromHeal(healAmount, caster);
            }
            // (Buff나 Cleanse는 기본적으로 적의를 생성하지 않지만, 필요하면 여기서 추가)

            // [훈련] 적의 감소 적용
            if (hostilityGained > 0f && trainingReduceHostility && routeForReduceHostility >= 0 && route == routeForReduceHostility)
            {
                hostilityGained *= trainingHostilityMultiplier;
                Debug.Log($"[Training] Support Hostility Reduced: {hostilityGained} (x{trainingHostilityMultiplier})");
            }

            if (hostilityGained > 0f)
                caster.AddHostility(hostilityGained);
        }
    }

    public override string GetFullDescriptionRich(BattleUnit _caster)
    {
        string baseDesc = base.GetFullDescriptionRich(_caster);

        int route = _caster != null ? _caster.GetTrainingRouteIndex(this) : -1;
        if (route < 0 || trainingRoutes == null || route >= trainingRoutes.Length)
            return baseDesc;

        var info = trainingRoutes[route];
        return SkillTooltipUtil.AppendTrainingRouteDescription(
            baseDesc,
            info.title,
            info.description
        );
    }
}