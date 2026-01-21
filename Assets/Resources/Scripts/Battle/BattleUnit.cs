using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


public class BattleUnit : MonoBehaviour
{
    #region Data & Stats
    [Header("Data")]
    public UnitData data; // 유닛 데이터 참조

    [Header("BattleManager")]
    BattleManager battleManager;
    public BattleManager Battle => battleManager;

    [Header("FX / Projectile")]
    public ProjectileController defaultProjectilePrefab;  // 유닛 기본 투사체

    public bool IsRetreated { get; private set; } = false;  //도주 확인용
    

    [Header("Runtime Stats")]
    public Team team;
    public ISBOSS isBoss;
    public float AGI;
    [NonSerialized] public float ATB = 0f; // 0~100
    float _agiMinRef, _agiMaxRef;
    public float Overfill { get; private set; } = 0f; // ATB가 그 프레임에 100을 넘기면 얼마큼 넘었는지 저장(동시턴 우선순위 1순위)
    public float MaxATB { get; private set; } = 100f; // 기본 100
    public bool IsTurnReady => ATB >= 100f; // ATB가 최대가 되어 행동 가능 상태
    public float atbPerSecond; // 초당 ATB 충전 속도

    [System.Serializable]
    public struct AttrMod { public AttackAttr attr; public float mult; } // 예: (Strike, 1.2f)
    public AttrMod[] resistTable;

    public float ATBProgress => Mathf.Clamp01(ATB / MaxATB);

    [SerializeField] private bool debugLogStats = false; //스탯 확인 임시용 - 사용 후 제거하기

    public float HP { get; private set; }
    public float MP { get; private set; }
    public float Rage { get; private set; }
    #endregion

    #region Visual
    [Header("Visual")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] float moveDuration = 0.18f; // 1칸 이동 연출 시간
    #endregion

    #region Animation Callbacks
    // 공격 타이밍/종료 콜백(애니메이션 이벤트용)
    public Action OnAttackImpact; // 타격 타이밍(데미지 적용)
    public Action OnAttackEnded; // 공격 모션 종료 시
    #endregion

    #region Map & Position
    public Tilemap CurrentMap; // 팀에 따라 Player_Tilemap or Enemy_Tilemap
    public Vector3Int Cell { get; private set; }
    #endregion

    [SerializeField] private float defaultAnimEndTimeout = 8f; // 기존 2f 대신, "비상용"으로 충분히 크게
    private const float MinTimeout = 0.25f;

    #region Events
    public event Action<int> OnDamaged;  //피격 이벤트
    public event Action<BattleUnit> OnDied; // 사망 이벤트
    public event Action<BattleUnit> OnRetreated; //도주 이벤트
    public event System.Action<BattleUnit, BattleUnit, int, SkillAsset> OnDealtDamage;  // 유닛이 피해를 "성공적으로 입혔을 때" 알림 (패시브 트리거용)
    public event Action<BattleUnit, Tilemap, Vector3Int, Vector3Int> OnMoved;   //유닛의 이동 확인
    public static event Action<BattleUnit> OnAnyMoved;

    // 스킬 사용 알림 이벤트
    public event Action<SkillAsset> OnSkillUsed;
    public void NotifySkillUsed(SkillAsset skill) => OnSkillUsed?.Invoke(skill);
    #endregion

    #region ----- State-based Stat System -----
    [Header("State-based Stat DB (Shared)")]
    public StateStatModifierDB stateStatDB;

    // 베이스 스탯(인스펙터/데이터로 세팅)
    [SerializeField] private float basePhysicalDamage = 1;
    [SerializeField] private float baseMagicDamage = 1;
    [SerializeField] private float baseBDY = 1;
    [SerializeField] private float baseMND = 1;
    [SerializeField] private float baseINS = 1;

    [Header("Passives (runtime)")]
    // UnitData.passives를 복사해 두고, 활성 상태만 OnAttach 호출
    private readonly List<PassiveAsset> _activePassives = new();
    int _passiveBDYBonus = 0;

    // 상태 컨트롤러 캐시/보정 캐시
    UnitStateController unitStateController;
    bool _statCacheDirty = true;

    struct StatMult
    {
        public float atk, mag, def, agi, ins;
        public int hpAdd, mpAdd;
        public float hostilityGain, hostilityDecay;
        public float hostilityGenerationMultiplier;

        public static StatMult Identity => new StatMult { atk = 1f, mag = 1f, agi = 1f, ins = 1f, hpAdd = 0, mpAdd = 0, hostilityGenerationMultiplier = 1.0f};

        public void Apply(StateStatModifierDB.Entry e)
        {
            if (e == null) return;
            atk *= Mathf.Max(0f, e.atkMultiplier);
            mag *= Mathf.Max(0f, e.magMultiplier);
            agi *= Mathf.Max(0f, e.agiMultiplier);
            ins *= Mathf.Max(0f, e.insMultiplier);
            hpAdd += e.hpFlatAdd;
            mpAdd += e.mpFlatAdd;
            hostilityGenerationMultiplier *= Mathf.Max(0f, e.hostilityStatMultiplier);
        }

        public void ApplyBuff(StateStatModifierDB.BuffEntry e)
        {
            if (e == null) return;

            atk *= e.atkMultiplier;
            mag *= e.magMultiplier;
            agi *= e.agiMultiplier;
            ins *= e.insMultiplier;

            hpAdd += e.hpFlatAdd;
            mpAdd += e.mpFlatAdd;

            hostilityGenerationMultiplier *= e.hostilityStatMultiplier;
        }
    }
    StatMult _cachedMult = StatMult.Identity;

    struct StatSnapshot
    {
        public float MaxHP, MaxMP;
        public float PhysicalDamage, MagicDamage;
        public float EffectiveAGI;
        public float EffectiveINS;
        public float CritChance;

        public static StatSnapshot From(BattleUnit u)
        {
            return new StatSnapshot
            {
                MaxHP = u.MaxHP,
                MaxMP = u.MaxMP,
                PhysicalDamage = u.PhysicalDamage,
                MagicDamage = u.MagicDamage,
                EffectiveAGI = u.EffectiveAGI,
                EffectiveINS = u.INS,
                CritChance = u.CritChance,
            };
        }
    }

    StatSnapshot _lastSnapshot;
    bool _hasSnapshot = false;

    void InvalidateStatCache() 
    { 
        _statCacheDirty = true;

        // 최대치가 줄어들 땐 현재값도 즉시 맞추기
        HP = Mathf.Min(HP, MaxHP);
        MP = Mathf.Min(MP, MaxMP);
        Rage = Mathf.Min(Rage, MaxRage);

        if (debugLogStats)
        {
            // 새 값 계산 강제 (Mult, MaxHP, EffectiveAGI 등)
            var _ = Mult;
            var newSnap = StatSnapshot.From(this);

            if (!_hasSnapshot)
            {
                _lastSnapshot = newSnap;
                _hasSnapshot = true;

                Debug.Log(
                    $"[STAT] {name} 초기 스냅샷: " +
                    $"HP={HP}/{newSnap.MaxHP}, MP={MP}/{newSnap.MaxMP}, " +
                    $"ATK={newSnap.PhysicalDamage}, MAG={newSnap.MagicDamage}, " +
                    $"AGI={newSnap.EffectiveAGI:F2}, INS={newSnap.EffectiveINS}, " +
                    $"Crit={newSnap.CritChance:P1}"
                );
            }
            else
            {
                // 바뀐 항목만 로그 출력
                if (newSnap.MaxHP != _lastSnapshot.MaxHP)
                    Debug.Log($"[STATΔ] {name} MaxHP: {_lastSnapshot.MaxHP} -> {newSnap.MaxHP}");
                if (newSnap.MaxMP != _lastSnapshot.MaxMP)
                    Debug.Log($"[STATΔ] {name} MaxMP: {_lastSnapshot.MaxMP} -> {newSnap.MaxMP}");
                if (newSnap.PhysicalDamage != _lastSnapshot.PhysicalDamage)
                    Debug.Log($"[STATΔ] {name} ATK: {_lastSnapshot.PhysicalDamage} -> {newSnap.PhysicalDamage}");
                if (newSnap.MagicDamage != _lastSnapshot.MagicDamage)
                    Debug.Log($"[STATΔ] {name} MAG: {_lastSnapshot.MagicDamage} -> {newSnap.MagicDamage}");
                if (Mathf.Abs(newSnap.EffectiveAGI - _lastSnapshot.EffectiveAGI) > 0.0001f)
                    Debug.Log($"[STATΔ] {name} AGI: {_lastSnapshot.EffectiveAGI:F2} -> {newSnap.EffectiveAGI:F2}");
                if (newSnap.EffectiveINS != _lastSnapshot.EffectiveINS)
                    Debug.Log($"[STATΔ] {name} INS: {_lastSnapshot.EffectiveINS} -> {newSnap.EffectiveINS}");
                if (Mathf.Abs(newSnap.CritChance - _lastSnapshot.CritChance) > 0.0001f)
                    Debug.Log($"[STATΔ] {name} Crit: {_lastSnapshot.CritChance:P1} -> {newSnap.CritChance:P1}");

                _lastSnapshot = newSnap;
            }
        }
    }

    StatMult Mult
    {
        get
        {
            if (_statCacheDirty)
            {
                _cachedMult = StatMult.Identity;
                if (unitStateController != null && stateStatDB != null)
                {
                    // 상태(State) 배수 적용
                    foreach (var s in unitStateController.GetAll())
                    {
                        var entry = stateStatDB.Get(s);
                        if (entry != null)
                            _cachedMult.Apply(entry);
                    }

                    // 버프(Buff) 배수 적용
                    foreach (var b in unitStateController.GetAllBuffs())
                    {
                        var buffEntry = stateStatDB.GetBuff(b);
                        if (buffEntry != null)
                            _cachedMult.ApplyBuff(buffEntry);
                    }
                }
                _statCacheDirty = false;
            }
            return _cachedMult;
        }
    }

    // === 외부에서 그대로 쓰던 이름을 '프로퍼티'로 유지 (상태 보정 반영) ===
    public float MaxHP
    {
        get
        {
            float fromBody = BDY * 3f;
            float fromStr = PhysicalDamage;
            float buffAdd = Mult.hpAdd;

            // 체력 공식 = (BDY * 3) + PhysicalDamage + 상태/버프에서 온 hpFlatAdd
            float raw = fromBody + fromStr + buffAdd;
            return Mathf.Max(1, Mathf.FloorToInt(raw));
        }
    }

    public float MaxMP
    {
        get
        {
            float raw = (baseMND * 3f) + MagicDamage + Mult.mpAdd;
            return Mathf.Max(0, Mathf.FloorToInt(raw));
        }
    }
    public float MaxRage
    {
        get
        {
            // 현재 전투 상황이 반영된 6 스탯
            float str = PhysicalDamage;   // 근력
            float mag = MagicDamage;      // 마력
            float agi = EffectiveAGI;     // 민첩
            float bdy = BDY;              // 신체
            float mnd = baseMND;          // 정신
            float ins = INS;              // 통찰

            return Mathf.Max(0f, str + mag + agi + bdy + mnd + ins);
        }
    }
    public float PhysicalDamage => Mathf.Max(0f, basePhysicalDamage * Mult.atk);
    public float MagicDamage => Mathf.Max(0f, baseMagicDamage * Mult.mag);
    public float BDY => Mathf.Max(0, baseBDY + _passiveBDYBonus);
    public float INS => Mathf.Max(0, (baseINS * Mult.ins));
    public float Hostility { get; private set; } = 1.0f; // 전투 시작 시 기본 적대감 (0으로 시작하면 첫 타겟팅이 불가능하므로 1 등으로 설정)

    // === Rage 조작 헬퍼 ===
    public void AddRage(float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;

        float before = Rage;
        Rage = Mathf.Clamp(before + amount, 0f, MaxRage);

        Debug.Log($"[RAGE] {name} Rage: {before:F2} -> {Rage:F2} (Δ={amount:F2})");
    }

    public void ReduceRageByRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (ratio <= 0f) return;

        float before = Rage;
        float delta = before * ratio;
        if (delta <= 0f) return;

        Rage = Mathf.Clamp(before - delta, 0f, MaxRage);
        Debug.Log($"[RAGE] {name} Rage Calm: {before:F2} -> {Rage:F2} (-{delta:F2}, ratio={ratio * 100f:F1}%)");
    }

    // === Hostility 조작 헬퍼 ===
    public void AddHostility(float amount)
    {
        float before = Hostility;

        float applied = amount;

        // 적의가 증가할 때만 각종 배율 적용
        if (applied > 0f)
        {
            applied *= HostilityGenerationMultiplier;
        }

        // 음수로 내려가면 0 밑으로는 안 떨어지게 클램프
        Hostility = Mathf.Max(0f, Hostility + applied);

        float after = Hostility;
        Debug.Log($"[HOSTILITY] {name} Hostility: {before:F2} -> {after:F2} (Δ={applied:F2})");
    }

    public void ResetHostility()
    {
        Hostility = 1.0f; // 전투 시작 시 기본값과 동일하게 맞춰줍니다.
    }

    // 상태 효과가 적용된 최종 적대감 '생성량' 배율 (예: 도발 상태일 때 2.0f)
    public float HostilityGenerationMultiplier => Mult.hostilityGenerationMultiplier;

    public void SetPassiveAgilityMultiplier(float multiplier)
    {
        _passiveAgilityMultiplier = Mathf.Max(0f, multiplier);
        // AGI가 변하면 ATB도 영향을 받으므로 재계산
        RecomputeATBFromRefs();
    }
    private float _passiveAgilityMultiplier = 1f;

    public float CritChance => baseINS * Mult.ins * 0.01f;  // 예: INS 30 → 30% 크리티컬
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        unitStateController = GetComponent<UnitStateController>();
        // 상태 변경 시 캐시 무효화(이벤트가 있다면 구독)
        if (unitStateController != null)
        {
            unitStateController.OnStatesChanged += InvalidateStatCache;
            unitStateController.OnBuffsChanged += InvalidateStatCache; // 캐시 무효화

            // ATB 재계산도 연결
            unitStateController.OnStatesChanged += RecomputeATBFromRefs;
            unitStateController.OnBuffsChanged += RecomputeATBFromRefs;
        }

        ApplyData(); // 데이터 반영(HP/MP 초기화 포함)
    }
    void OnEnable()
    {
        if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();
        if (battleManager != null) battleManager.OnWaveStarted += HandleWaveStarted;
    }

    void OnDestroy()
    {
        if (unitStateController != null)
        {
            unitStateController.OnStatesChanged -= InvalidateStatCache;
            unitStateController.OnBuffsChanged -= InvalidateStatCache;

            unitStateController.OnStatesChanged -= RecomputeATBFromRefs;
            unitStateController.OnBuffsChanged -= RecomputeATBFromRefs;
        }
        if (Shared.BattleManager != null)
        {
            Shared.BattleManager.OnWaveStarted -= HandleWaveStarted;
        }
    }

    void Start()
    {
        if (CurrentMap == null && Shared.battleMapManager != null)
        {
            var map = (team == Team.Player) ? Shared.battleMapManager.PlayerFloor : Shared.battleMapManager.EnemyFloor;
            var cell = map.WorldToCell(transform.position);
            MoveTo(map, cell);
        }
    }
    #endregion

    #region Data Initialization
    public void ApplyData()
    {
        if (data != null)
        {
            name = data.DisplayName;
            team = data.team;
            basePhysicalDamage = data.PhysicalDamage;
            baseMagicDamage = data.MagicDamage;
            AGI = data.AGI;
            baseBDY = data.BDY;
            baseMND = data.MND;
            baseINS = data.INS;
            isBoss = data.isBoss;
            Hostility = data.Hostility;
        }

        // 상태 반영된 최대치가 필요하므로 먼저 캐시 무효화
        InvalidateStatCache();

        // 현재값 초기화/보정
        HP = Mathf.Clamp(HP == 0 ? MaxHP : HP, 0, MaxHP);
        MP = Mathf.Clamp(MP == 0 ? MaxMP : MP, 0, MaxMP);
        Rage = Mathf.Clamp(Rage, 0, MaxRage);
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
        _agiMinRef = minAGI;                 // 기준 저장
        _agiMaxRef = maxAGI;

        float normalized = (EffectiveAGI - minAGI) / Mathf.Max(0.01f, maxAGI - minAGI);
        float turnTime = Mathf.Lerp(12f, 6f, normalized); // 6~12초
        atbPerSecond = MaxATB / turnTime;
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

    public float EffectiveAGI
    {
        get
        {
            var sc = GetComponent<StatusController>();
            float mul = (sc != null) ? sc.GetAgilityMultiplier() : 1f;

            // 상태/버프 DB 배수(특히 연막 AgiUp)는 여기서 곱해진다
            float stateMul = 1f;
            if (stateStatDB != null && unitStateController != null)
                stateMul = stateStatDB.ComputeMultipliers(unitStateController).agi;

            return AGI * mul * stateMul * _passiveAgilityMultiplier;
        }
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
        Vector3 fromW = transform.position;
        Vector3 toW = map.GetCellCenterWorld(toCell);

        if (animator) animator.SetBool("Move", true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, moveDuration);
            transform.position = Vector3.Lerp(fromW, toW, t);
            yield return null;
        }

        transform.position = toW; // 셀 스냅/상태 갱신
        MoveTo(map, toCell);

        if (animator) animator.SetBool("Move", false);
    }
    public void PlayTrigger(string triggerName)
    {
        if (!animator || string.IsNullOrEmpty(triggerName)) return;
        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
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
        MP = Mathf.Max(0, MP - cost);
        return true;
    }
    public void GainMP(int amount)
    {
        int MPInt = Mathf.FloorToInt(amount);
        if (MPInt <= 0)
            return;

        float before = MP;
        int maxThrPer = Mathf.FloorToInt(MaxMP * 0.3f);

        MP = Mathf.Min(MaxMP, MP + MPInt);
    }

    public bool HasRage(int amount) => amount <= 0 || Rage >= amount;
    public bool TryConsumeRage(int amount)
    {
        if (amount <= 0) return true;
        if (Rage < amount) return false;
        Rage = Mathf.Max(0f, Rage - amount);
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
            if (team == Team.Player)
                animator.SetTrigger("Die");
        }
        yield return new WaitForSeconds(maxWait); // 간단 대기
    }
    #endregion

    #region Damage / Heal
    public bool IsDead => HP <= 0;

    public void TakeDamage(int amount)
    {
        HP = Mathf.Max(HP - Mathf.Max(0, amount), 0);
        OnDamaged?.Invoke(amount);

        int dmg = Mathf.Max(0, amount);
        HP = Mathf.Max(HP - dmg, 0);
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
            if (animator && Team.Player == team) animator.SetBool("hurt", false);
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

        HP = Mathf.Min((int)MaxHP, HP + healInt);

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
