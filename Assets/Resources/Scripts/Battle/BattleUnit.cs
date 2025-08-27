using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public enum Team { Player, Enemy }
public class BattleUnit : MonoBehaviour
{
    #region Data & Stats
    [Header("Data")]
    public UnitData data; // 유닛 데이터 참조

    [Header("Runtime Stats")]
    public Team team;
    public float AGI;
    [NonSerialized] public float ATB = 0f; // 0~100
    public float Overfill { get; private set; } = 0f; // ATB가 그 프레임에 100을 넘기면 얼마큼 넘었는지 저장(동시턴 우선순위 1순위)
    public float MaxATB { get; private set; } = 100f; // 기본 100
    public bool IsTurnReady => ATB >= 100f; // ATB가 최대가 되어 행동 가능 상태
    public float atbPerSecond; // 초당 ATB 충전 속도
    public int AttackDamage = 1;
    public int AttackRange = 2;
    public int MaxHP = 100;

    public float ATBProgress => Mathf.Clamp01(ATB / MaxATB);

    public int HP { get; private set; }
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
    //public int MoveRange = 3;
    public Tilemap CurrentMap; // 팀에 따라 Player_Tilemap or Enemy_Tilemap
    public Vector3Int Cell { get; private set; }
    #endregion

    #region Events
    public event Action<BattleUnit> OnDied; // 사망 이벤트
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyData(); // 데이터 반영(최우선)
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
            team = data.team;
            MaxHP = data.MaxHP;
            AttackRange = data.AttackRange;
            AGI = data.AGI;
        }

        HP = Mathf.Clamp(HP == 0 ? MaxHP : HP, 0, MaxHP); // 씬 배치 중 수동 값 보호
    }
    #endregion

    public void InitializeATB(float minAGI, float maxAGI)
    {
        float normalized = (AGI - minAGI) / Mathf.Max(0.01f, maxAGI - minAGI);
        float turnTime = Mathf.Lerp(12f, 6f, normalized); // 6~12초
        atbPerSecond = MaxATB / turnTime;
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

    // 턴이 끝났을 때 ATB 초기화
    public void ResetATB()
    {
        ATB = 0f;
        Overfill = 0f; // 동시턴 우선순위 잔여값도 초기화
    }
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
        Cell = toCell;
        transform.position = map.GetCellCenterWorld(toCell);
    }
    #endregion

    #region Attack
    public IEnumerator AnimateAttack(BattleUnit target) //근접공격 애니메이션
    {
        if (target != null)

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
    #endregion



    #region Hit / Death
    public void PlayHit()
    {
        if (animator) animator.SetTrigger("Hit"); // Hit 애니메이션 추가 시 사용
    }

    public IEnumerator PlayDieAndWait(float maxWait = 1.5f)
    {
        if (animator) animator.SetTrigger("Die");
        yield return new WaitForSeconds(maxWait); // 간단 대기
    }
    #endregion

    #region Damage / Heal
    public bool IsDead => HP <= 0;

    public void TakeDamage(int amount)
    {
        HP = Mathf.Max(HP - Mathf.Max(0, amount), 0);

        if (HP == 0) //죽었을 시
        {
            if (animator) animator.SetBool("Warning", false);
            OnDied?.Invoke(this);
        }
        else if (HP == 1) // 위험처리할 Hp에 도달할 시
        {
            if (animator) animator.SetBool("Warning", true);
        }

        Debug.Log($"{name} HP={HP}");
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int before = HP;
        HP = Mathf.Min(MaxHP, HP + amount);

        if(HP > 1)  //회복 후 위험상태에서 벗어났을 시
            if (animator) animator.SetBool("Warning", false);

        if (HP != before) Debug.Log($"{name} Heal +{HP - before} → {HP}/{MaxHP}");
    }
    #endregion

    #region Animation Events
    // Attack 클립의 임팩트 프레임에서 호출
    public void AnimEvent_AttackImpact() => OnAttackImpact?.Invoke();

    // Attack 클립 끝에서 호출(또는 트랜지션 Exit 이벤트)
    public void AnimEvent_AttackEnd() => OnAttackEnded?.Invoke();
    #endregion
}
