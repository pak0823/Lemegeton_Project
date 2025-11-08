using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public enum Team { Player, Enemy }
public class BattleUnit : MonoBehaviour
{
    #region Data & Stats
    [Header("Data")]
    public UnitData data; // 유닛 데이터 참조

    [Header("BattleManager")]
    BattleManager battleManager;

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

    public int HP { get; private set; }
    public int MP { get; private set; }
    public int Rage { get; private set; }
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

    #region Events
    public event Action<int> OnDamaged;  //피격 이벤트
    public event Action<BattleUnit> OnDied; // 사망 이벤트
    public event Action<BattleUnit> OnRetreated; //도주 이벤트
    public event System.Action<BattleUnit, BattleUnit, int, SkillAsset> OnDealtDamage;  // 유닛이 피해를 "성공적으로 입혔을 때" 알림 (패시브 트리거용)
    public event Action<BattleUnit, Tilemap, Vector3Int, Vector3Int> OnMoved;   //유닛의 이동 확인
    public static event Action<BattleUnit> OnAnyMoved;
    #endregion

    #region ----- State-based Stat System -----
    [Header("State-based Stat DB (Shared)")]
    public StateStatModifierDB stateStatDB;

    // 베이스 스탯(인스펙터/데이터로 세팅)
    [SerializeField] private int basePhysicalDamage = 1;
    [SerializeField] private int baseMagicDamage = 1;
    [SerializeField] private int baseMaxHP = 100;
    [SerializeField] private int baseMaxMP = 100;
    [SerializeField] private int baseMaxRage = 100;

    [Header("Passives (runtime)")]
    // UnitData.passives를 복사해 두고, 활성 상태만 OnAttach 호출
    private readonly List<PassiveAsset> _activePassives = new();

    // 상태 컨트롤러 캐시/보정 캐시
    UnitStateController unitStateController;
    bool _statCacheDirty = true;

    struct StatMult
    {
        public float atk, mag, def, spd;
        public int hpAdd, mpAdd;
        public float hostilityGain, hostilityDecay;

        public float hostilityGenerationMultiplier;

        public static StatMult Identity => new StatMult { atk = 1f, mag = 1f, def = 1f, spd = 1f, hpAdd = 0, mpAdd = 0, hostilityGenerationMultiplier = 1.0f};

        public void Apply(StateStatModifierDB.Entry e)
        {
            if (e == null) return;
            atk *= Mathf.Max(0f, e.atkMultiplier);
            mag *= Mathf.Max(0f, e.magMultiplier);
            def *= Mathf.Max(0f, e.defMultiplier);
            spd *= Mathf.Max(0f, e.agiMultiplier);
            hpAdd += e.hpFlatAdd;
            mpAdd += e.mpFlatAdd;
            hostilityGenerationMultiplier *= Mathf.Max(0f, e.hostilityStatMultiplier);
        }
    }
    StatMult _cachedMult = StatMult.Identity;

    void InvalidateStatCache() 
    { 
        _statCacheDirty = true;
        // 최대치가 줄어들 땐 현재값도 즉시 맞추기
        HP = Mathf.Min(HP, MaxHP);
        MP = Mathf.Min(MP, MaxMP);
        Rage = Mathf.Min(Rage, MaxRage);

        if (debugLogStats)
        {
            // Mult getter를 한 번 읽으면 즉시 재평가됨(캐시 갱신)
            var _ = Mult;

            Debug.Log(
                $"[STAT] {name} " +
                $"ATK={PhysicalDamage}  MAG={MagicDamage} " +
                $"HOSTILITY(스탯)={Hostility}  " +
                $"HP={HP}/{MaxHP}  MP={MP}/{MaxMP}"
            );
            //DEF ={ Defense}SPD ={ Speed}
            
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
                    var states = unitStateController.GetAll(); // 현재 활성 상태 목록
                    if (states != null)
                    {
                        foreach (var s in states)
                            _cachedMult.Apply(stateStatDB.Get(s));
                    }
                }
                _statCacheDirty = false;
            }
            return _cachedMult;
        }
    }

    // === 외부에서 그대로 쓰던 이름을 '프로퍼티'로 유지 (상태 보정 반영) ===
    public int PhysicalDamage => Mathf.Max(1, Mathf.RoundToInt(basePhysicalDamage * Mult.atk));
    public int MagicDamage => Mathf.Max(1, Mathf.RoundToInt(baseMagicDamage * Mult.mag));
    public int MaxHP => baseMaxHP + Mult.hpAdd;
    public int MaxMP => baseMaxMP + Mult.mpAdd;
    public int MaxRage => baseMaxRage;
    public float Hostility { get; private set; } = 1.0f; // 전투 시작 시 기본 적대감 (0으로 시작하면 첫 타겟팅이 불가능하므로 1 등으로 설정)
    public void AddHostility(float amount)
    {
        Hostility = Mathf.Max(0, Hostility + amount); // 적대감은 0 밑으로 내려가지 않도록 합니다.
    }

    public void ResetHostility()
    {
        Hostility = 1.0f; // 전투 시작 시 기본값과 동일하게 맞춰줍니다.
    }

    // 상태 효과가 적용된 최종 적대감 '생성량' 배율 (예: 도발 상태일 때 2.0f)
    public float HostilityGenerationMultiplier => Mult.hostilityGenerationMultiplier;
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
    void ApplyData()
    {
        if (data != null)
        {
            name = data.DisplayName;
            team = data.team;
            basePhysicalDamage = data.PhysicalDamage;
            baseMagicDamage = data.MagicDamage;
            baseMaxHP = data.MaxHP;
            baseMaxMP = data.MaxMP;
            baseMaxRage = data.MaxRage;
            AGI = data.AGI;
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

            return AGI * mul * stateMul;
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

        int cd = Mathf.Max(0, s.cooldownTurns);
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

    public IEnumerator AnimateMoveTo(Tilemap map, Vector3Int toCell)
    {
        Vector3 fromW = transform.position;
        Vector3 toW = map.GetCellCenterWorld(toCell);

        if (animator) animator.SetBool("IsMoving", true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, moveDuration);
            transform.position = Vector3.Lerp(fromW, toW, t);
            yield return null;
        }

        transform.position = toW; // 셀 스냅/상태 갱신
        MoveTo(map, toCell);

        if (animator) animator.SetBool("IsMoving", false);
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
    }
    #endregion

    #region Attack
    public IEnumerator AnimateAttack(BattleUnit target) //근접공격 애니메이션
    {
        if (animator) animator.SetTrigger("Attack");

        bool ended = false;
        Action onEnd = () => ended = true;
        OnAttackEnded += onEnd;

        float timeout = 2f; // 안전 타임아웃
        while (!ended && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        OnAttackEnded -= onEnd;
    }

    public IEnumerator AnimateRanged()  //원거리 공격 애니메이션
    {
        if (animator) animator.SetTrigger("Ranged");
        bool ended = false;
        Action onEnd = () => ended = true;
        OnAttackEnded += onEnd;
        float timeout = 2f;
        while (!ended && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
        OnAttackEnded -= onEnd;
    }

    //점프 애니메이션 및 기능
    public IEnumerator AnimateJumpToWorld(
    Vector3 toWorld,
    float? durationOverride = null,         // 시간을 직접 지정
    float? speedUnitsPerSec = null,         // 또는 속도로 지정(거리/속도 = 시간)
    float arcHeight = 0.15f)
    {
        if (animator) animator.SetTrigger("Jump");

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

    public IEnumerator AnimateShootWeb()    //실뿜기 애니메이션 실행 - Spider
    {
        if (animator)
        {
            if (HasParam("ShootWeb")) animator.SetTrigger("ShootWeb");
            else animator.SetTrigger("Ranged");
        }

        bool ended = false;
        Action onEnd = () => ended = true;
        OnAttackEnded += onEnd;

        float timeout = 2f;
        while (!ended && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

        OnAttackEnded -= onEnd;
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
    // (선택) 회복도 필요하면:
    //public void GainMP(int amount)
    //{
    //    if (amount <= 0) return;
    //    MP = Mathf.Clamp(MP + amount, 0, MaxMP);
    //}

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

        if (HP == 0) //죽었을 시
        {
            if (animator && Team.Player == team) animator.SetBool("Warning", false);
            OnDied?.Invoke(this);
        }
        else if (HP <= (MaxHP * 0.3f)) // 최대체력의 30% Hp보다 작거나 같을 때
        {
            if (animator) animator.SetBool("Warning", true);
        }

        Debug.Log($"Damaged: {name} damage={amount}");
    }

    public void Heal(int amount)
    {
        if (amount <= 0 && amount > -1) return;

        int before = HP;
        int Max_ThrPer = (int)(MaxHP * 0.3f);
        int Max_tenPer = (int)(MaxHP * 0.1f);

        if (amount == -1) amount = Max_tenPer;  //수동 턴 종료일 때의 회복량

        HP = Mathf.Min(MaxHP, HP + amount);

        if(HP > Max_ThrPer)  //회복 후 위험상태에서 벗어났을 시
            if (animator) animator.SetBool("Warning", false);

        //if (HP != before) Debug.Log($"{name} Heal +{HP - before} → {HP}/{MaxHP}");
    }
    #endregion

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
}
