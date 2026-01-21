using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleSkillProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;

    // === 내부 상태 변수 ===
    private bool _isResolvingSelfCast = false;
    // 외부(BM)에서 읽을 수 있게 프로퍼티 추가
    public bool IsResolvingSelfCast => _isResolvingSelfCast;

    // 넉백 관련 상태
    private ParametricDamageSkill _pendingKnockbackSkill;
    private BattleUnit _pendingKnockbackTarget;
    private Vector3Int _pendingKnockbackDest;

    public void Initialize(BattleManager manager)
    {
        this.battleManager = manager;
    }

    #region Damage Calculation
    public int GetFinalSkillDamage(BattleUnit caster, BattleUnit target, SkillAsset source, float baseDamage)
    {
        // ... BattleManager에 있던 로직 그대로 복사 ...
        float finalBase = Mathf.Max(0f, baseDamage);

        if (target == null || source == null)
            return Mathf.Max(0, Mathf.FloorToInt(finalBase));

        // 전방 보너스 체크
        if (source is ParametricDamageSkill pds && pds.UseFrontlineBonus && pds.CheckFrontline(target))
        {
            finalBase *= pds.FrontlineMultiplier;
        }

        var stateDb = target.stateStatDB;
        var usc = target.GetComponent<UnitStateController>();
        var sc = target.GetComponent<StatusController>();

        float mul = 1f;
        if (stateDb != null)
            mul *= stateDb.GetDamageTakenMultiplier(usc, source.school);

        if (sc != null)
        {
            if (source.school == DamageSchool.Physical)
            {
                mul *= Mathf.Pow(1.20f, sc.GetStacks(StatusId.Exhaustion));
                mul *= Mathf.Pow(0.80f, sc.GetStacks(StatusId.Defense));
            }
            else if (source.school == DamageSchool.Magical)
            {
                mul *= Mathf.Pow(1.20f, sc.GetStacks(StatusId.Weakness));
                mul *= Mathf.Pow(0.80f, sc.GetStacks(StatusId.Resistance));
            }
        }

        // Rage 보정
        float rageMult = 1f;
        if (caster != null && caster.Rage > 0f)
        {
            rageMult += 0.01f * caster.Rage;
        }

        float raw = finalBase * rageMult * mul;
        return Mathf.Max(0, Mathf.FloorToInt(raw));
    }
    #endregion

    #region Effect Execution
    // 넉백 예약 설정
    public void SetPendingKnockback(ParametricDamageSkill skill, BattleUnit target, Vector3Int dest)
    {
        _pendingKnockbackSkill = skill;
        _pendingKnockbackTarget = target;
        _pendingKnockbackDest = dest;
    }

    public void ExecuteSkillDamage(BattleUnit caster, IEnumerable<BattleUnit> victims, SkillAsset source, Tilemap map, Vector3Int originCell)
    {
        if (caster == null || source == null) return;
        bool killRefundDone = false; // 환급 중복 방지 플래그

        foreach (var v in victims)
        {
            if (v == null) continue;
            if (v.team == caster.team) continue;

            var ctx = new SkillRuntime
            {
                map = map,
                originCell = originCell,
                casterCell = caster.Cell,
                targetCell = v.Cell
            };

            float baseDamage = source.ComputeDamage(caster, v, ctx);
            int damage = GetFinalSkillDamage(caster, v, source, baseDamage);

            // === 경계(Guard) 처리 ===
            var usc = v.GetComponent<UnitStateController>();
            if (usc != null && usc.Has(UnitStateId.Guard) && source.school == DamageSchool.Physical)
            {
                // (통찰 약화 로직 생략 - 필요시 여기에 복구하거나 별도 함수 분리)
                Debug.Log($"[Vigilance] {v.name} 방어: {damage} -> 0");
                damage = 0;
                usc.Remove(UnitStateId.Guard);
            }

            // 피해 적용
            float hpBefore = v.HP;
            v.PlayHit();
            v.TakeDamage(damage);
            bool diedNow = (hpBefore > 0f && v.IsDead);

            // [수정] 잡다한 효과 처리는 전부 여기로 위임! (중복 코드 삭제됨)
            if (source is ParametricDamageSkill dmgSkill)
            {
                ProcessParametricEffects(caster, v, dmgSkill, map, diedNow, ref killRefundDone);
            }

            // 적대감 처리
            float hostilityGained = HostilityRules.FromDamage(damage, caster, v);
            caster.AddHostility(hostilityGained);
            caster.NotifyDealtDamage(v, damage, source);
        }
    }

    // ParametricDamageSkill의 잡다한 효과들 분리 (가독성 위해)
    private void ProcessParametricEffects(BattleUnit caster, BattleUnit v, ParametricDamageSkill dmgSkill, Tilemap map, bool diedNow, ref bool killRefundDone)
    {
        if (caster == null || v == null || dmgSkill == null) return;
        int route = caster.GetTrainingRouteIndex(dmgSkill);

        // 1. 제압 추가 감소
        if (dmgSkill.trainingSuppressionOnHit > 0 &&
            dmgSkill.routeForSuppression >= 0 &&
            route == dmgSkill.routeForSuppression)
        {
            var cast = v.GetComponent<EnemyCastState>();
            if (cast != null) cast.TryReduceSuppression(dmgSkill.trainingSuppressionOnHit);
        }

        // 2. 출혈 부여
        if (dmgSkill.trainingApplyBleed &&
            dmgSkill.routeForBleed >= 0 &&
            route == dmgSkill.routeForBleed)
        {
            var sc = v.GetComponent<StatusController>();
            if (sc != null)
            {
                sc.ApplyWithTurnContext(
                    StatusId.Bleeding,
                    Mathf.Max(1, dmgSkill.trainingBleedStacks),
                    Mathf.Max(1, dmgSkill.trainingBleedDurationTurns)
                );
            }
        }

        // 3. 민첩 약화
        if (dmgSkill.trainingApplyAgiDebuff &&
            dmgSkill.routeForAgiDebuff >= 0 &&
            route == dmgSkill.routeForAgiDebuff &&
            dmgSkill.targetAgiDebuffId != UnitStateBuffId.None)
        {
            var uscTarget = v.GetComponent<UnitStateController>();
            if (uscTarget != null)
            {
                uscTarget.ApplyBuffForTurns(dmgSkill.targetAgiDebuffId, Mathf.Max(1, dmgSkill.targetAgiDebuffDurationTurns));
            }
        }

        // 4. 공포 부여
        if (dmgSkill.trainingApplyFear &&
            dmgSkill.routeForFear >= 0 &&
            route == dmgSkill.routeForFear &&
            !v.IsDead)
        {
            var uscFear = v.GetComponent<UnitStateController>();
            if (uscFear != null)
            {
                uscFear.ApplyForTurns(UnitStateId.Fear, Mathf.Max(1, dmgSkill.fearDurationTurns));
            }
        }

        // 5. 처치 시 자원 환급
        if (diedNow && !killRefundDone &&
            dmgSkill.trainingRefundOnKill &&
            dmgSkill.routeForRefundOnKill >= 0 &&
            route == dmgSkill.routeForRefundOnKill)
        {
            int cost = dmgSkill.GetEffectiveCost(caster);
            if (cost > 0)
            {
                caster.GainMP(cost);
                Debug.Log($"[Refund] {caster.name} Kill Bonus: MP +{cost}");
            }
            killRefundDone = true;
        }

        // 6. 넉백 처리 (Pending 확인)
        if (_pendingKnockbackSkill == dmgSkill &&
            _pendingKnockbackTarget == v &&
            !v.IsDead &&
            v.CurrentMap == map)
        {
            var dest = _pendingKnockbackDest;
            bool canMove = map.HasTile(dest);

            // 상태이상(이동 불가) 체크
            var sc = v.GetComponent<StatusController>();
            if (sc != null && sc.Has(StatusId.Fixing)) canMove = false;

            // 점유 체크 (BM 그리드 사용)
            if (canMove && battleManager.gridManager != null && (battleManager.gridManager.IsOccupied(Team.Player, dest) || battleManager.gridManager.IsOccupied(Team.Enemy, dest)))
            {
                canMove = false;
            }

            if (canMove)
            {
                if (battleManager.gridManager != null) battleManager.gridManager.SetOccupied(v.team, v.Cell, false);
                v.MoveTo(map, dest);
                if (battleManager.gridManager != null) battleManager.gridManager.SetOccupied(v.team, v.Cell, true);
            }

            // 사용된 Pending 초기화
            _pendingKnockbackSkill = null;
            _pendingKnockbackTarget = null;
        }
    }
    public void ResolveSkillAtCell(SkillDefinition def, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        var area = def.GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        if (battleManager.gridManager != null)
        {
            var victims = battleManager.gridManager.GetUnitsInArea(map, area);
        }
            
        // 여기서 ExecuteSkillDamage 호출하고 싶지만, 
        // SkillDefinition 구조체에는 SkillAsset 정보가 없어서 대미지 처리가 애매함.
        // 기존 코드에서도 ResolveSkillAtCell은 Enemy AI 등에서 제한적으로 쓰였음.
        // 필요하다면 구현.
    }
    #endregion

    #region Flows (Unit / Tile / Self)
    // 표준 유닛 스킬 흐름
    public IEnumerator PerformStandardUnitSkillFlow(SkillAsset _skill, BattleUnit _caster, BattleUnit _target)
    {
        bool doGapClose = _skill.ShouldGapCloseToTarget(_caster, _target);

        // BM에 있던 Co_GapCloseThenResolveOnTargetSO 로직 이동
        var originalW = _caster.transform.position;

        if (doGapClose && TryGetFrontCellOfTarget(_caster, _target, out var frontCell))
        {
            var mapForJump = _target.CurrentMap; // ?? provider...
                                                // 위치 계산 로직
            yield return _caster.AnimateJumpToWorld(GetCellRightEdgeWorld(mapForJump, frontCell), 0.08f, null, 0.2f);
        }

        bool resolved = false;
        System.Action OnImpact = () => {
            resolved = true;
            StartCoroutine(_skill.ResolveOnUnit(battleManager, _caster, _target));
        };

        _caster.OnAttackImpact += OnImpact;
        string trigger = _caster.GetAnimTriggerForSkill(_skill);
        yield return _caster.AnimateAttack(_target, trigger);
        _caster.OnAttackImpact -= OnImpact;

        if (!resolved) yield return _skill.ResolveOnUnit(battleManager, _caster, _target);

        _caster.ApplyCooldown(_skill);
        battleManager.FinishActionAfterSkill(); // BM에게 "끝났다"고 보고

        _caster.transform.position = originalW;
    }

    public IEnumerator PerformStandardTileSkillFlow(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster)
    {
        // 1. 투사체 스킬인 경우 (IProjectileTileSkill 인터페이스 구현 여부 확인)
        if (skill is IProjectileTileSkill projSkill)
        {
            bool castEnded = false;
            bool projEnded = false;
            bool fired = false;
            bool aborted = false;
            GameObject liveProjectile = null;

            int cost = skill.GetEffectiveCost(caster);

            // 1-1) 투사체 프리팹 확보
            ProjectileController projPrefab = projSkill.GetProjectilePrefab(caster);
            float projSpeed = projSkill.GetProjectileSpeed(caster);
            if (projSpeed <= 0f) projSpeed = 3f;

            // fallback: 스킬에 없으면 유닛 기본값
            if (projPrefab == null && caster != null)
                projPrefab = caster.defaultProjectilePrefab;

            // 1-2) 진짜 투사체가 없는 경우 -> 즉발로 처리할지, 취소할지 결정
            if (projPrefab == null)
            {
                Debug.LogWarning($"[Projectile] 프리팹 없음. 즉발 처리로 전환: {skill.name}");
                if (!caster.TryConsumeResource(skill.costResource, cost))
                {
                    battleManager.UnlockSkillConfirm();
                    yield break;
                }
                yield return skill.ResolveOnTile(battleManager, map, cell, caster);
                caster.ApplyCooldown(skill);
                battleManager.FinishActionAfterSkill();
                yield break;
            }

            // 1-3) 이벤트 연결 (Cast End)
            System.Action onCastEnd = null;
            onCastEnd = () => { caster.OnAttackEnded -= onCastEnd; castEnded = true; };
            caster.OnAttackEnded += onCastEnd;

            // 1-4) 이벤트 연결 (Impact -> 발사)
            System.Action onFire = null;
            onFire = () =>
            {
                caster.OnAttackImpact -= onFire;
                fired = true;

                if (!caster.TryConsumeResource(skill.costResource, cost))
                {
                    battleManager.UnlockSkillConfirm();
                    projEnded = true;
                    return;
                }
                if (aborted) { projEnded = true; return; }

                Vector3 startW = caster.transform.position;
                Vector3 targetW = map.GetCellCenterWorld(cell);

                // 투사체 생성
                liveProjectile = Instantiate(projPrefab.gameObject, startW, Quaternion.identity);
                var pc = liveProjectile.GetComponent<ProjectileController>();

                System.Action onArrive = () =>
                {
                    if (aborted) return;
                    StartCoroutine(Co_ResolveTileThenFlag(skill, map, cell, caster, () =>
                    {
                        caster.ApplyCooldown(skill);
                        projEnded = true;
                    }));
                };

                if (pc != null) pc.Init(startW, targetW, onArrive, speedUnitsPerSec: projSpeed);
                else
                {
                    // 컨트롤러 없으면 대충 시간 떼우고 도착 처리
                    StartCoroutine(FallbackProjectile(startW, targetW, 0.35f, onArrive));
                }
            };
            caster.OnAttackImpact += onFire;

            // 1-5) 애니메이션 실행
            string trigger = caster.GetAnimTriggerForSkill(skill);
            if (skill.animKind == SkillAnimKind.Ranged)
                yield return caster.AnimateRanged(trigger);
            else
                yield return caster.AnimateAttack(null, trigger);

            // 1-6) 안전장치: 애니메이션 끝날 때까지 임팩트 안 왔으면 강제 발사
            if (!fired && !projEnded)
            {
                onFire?.Invoke();
            }

            // 1-7) 대기 (최대 5초 타임아웃)
            float timeout = 5f;
            while (!(castEnded && projEnded) && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            // 1-8) 타임아웃/중단 시 정리
            if (!(castEnded && projEnded))
            {
                aborted = true;
                if (liveProjectile != null) Destroy(liveProjectile);
            }

            battleManager.FinishActionAfterSkill();
        }
        // 2. 즉발(Instant) 스킬인 경우 (또는 그 외)
        else
        {
            // === 기존 즉발 처리 로직 ===
            bool resolved = false;
            int cost = skill.GetEffectiveCost(caster);

            System.Action onImpact = () =>
            {
                if (!caster.TryConsumeResource(skill.costResource, cost))
                {
                    battleManager.UnlockSkillConfirm();
                    resolved = true;
                    return;
                }
                StartCoroutine(Co_ResolveTileThenFlag(skill, map, cell, caster, () =>
                {
                    caster.ApplyCooldown(skill);
                    resolved = true;
                }));
            };

            caster.OnAttackImpact += onImpact; // 구독

            string trigger = caster.GetAnimTriggerForSkill(skill);
            yield return caster.AnimateAttack(null, trigger);

            caster.OnAttackImpact -= onImpact; // 해제

            if (!resolved) // 이벤트 못 받았으면 강제 실행
            {
                if (caster.TryConsumeResource(skill.costResource, cost))
                {
                    yield return skill.ResolveOnTile(battleManager, map, cell, caster);
                    caster.ApplyCooldown(skill);
                }
                else
                {
                    battleManager.UnlockSkillConfirm();
                }
            }

            battleManager.FinishActionAfterSkill();
        }
    }

    public IEnumerator PerformSelfCastFlow(SkillAsset _skill, BattleUnit _caster, bool _freeAction)
    {
        if (_skill == null || _caster == null)
        {
            _isResolvingSelfCast = false;
            yield break;
        }

        // 여기서 상태 잠금 시작
        _isResolvingSelfCast = true;

        try
        {
            string trigger = _caster.GetAnimTriggerForSkill(_skill);
            yield return _caster.AnimateAttack(null, trigger);

            yield return _skill.ResolveOnUnit(battleManager, _caster, _caster);

            _caster.ApplyCooldown(_skill);

            if (!_freeAction)
            {
                battleManager.FinishActionAfterSkill();
            }
            else
            {
                battleManager.ResetSkillSelectionState();
            }
        }
        finally
        {
            _isResolvingSelfCast = false;
        }
    }

    public IEnumerator Co_ReactiveAttackFlow(SkillAsset skill, BattleUnit caster, BattleUnit target, bool doGapClose)
    {
        if (skill == null || caster == null || target == null) yield break;

        var originalW = caster.transform.position;

        if (doGapClose && TryGetFrontCellOfTarget(caster, target, out var frontCell))
        {
            var mapForJump = target.CurrentMap; // (provider 접근 대신 직접 참조 권장)
            var frontW = GetCellRightEdgeWorld(mapForJump, frontCell, 0.02f);
            yield return caster.AnimateJumpToWorld(frontW, 0.08f, null, 0.2f); // 점프 시간 상수는 파라미터나 const로
        }

        bool resolved = false;
        System.Action OnImpact = () =>
        {
            // 람다 안에서 이벤트 해제 주의 (메서드로 분리하거나 조심해서 사용)
            StartCoroutine(Co_ResolveUnitThenFlag(skill, caster, target, () => resolved = true));
        };

        caster.OnAttackImpact += OnImpact;
        string trigger = caster.GetAnimTriggerForSkill(skill);
        yield return caster.AnimateAttack(target, trigger);
        caster.OnAttackImpact -= OnImpact;

        if (!resolved)
            yield return skill.ResolveOnUnit(battleManager, caster, target);

        caster.transform.position = originalW;
    }
    #endregion

    // === 헬퍼 함수들 (BM에서 복사해오거나 BM꺼 호출) ===
    private bool TryGetFrontCellOfTarget(BattleUnit caster, BattleUnit target, out Vector3Int frontCell)
    {
        {
            frontCell = target != null ? target.Cell : default;
            if (target == null || caster == null) return false;

            var targetMap = target.CurrentMap;
            var casterMap = caster.CurrentMap ?? targetMap;

            // --- 1) 좌우 우선 규칙 ---
            var baseCell = target.Cell;
            int dx = caster.Cell.x - target.Cell.x;

            if (dx < 0)
            {
                // 캐스터가 타깃의 '왼쪽'에 있음 → 서쪽 이웃 고정
                frontCell = new Vector3Int(baseCell.x - 1, baseCell.y, baseCell.z);
                return true;
            }
            else if (dx > 0)
            {
                // 캐스터가 타깃의 '오른쪽'에 있음 → 동쪽 이웃 고정
                frontCell = new Vector3Int(baseCell.x + 1, baseCell.y, baseCell.z);
                return true;
            }

            // --- 2) 같은 컬럼(수직 정렬)일 때만 기존 각도 기반 선택 폴백 ---
            // 타겟→시전자 월드 방향
            Vector3 targetW = targetMap.GetCellCenterWorld(target.Cell);
            Vector3 casterW = casterMap.GetCellCenterWorld(caster.Cell);
            Vector2 aimDir = (Vector2)(casterW - targetW);
            if (aimDir.sqrMagnitude < 1e-6f) return false;
            aimDir.Normalize();

            bool oddCol = SkillLibrary.IsOddColumn(baseCell);

            // odd-q 이웃 집합(프로젝트에서 쓰는 체계 그대로)
            Vector3Int[] neighOffsetsEven = {
        new Vector3Int(+1, 0, 0), new Vector3Int( 0,+1,0),
        new Vector3Int(-1,+1, 0), new Vector3Int(-1, 0,0),
        new Vector3Int(-1,-1, 0), new Vector3Int( 0,-1,0)
    };
            Vector3Int[] neighOffsetsOdd = {
        new Vector3Int(+1, 0, 0), new Vector3Int(+1,+1,0),
        new Vector3Int( 0,+1, 0), new Vector3Int(-1, 0,0),
        new Vector3Int( 0,-1, 0), new Vector3Int(+1,-1,0)
    };
            var candidates = oddCol ? neighOffsetsOdd : neighOffsetsEven;

            float bestDot = float.NegativeInfinity;
            float bestDist2 = float.PositiveInfinity;
            const float EPS = 1e-5f;
            Vector3Int best = baseCell;

            foreach (var off in candidates)
            {
                var neigh = new Vector3Int(baseCell.x + off.x, baseCell.y + off.y, baseCell.z);
                var neighW = targetMap.GetCellCenterWorld(neigh);

                Vector2 dir = (Vector2)(neighW - targetW);
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();

                float d = Vector2.Dot(aimDir, dir);
                float dist2 = ((Vector2)(neighW - casterW)).sqrMagnitude;

                if (d > bestDot + EPS || (Mathf.Abs(d - bestDot) <= EPS && dist2 < bestDist2))
                {
                    bestDot = d;
                    bestDist2 = dist2;
                    best = neigh;
                }
            }

            frontCell = best;
            return true;
        }
    }
    private Vector3 GetCellRightEdgeWorld(Tilemap map, Vector3Int cell, float margin = 0.02f) 
    {
        if (map == null) return Vector3.zero;
        // BM 로직 그대로 복붙
        var center = map.GetCellCenterWorld(cell);
        var grid = map.layoutGrid != null ? map.layoutGrid : map.GetComponentInParent<Grid>();
        var cellSize = (grid != null) ? grid.cellSize : Vector3.one;
        Vector3 rightDir = (grid != null) ? grid.transform.right : Vector3.right;

        return center + rightDir * (cellSize.x * 0.5f - margin);
    }

    IEnumerator Co_ResolveUnitThenFlag(SkillAsset skill, BattleUnit caster, BattleUnit target, System.Action done)
    {
        yield return skill.ResolveOnUnit(battleManager, caster, target);
        done?.Invoke();
    }

    IEnumerator Co_ResolveTileThenFlag(SkillAsset skill, Tilemap map, Vector3Int cell, BattleUnit caster, System.Action done)
    {
        yield return skill.ResolveOnTile(battleManager, map, cell, caster);
        done?.Invoke();
    }

    IEnumerator FallbackProjectile(Vector3 start, Vector3 end, float time, System.Action done)
    {
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime / Mathf.Max(0.01f, time); yield return null; }
        done?.Invoke();
    }
}