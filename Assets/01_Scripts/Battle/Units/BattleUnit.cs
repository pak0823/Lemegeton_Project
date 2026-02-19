using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(UnitStats), typeof(UnitMover), typeof(UnitVisual))]
public class BattleUnit : MonoBehaviour
{
    #region 1. Core Data & Configuration (데이터 및 설정)
    [Header("Data Source")]
    public UnitData data;                   // 유닛 데이터 원본 (ScriptableObject)
    public StateStatModifierDB stateStatDB; // 상태 이상 스탯 보정 DB

    [Header("Prefab Settings")]
    public ProjectileController defaultProjectilePrefab; // 기본 투사체 프리팹
    
    // Components
    public UnitStats Stats { get; private set; }
    public UnitMover Mover { get; private set; }
    public UnitVisual Visual { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debugLogStats = false; // 스탯 디버깅용 (빌드 시 제외 가능)

    #endregion



    #region 2. Dependencies (외부 의존성)

    [Header("Managers & Controllers")]

    // BattleManager: 외부에서는 프로퍼티로 접근

    [SerializeField] private BattleManager battleManager;

    public BattleManager Battle => battleManager;



    // 내부 캐싱용 컨트롤러

    private UnitStateController unitStateController;

    private StatusController statusController;

    #endregion



    #region 3. Runtime Status (실시간 전투 상태)

    //Vital Stats

    public float HP => Stats.HP;
    public float MP => Stats.MP;
    public float Rage => Stats.Rage;

    public bool IsRetreated { get; private set; } = false; // 도주 여부



    [Header("Map Position")]

    public Tilemap CurrentMap;      // 현재 위치한 타일맵

    public Vector3Int Cell { get; private set; } // 그리드 좌표



    // 속성 저항 테이블 (런타임 변동 가능성 고려하여 구조체 배열 유지)

    [System.Serializable]

    public struct AttrMod { public AttackAttr attr; public float mult; }

    public AttrMod[] resistTable;

    #endregion



    #region 4. ATB System (턴 관리 시스템)

    [Header("ATB Settings")]

    [NonSerialized] public float ATB = 0f;

    public float MaxATB { get; private set; } = 100f;

    public float Overfill { get; private set; } = 0f; // 턴 초과분 (우선권 결정용)



    private float speedMultiplier = 5.0f;  // 속도 계수



    // ATB 계산 프로퍼티

    public bool IsTurnReady => ATB >= MaxATB;

    public float ATBProgress => Mathf.Clamp01(ATB / MaxATB);

    public float atbPerSecond

    {

        get

        {

            // 현재 AGI 가져오기 (0 방지)

            float currentAgi = Mathf.Max(0.1f, EffectiveAGI);



            // 최종 속도 계산 (선형 비례)

            float finalSpeed = currentAgi * speedMultiplier;



            // 너무 빨라서 게임이 고장나는 것만 방지 (최소 0.1, 최대 5000 등 넉넉하게)

            return Mathf.Clamp(finalSpeed, 1f, 10000f);

        }

    }



    // 내부 연산용 변수

    private float _agiMinRef, _agiMaxRef;

    #endregion



    #region 5. Visuals & Animation (비주얼)

    [Header("Visual Components")]

    [SerializeField] private Animator animator;

    [SerializeField] private SpriteRenderer spriteRenderer;



    [Header("Animation Settings")]

    [SerializeField] private float moveDuration = 0.18f; // 이동 연출 시간

    [SerializeField] private float defaultAnimEndTimeout = 8f; // 애니메이션 강제 종료 타임아웃

    private const float MinTimeout = 0.25f;



    // 애니메이션 이벤트 콜백 (Action)

    public Action OnAttackImpact;  // 타격 시점 (데미지 적용)

    public Action OnAttackEnded;   // 모션 종료 시점

    #endregion



    #region 6. Internal Logic & Cache (내부 로직용)

    // 패시브 관리

    private readonly List<PassiveAsset> _activePassives = new();

    private int _passiveBDYBonus = 0;



    // 스탯 캐싱 플래그

    private bool _statCacheDirty = true;

    #endregion



    #region 7. Events (외부 알림용)

    // 상태 변화 이벤트

    public event Action<int> OnDamaged;       // 피격 시

    public event Action<BattleUnit> OnDied;   // 사망 시

    public event Action<BattleUnit> OnRetreated; // 도주 시



    // 행동 및 이동 이벤트

    public event Action<BattleUnit, Tilemap, Vector3Int, Vector3Int> OnMoved; // 이동 완료 시

    public static event Action<BattleUnit> OnAnyMoved; // (Static) 누군가 이동했을 때



    // 전투 로직 이벤트

    public event Action<BattleUnit, BattleUnit, int, SkillAsset> OnDealtDamage; // 피해를 입혔을 때 (트리거용)

    public event Action<SkillAsset> OnSkillUsed; // 스킬 사용 시



    // 이벤트 호출 헬퍼

    public void NotifySkillUsed(SkillAsset skill) => OnSkillUsed?.Invoke(skill);

    #endregion



    #region 8. State-based Stat System (스탯 및 상태 계산 시스템)



    // ========================================================================

    // [1] Calculation Structs (내부 연산용 구조체)

    // ========================================================================



    // 각종 배율(Multiplier)과 가산치(Add)를 모아둔 구조체

    private struct StatMult

    {

        public float str, clv, mnd, agi, ins;

        public int hpAdd, mpAdd;

        public float hostilityGain, hostilityDecay;

        public float hostilityGenerationMultiplier;



        // 초기값 (배율은 1.0, 가산치는 0)

        public static StatMult Identity => new StatMult

        {

            str = 1f,

            clv = 1f,

            agi = 1f,

            ins = 1f,

            mnd = 1f,

            hpAdd = 0,

            mpAdd = 0,

            hostilityGenerationMultiplier = 1.0f

        };



        // 상태 이상(State) 적용

        public void Apply(StateStatModifierDB.Entry e)

        {

            if (e == null) return;

            str *= Mathf.Max(0f, e.atkMultiplier);

            clv *= Mathf.Max(0f, e.magMultiplier);

            agi *= Mathf.Max(0f, e.agiMultiplier);

            ins *= Mathf.Max(0f, e.insMultiplier);

            hpAdd += e.hpFlatAdd;

            mpAdd += e.mpFlatAdd;

            hostilityGenerationMultiplier *= Mathf.Max(0f, e.hostilityStatMultiplier);

        }



        // 버프(Buff) 적용

        public void ApplyBuff(StateStatModifierDB.BuffEntry e)

        {

            if (e == null) return;

            str *= e.atkMultiplier;

            clv *= e.magMultiplier;

            agi *= e.agiMultiplier;

            ins *= e.insMultiplier;

            hpAdd += e.hpFlatAdd;

            mpAdd += e.mpFlatAdd;

            hostilityGenerationMultiplier *= e.hostilityStatMultiplier;

        }

    }



    // 스탯 변화 추적용 스냅샷 (디버깅용)

    private struct StatSnapshot

    {

        public float MaxHP, MaxMP;

        public float PhysicalDamage, MagicDamage;

        public float EffectiveAGI, EffectiveINS;

        public float CritChance;



        public static StatSnapshot From(BattleUnit u)

        {

            return new StatSnapshot

            {

                MaxHP = u.MaxHP,

                MaxMP = u.MaxMP,

                PhysicalDamage = u.STR,

                MagicDamage = u.CLV,

                EffectiveAGI = u.EffectiveAGI,

                EffectiveINS = u.INS,

                CritChance = u.CritChance,

            };

        }

    }



    // ========================================================================

    // [2] Caching & Core Logic (캐싱 및 핵심 계산)

    // ========================================================================



    private StatMult _cachedMult = StatMult.Identity;

    private StatSnapshot _lastSnapshot;

    private bool _hasSnapshot = false;



    // 배율 계산 프로퍼티 (캐싱 적용)

    private StatMult Mult

    {

        get

        {

            if (_statCacheDirty)

            {

                _cachedMult = StatMult.Identity;

                if (unitStateController != null && stateStatDB != null)

                {

                    // 1. 상태(State) 배수 적용

                    foreach (var s in unitStateController.GetAll())

                    {

                        var entry = stateStatDB.Get(s);

                        if (entry != null) _cachedMult.Apply(entry);

                    }



                    // 2. 버프(Buff) 배수 적용

                    foreach (var b in unitStateController.GetAllBuffs())

                    {

                        var buffEntry = stateStatDB.GetBuff(b);

                        if (buffEntry != null) _cachedMult.ApplyBuff(buffEntry);

                    }

                }

                _statCacheDirty = false;

            }

            return _cachedMult;

        }

    }



    // 스탯 캐시 초기화 (상태 변화 시 호출)

    public void InvalidateStatCache()

    {

        _statCacheDirty = true;



        // 최대치가 줄어들 땐 현재값도 즉시 맞춰줌 (Clamping)

        Stats.SetHP(Mathf.Min(HP, MaxHP));
        Stats.SetMP(Mathf.Min(MP, MaxMP));
        Stats.SetRage(Mathf.Min(Rage, MaxRage));



        // 디버그 모드일 때만 로그 출력 (성능 부하 방지)

        if (debugLogStats)

        {

            // 새 값 계산 강제 (Mult 재계산 유도)

            var _ = Mult;

            var newSnap = StatSnapshot.From(this);



            if (!_hasSnapshot)

            {

                _lastSnapshot = newSnap;

                _hasSnapshot = true;

                Debug.Log($"[STAT] {name} 초기 스냅샷: HP={HP}/{newSnap.MaxHP}, ATK={newSnap.PhysicalDamage}, AGI={newSnap.EffectiveAGI:F2}");

            }

            else

            {

                CompareAndLogSnapshot(_lastSnapshot, newSnap);

                _lastSnapshot = newSnap;

            }

        }

    }



    private void CompareAndLogSnapshot(StatSnapshot oldSnap, StatSnapshot newSnap)

    {

        if (newSnap.MaxHP != oldSnap.MaxHP) Debug.Log($"[STATΔ] {name} MaxHP: {oldSnap.MaxHP} -> {newSnap.MaxHP}");

        if (newSnap.PhysicalDamage != oldSnap.PhysicalDamage) Debug.Log($"[STATΔ] {name} STR: {oldSnap.PhysicalDamage} -> {newSnap.PhysicalDamage}");

        if (Mathf.Abs(newSnap.EffectiveAGI - oldSnap.EffectiveAGI) > 0.001f) Debug.Log($"[STATΔ] {name} AGI: {oldSnap.EffectiveAGI:F2} -> {newSnap.EffectiveAGI:F2}");

        // 필요한 항목 추가 가능

    }



    // ========================================================================

    // [3] Public Stat Properties (최종 스탯 반환)

    // ========================================================================



    // 기본 6대 스탯 (Data * Mult)

    public float STR => Mathf.Max(0f, data.baseSTR * Mult.str);

    public float CLV => Mathf.Max(0f, data.baseCLV * Mult.clv);

    public float MND => Mathf.Max(0, data.baseMND * Mult.mnd); // (* Mult.mnd로 수정: 구조체 로직과 일치)

    public float INS => Mathf.Max(0, data.baseINS * Mult.ins);



    // 신체(BDY)는 패시브 보너스 가산 방식

    public float BDY => Mathf.Max(0, data.baseBDY + _passiveBDYBonus);

    public float EffectiveAGI

    {

        get

        {

            if (data == null) return 0f;



            // 1. 기존 StatusController 배율 (Awake에서 캐싱된 statusController 사용)

            float scMul = (statusController != null) ? statusController.GetAgilityMultiplier() : 1f;



            // 2. 신규 DB 시스템 배율 (Mult.agi에 이미 캐싱되어 있음)

            float dbMul = Mult.agi;



            // 3. 패시브 및 최종 연산

            return Mathf.Max(0f, data.baseAGI * scMul * dbMul * _passiveAgilityMultiplier);

        }

    }



    // 민첩(AGI)은 패시브 승수 추가 적용

    private float _passiveAgilityMultiplier = 1f;



    // 파생 스탯 (MaxHP, MaxMP, MaxRage)

    public float MaxHP

    {

        get

        {

            float fromBody = BDY * 3f;

            float fromStr = STR;

            float buffAdd = Mult.hpAdd;

            return Mathf.Max(1, Mathf.FloorToInt(fromBody + fromStr + buffAdd));

        }

    }



    public float MaxMP

    {

        get

        {

            // (주의: Mult.mpAdd 사용)

            float raw = (MND * 3f) + CLV + Mult.mpAdd;

            return Mathf.Max(0, Mathf.FloorToInt(raw));

        }

    }



    public float MaxRage

    {

        get

        {

            // 6대 스탯 총합

            return Mathf.Max(0f, STR + CLV + EffectiveAGI + BDY + MND + INS);

        }

    }



    public float CritChance => data.baseINS * Mult.ins * 0.01f;  // 예: INS 30 → 30%



    // ========================================================================

    // [4] Helper Methods (Rage, Hostility 조작)

    // ========================================================================



    // --- Rage ---

    public void AddRage(float amount)

    {

        if (Mathf.Approximately(amount, 0f)) return;

        float before = Rage;

        Stats.SetRage(Mathf.Clamp(before + amount, 0f, MaxRage));

        if (debugLogStats) Debug.Log($"[RAGE] {name}: {before:F1} -> {Rage:F1} ({amount:+#;-#;0})");

    }



    public void ReduceRageByRatio(float ratio)

    {

        if (ratio <= 0f) return;

        float amount = Rage * Mathf.Clamp01(ratio);

        Stats.SetRage(Mathf.Clamp(Rage - amount, 0f, MaxRage));

    }



    // --- Hostility (적대감) ---

    public float Hostility { get; private set; } = 1.0f;

    public float HostilityGenerationMultiplier => Mult.hostilityGenerationMultiplier;



    public void AddHostility(float amount)

    {

        if (amount > 0f) amount *= HostilityGenerationMultiplier; // 적대감 생성 배율 적용

        Hostility = Mathf.Max(0f, Hostility + amount);

    }



    public void ResetHostility() => Hostility = 1.0f;



    // --- Utils ---

    public void SetPassiveAgilityMultiplier(float multiplier)

    {

        _passiveAgilityMultiplier = Mathf.Max(0f, multiplier);

        InvalidateStatCache(); // AGI 변경 시 캐시 갱신



        // 만약 ATB 시스템이 이 값을 캐싱하고 있다면 알림 필요 (예: ATBTurnController.RefreshUnits())

    }



    #endregion



    #region Unity Callbacks

    void Awake()

    {

        if (!animator) animator = GetComponent<Animator>();

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();



        unitStateController = GetComponent<UnitStateController>();

        // 상태 변경 시 캐시 무효화(이벤트가 있다면 구독)

        /* Event subscriptions moved to OnEnable/OnDisable
        if (unitStateController != null)
        {
            unitStateController.OnStatesChanged += InvalidateStatCache;
            unitStateController.OnBuffsChanged += InvalidateStatCache;
            unitStateController.OnStatesChanged += RecomputeATBFromRefs;
            unitStateController.OnBuffsChanged += RecomputeATBFromRefs;
        }
        */



        Stats = GetComponent<UnitStats>();
        if (Stats == null) Stats = gameObject.AddComponent<UnitStats>();

        Mover = GetComponent<UnitMover>();
        if (Mover == null) Mover = gameObject.AddComponent<UnitMover>();

        Visual = GetComponent<UnitVisual>();
        if (Visual == null) Visual = gameObject.AddComponent<UnitVisual>();

        Visual.Initialize();
        if (Stats != null) Stats.Initialize(data);

        ApplyData(); // 데이터 반영(HP/MP 초기화 포함)

    }

    void OnEnable()
    {
        if (battleManager == null) battleManager = BattleManager.Instance; // [Optimization] Use Instance
        // Fallback if Instance is null (though unlikely in battle)
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();
        
        // Register and Subscribe
        if (battleManager != null)
        {
            battleManager.RegisterUnit(this);
            battleManager.OnWaveStarted += HandleWaveStarted;
        }

        // Subscribe to UnitStateController
        if (unitStateController != null)
        {
            unitStateController.OnStatesChanged += InvalidateStatCache;
            unitStateController.OnBuffsChanged += InvalidateStatCache;
            unitStateController.OnStatesChanged += RecomputeATBFromRefs;
            unitStateController.OnBuffsChanged += RecomputeATBFromRefs;
        }
    }

    void OnDisable()
    {
        // Unregister from BattleManager
        if (battleManager != null)
        {
            battleManager.UnregisterUnit(this);
            battleManager.OnWaveStarted -= HandleWaveStarted;
        }
        else if (BattleManager.Instance != null)
        {
            BattleManager.Instance.UnregisterUnit(this);
            BattleManager.Instance.OnWaveStarted -= HandleWaveStarted;
        }

        // Unsubscribe from UnitStateController
        if (unitStateController != null)
        {
            unitStateController.OnStatesChanged -= InvalidateStatCache;
            unitStateController.OnBuffsChanged -= InvalidateStatCache;
            unitStateController.OnStatesChanged -= RecomputeATBFromRefs;
            unitStateController.OnBuffsChanged -= RecomputeATBFromRefs;
        }
    }

    void OnDestroy()
    {
        // Cleanup handled in OnDisable
    }



    void Start()

    {

        if (CurrentMap == null && BattleMapManager.Instance != null)

        {

            var map = (data.team == Team.Player) ? BattleMapManager.Instance.PlayerFloor : BattleMapManager.Instance.EnemyFloor;

            var cell = map.WorldToCell(transform.position);

            MoveTo(map, cell);

        }

    }

    #endregion



    #region Data Initialization

    public void ApplyData()

    {

        gameObject.name = data.DisplayName;

        Hostility = data.baseHostility; // 런타임에 변하는 값만 초기값 대입



        // 상태 반영된 최대치가 필요하므로 먼저 캐시 무효화

        InvalidateStatCache();



        // 현재값 초기화/보정

        Stats.SetHP(Mathf.Clamp(HP == 0 ? MaxHP : HP, 0, MaxHP));
        Stats.SetMP(Mathf.Clamp(MP == 0 ? MaxMP : MP, 0, MaxMP));
        Stats.SetRage(Mathf.Clamp(Rage, 0, MaxRage));

    }

    #endregion



    public void InitPassives(BattleManager _battlemanager)

    {

        _activePassives.Clear();



        if (data == null || data.passives == null) return;



        foreach (var passives in data.passives)

        {

            if (passives == null) continue;

            if (!passives.unlockedByDefault) continue; // 추후 해금 조건 체크 지점



            _activePassives.Add(passives);

            passives.OnAttach(this, _battlemanager);

        }

    }



    public void InitializeATB(float minAGI, float maxAGI)

    {

        _agiMinRef = minAGI;

        _agiMaxRef = maxAGI;

    }



    // 버프/상태 변경 시 불러줄 헬퍼

    void RecomputeATBFromRefs()

    {

        if (_agiMaxRef <= _agiMinRef + 0.001f) return;

        // 같은 기준으로 다시 계산(EffectiveAGI가 달라졌으니 atbPerSecond가 갱신됨)

        InitializeATB(_agiMinRef, _agiMaxRef);

    }



    public void UpdateATB(float deltaTime)

    {

        if (IsDead || IsTurnReady) return; // 사망 또는 이미 준비 완료



        float gain = atbPerSecond * deltaTime;

        float raw = ATB + gain;           // 클램프 전 원시값



        // 이번 프레임에 100%를 넘겼다면, 넘긴 만큼을 Overfill에 보관

        if (raw >= 100f)

            Overfill = raw - 100f;

        else

            Overfill = 0f;



        ATB = Mathf.Min(100f, raw);

    }



    public void AddBodyBonusFromPassive(int delta)

    {

        _passiveBDYBonus += delta;

        // BDY가 바뀌면 MaxHP도 다시 계산되도록 캐시 갱신

        InvalidateStatCache();

    }



    // 턴이 끝났을 때 ATB 초기화

    public void ResetATB()

    {

        ATB = 0f;

        Overfill = 0f; // 동시턴 우선순위 잔여값도 초기화

    }



    // 외부에서 패시브 on/off 할 때 사용 (예: 해금 조건 달성 시 켜기)

    public void SetPassiveEnabled(PassiveAsset _passives, bool enabled, BattleManager _battlemanager)

    {

        if (_passives == null) return;



        bool has = _activePassives.Contains(_passives);



        if (enabled && !has)

        {

            _activePassives.Add(_passives);

            _passives.OnAttach(this, _battlemanager);

        }

        else if (!enabled && has)

        {

            _activePassives.Remove(_passives);

            _passives.OnDetach(this, _battlemanager);

        }

    }



    #region Skill Cooldowns

    // === Skill Cooldowns (per unit) ===

    private readonly Dictionary<SkillAsset, int> _cooldowns = new();



    public bool IsSkillOnCooldown(SkillAsset s)

    {

        var key = GetCooldownKey(s);

        return key != null && _cooldowns.TryGetValue(key, out var left) && left > 0;

    }



    public int GetCooldownRemaining(SkillAsset s)

    {

        var key = GetCooldownKey(s);

        return key != null && _cooldowns.TryGetValue(key, out var left)

            ? Mathf.Max(0, left)

            : 0;

    }



    public void ApplyCooldown(SkillAsset s)

    {

        var key = GetCooldownKey(s);

        if (key == null) return;



        // 훈련 등을 반영한 실제 쿨다운 턴수

        int cd = Mathf.Max(0, s.GetEffectiveCooldownTurns(this));

        if (cd <= 0)

        {

            _cooldowns.Remove(key);

            return;

        }



        _cooldowns[key] = cd;

    }



    // 자신의 턴이 끝날 때 1씩 감소

    public void TickAllCooldowns()

    {

        var keys = new List<SkillAsset>(_cooldowns.Keys);

        foreach (var k in keys)

        {

            _cooldowns[k] = Mathf.Max(0, _cooldowns[k] - 1);

            if (_cooldowns[k] == 0) _cooldowns.Remove(k);

        }

    }



    private SkillAsset GetCooldownKey(SkillAsset s)

    {

        if (s == null) return null;



        // 이미 등록된 것 중 같은 legacyId를 가진 애가 있으면 그걸 공용 키로 사용

        foreach (var key in _cooldowns.Keys)

        {

            if (key != null && key.legacyId == s.legacyId)

                return key;

        }



        // 2) 아직 없으면 이번 스킬 자신을 키로 사용

        return s;

    }

    #endregion



    #region Movement

    /// <summary>

    /// 주어진 스킬에 대해, 이 유닛이 사용할 애니메이션 트리거 이름을 결정합니다.

    /// 우선순위: UnitData.skillAnimBindings → SkillAsset.animTriggerOverride → animKind 기본값

    /// </summary>

    public string GetAnimTriggerForSkill(SkillAsset skill)

    {

        if (skill == null)

            return "Skill_1";



        // 1) UnitData에 유닛별 매핑이 있으면 우선 사용

        if (data != null && data.skillAnimBindings != null)

        {

            foreach (var b in data.skillAnimBindings)

            {

                if (b.skillId == skill.legacyId && !string.IsNullOrEmpty(b.triggerName))

                    return b.triggerName;

            }

        }



        // 2) 스킬 자체에서 오버라이드 지정된 경우

        if (!string.IsNullOrEmpty(skill.animTriggerOverride))

            return skill.animTriggerOverride;



        // 3) animKind 에 따른 기본값

        switch (skill.animKind)

        {

            case SkillAnimKind.SelfCast:

                // 자기 강화용 캐스팅 트리거 (Animator에 따라 이름 다를 수 있음)

                return "Casting";

            case SkillAnimKind.None:

            case SkillAnimKind.Special:

            case SkillAnimKind.Melee:

            case SkillAnimKind.Ranged:

                return "Skill_1";

            default:

                return "Skill_1";

        }

    }



    public IEnumerator AnimateMoveTo(Tilemap map, Vector3Int toCell)

    {

        // Delegate to Mover
        yield return Mover.MoveToAsync(toCell, moveDuration).ToCoroutine();



        Vector3 toW = map.GetCellCenterWorld(toCell); // Re-define toW for the line below
        transform.position = toW; // 셀 스냅/상태 갱신
        MoveTo(map, toCell);

        if (animator) animator.SetBool("Move", false);
    }

    public void PlayTrigger(string triggerName)

    {

        if (string.IsNullOrEmpty(triggerName)) return;
        Visual.PlayTriggerAsync(triggerName).Forget();

    }



    public void Bind(Tilemap map, Vector3Int startCell)

    {

        CurrentMap = map;

        Cell = startCell;

        transform.position = map.GetCellCenterWorld(startCell);

    }



    public void MoveTo(Tilemap map, Vector3Int toCell)

    {

        Tilemap fromMap = CurrentMap;

        Vector3Int fromCell = Cell;



        CurrentMap = map;

        Cell = toCell;

        transform.position = map.GetCellCenterWorld(toCell);



        // 인스턴스 이벤트

        OnMoved?.Invoke(this, fromMap, fromCell, toCell);

        // 전역 이벤트 (패시브들이 듣는 용도)

        OnAnyMoved?.Invoke(this);

    }



    void HandleWaveStarted()

    {

        ResetHostility();   //적의 초기화

#if UNITY_EDITOR

        AddRage(9999f);

#endif

    }

    #endregion



    #region Attack

    public IEnumerator AnimateAttack(BattleUnit target, string triggerOverride)

     => AnimateAttack(target, triggerOverride, null);



    public IEnumerator AnimateAttack(BattleUnit target, string triggerOverride, float? timeoutOverride)

    {

        string trigger = string.IsNullOrEmpty(triggerOverride) ? "Skill_1" : triggerOverride;

        yield return PlayTriggerAndWaitEnd(trigger, timeoutOverride, "Skill_1(Override)");

    }



    public IEnumerator AnimateRanged(string triggerOverride)

     => AnimateRanged(triggerOverride, null);



    public IEnumerator AnimateRanged(string triggerOverride, float? timeoutOverride)

    {

        string trigger = string.IsNullOrEmpty(triggerOverride) ? "Ranged" : triggerOverride;

        yield return PlayTriggerAndWaitEnd(trigger, timeoutOverride, "Ranged(Override)");

    }



    public IEnumerator AnimateShootWeb()

    {

        // 기존 로직 유지: ShootWeb 있으면 ShootWeb, 없으면 Ranged

        string trigger = HasParam("ShootWeb") ? "ShootWeb" : "Ranged";

        yield return PlayTriggerAndWaitEnd(trigger, null, "ShootWeb");

    }



    //점프 애니메이션 및 기능

    public IEnumerator AnimateJumpToWorld(

    Vector3 toWorld,

    float? durationOverride = null,         // 시간을 직접 지정

    float? speedUnitsPerSec = null,         // 또는 속도로 지정(거리/속도 = 시간)

    float arcHeight = 0.15f)

    {

        Vector3 from = transform.position;

        float distance = Vector3.Distance(from, toWorld);

        float duration = durationOverride ?? (speedUnitsPerSec.HasValue

            ? distance / Mathf.Max(0.01f, speedUnitsPerSec.Value)

            : 0.18f); // 기본값



        float t = 0f;

        while (t < 1f)

        {

            t += Time.deltaTime / Mathf.Max(0.01f, duration);

            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = Vector3.Lerp(from, toWorld, t) + new Vector3(0f, arc, 0f);

            yield return null;

        }

    }



    public void SetCasting(bool on) //캐스팅 애니메이션 실행

    {

        if (animator) animator.SetBool("Casting", on);

    }



    bool HasParam(string name)

    {

        if (!animator) return false;

        foreach (var p in animator.parameters) if (p.name == name) return true;

        return false;

    }

    #endregion



    public bool HasMP(int cost) => cost <= 0 || MP >= cost;

    public bool TryConsumeMP(int cost)

    {

        if (cost <= 0) return true;

        if (MP < cost) return false;

        Stats.SetMP(Mathf.Max(0, MP - cost));

        return true;

    }

    public void GainMP(int amount)

    {

        int MPInt = Mathf.FloorToInt(amount);

        if (MPInt <= 0)

            return;



        float before = MP;

        int maxThrPer = Mathf.FloorToInt(MaxMP * 0.3f);



        Stats.SetMP(Mathf.Min(MaxMP, MP + MPInt));

    }



    public bool HasRage(int amount) => amount <= 0 || Rage >= amount;

    public bool TryConsumeRage(int amount)

    {

        if (amount <= 0) return true;

        if (Rage < amount) return false;

        Stats.SetRage(Mathf.Max(0f, Rage - amount));

        return true;

    }

    public bool HasResource(SkillCostResource res, int amount)

    {

        return res switch

        {

            SkillCostResource.MP => HasMP(amount),

            SkillCostResource.Rage => HasRage(amount),

            _ => true

        };

    }

    public bool TryConsumeResource(SkillCostResource res, int amount)

    {

        return res switch

        {

            SkillCostResource.MP => TryConsumeMP(amount),

            SkillCostResource.Rage => TryConsumeRage(amount),

            _ => true

        };

    }



    public void Retreat()

    {

        if (IsRetreated || IsDead) return;

        IsRetreated = true;

        OnRetreated?.Invoke(this);

    }



    #region Hit / Death

    public void PlayHit()

    {

        if (animator) animator.SetTrigger("Hit"); // Hit 애니메이션 추가 시 사용

    }



    public IEnumerator PlayDieAndWait(float maxWait = 1.5f)

    {

        if (animator)

        {

            if (data.team == Team.Player)

                animator.SetTrigger("Die");

        }

        yield return new WaitForSeconds(maxWait); // 간단 대기

    }

    #endregion



    #region Damage / Heal

    public bool IsDead => HP <= 0;



    public void TakeDamage(int amount)

    {

        int dmg = Mathf.Max(0, amount);

        Stats.SetHP(Mathf.Max(HP - dmg, 0));

        OnDamaged?.Invoke(dmg);



        // FloatingText (Damage)

        if (dmg > 0)

        {

            var pos = transform.position + Vector3.up * 0.15f;

            // TMP RichText로 빨간색 출력

            FloatingTextManager.Instance?.Spawn(pos, $"<color=#FF0000>{dmg}</color>");

        }



        if (HP == 0) //죽었을 시

        {

            if (animator && Team.Player == data.team) animator.SetBool("hurt", false);

            OnDied?.Invoke(this);

        }

        else if (HP <= (MaxHP * 0.3f)) // 최대체력의 30% Hp보다 작거나 같을 때

        {

            if (animator) animator.SetBool("hurt", true);

        }



        Debug.Log($"Damaged: {name} damage={amount}");

    }

    public void HealPercent(float ratio)

    {

        ratio = Mathf.Clamp01(ratio);

        int amount = Mathf.FloorToInt(MaxHP * ratio);

        Heal(amount);

    }



    public void Heal(float amount)

    {

        int healInt = Mathf.FloorToInt(amount);

        if (healInt <= 0)

            return;



        float before = HP;

        int maxThrPer = Mathf.FloorToInt(MaxHP * 0.3f);



        Stats.SetHP(Mathf.Min((int)MaxHP, HP + healInt));



        // 회복 후 위험 상태에서 벗어났으면 Warning 끔

        if (HP > maxThrPer && animator)

            animator.SetBool("hurt", false);



        // 필요하면 디버그 로그

        //Debug.Log($"{name} Heal +{HP - before} → {HP}/{MaxHP}");

    }

    #endregion



    public int GetTrainingRouteIndex(SkillAsset skill)

    {

        if (Battle == null || Battle.Training == null || data == null || skill == null)

            return -1;



        var db = Battle.Training;



        // 1) legacyId 그룹 기준 조회

        if (skill.legacyId != SkillId.None)

        {

            int r = db.GetRouteByLegacy(data, skill.legacyId);

            if (r >= 0) return r;

        }



        // 2) fallback: 개별 스킬 기준 조회

        return db.GetRoute(data, skill);

    }

    //패시브 설명 호출

    public void AnnouncePassive(string passiveName)

    {

        Battle?.EmitPassiveLabelAutoClear(this, passiveName, 1.0f);

    }



    #region Animation Events

    // Attack 클립의 임팩트 프레임에서 호출

    public void AnimEvent_AttackImpact() => OnAttackImpact?.Invoke();



    // Attack 클립 끝에서 호출(또는 트랜지션 Exit 이벤트)

    public void AnimEvent_AttackEnd() => OnAttackEnded?.Invoke();

    #endregion



    /// 이 유닛이 피해를 성공적으로 가했을 때 호출해, 패시브 등 리스너에게 알린다.

    /// 반드시 BattleUnit 외부에선 이 메서드만 호출하고, 이벤트를 직접 Invoke하지 말 것.

    public void NotifyDealtDamage(BattleUnit victim, int damage, SkillAsset source)

    {

        OnDealtDamage?.Invoke(this, victim, damage, source);

    }



    private IEnumerator PlayTriggerAndWaitEnd(string trigger, float? timeoutOverride, string debugTag)

    {

        if (!animator)

            yield break;



        if (string.IsNullOrEmpty(trigger))

            trigger = "Skill_1";



        bool ended = false;

        Action onEnd = () => ended = true;

        OnAttackEnded += onEnd;



        animator.ResetTrigger(trigger);  // 선택: 트리거 꼬임 방지(프로젝트 전반 영향 낮음)

        animator.SetTrigger(trigger);



        float timeout = Mathf.Max(MinTimeout, timeoutOverride ?? defaultAnimEndTimeout);



        yield return null;



        while (!ended && timeout > 0f)

        {

            timeout -= Time.deltaTime;



            // 이벤트가 누락되었더라도, 현재 재생 중인 애니메이션이 끝났다면 종료 처리

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);



            // 태그(Tag)를 쓰거나, 단순히 "Attack"이나 "Casting" 등 핵심 동작 중인지 체크

            // 여기서는 진행도가 1.0(100%)을 넘었고, Loop가 아닌 경우 강제 종료

            if (stateInfo.normalizedTime >= 1.0f && !stateInfo.loop)

            {

                // 트랜지션 중이 아닐 때만 체크 (트랜지션 중에는 이전/다음 상태가 섞임)

                if (!animator.IsInTransition(0))

                {

                    // Debug.Log($"[SafetyBreak] {name} 애니메이션 종료 감지되어 강제 넘김.");

                    ended = true;

                }

            }

            yield return null;

        }



        OnAttackEnded -= onEnd;



        if (!ended)

        {

            // watchdog 발동: "End 이벤트 누락/전이 문제"를 실제로 잡아내기 위한 경고

            var state = animator.GetCurrentAnimatorStateInfo(0);

            Debug.LogWarning(

                $"[AnimTimeout] Unit='{name}', Trigger='{trigger}', Tag='{debugTag}', " +

                $"StateHash={state.shortNameHash}, NormalizedTime={state.normalizedTime:F2}. " +

                $"Check AnimationEvent 'AnimEvent_AttackEnd' on the clip and transitions.");

        }

    }

}

