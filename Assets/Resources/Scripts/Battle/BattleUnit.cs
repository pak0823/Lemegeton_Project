using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum Team { Player, Enemy }

public class BattleUnit : MonoBehaviour
{
    [Header("Data")]
    public UnitData data;             // 유닛 데이터 참조

    [Header("Runtime Stats")]
    public Team team;
    public int AGI;
    public int AttackDamage = 1;
    public int AttackRange = 2;
    public int MaxHP = 100;
    public int HP { get; private set; }

    [Header("Visual")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] float moveDuration = 0.18f; // 1칸 이동 연출 시간

    // 공격 타이밍/종료 콜백(애니메이션 이벤트용)
    public Action OnAttackImpact;   // 타격 타이밍(데미지 적용)
    public Action OnAttackEnded;    // 공격 모션 종료 시

    //public int MoveRange = 3;

    public Tilemap CurrentMap;   // 팀에 따라 Player_Tilemap or Enemy_Tilemap
    public Vector3Int Cell { get; private set; }

    public event Action<BattleUnit> OnDied;   // 사망 이벤트

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyData();  //데이터 반영(최우선)
    }

    void Start()
    {
        if (CurrentMap == null && Shared.battleMapManager != null)
        {
            var map = (team == Team.Player) ? Shared.battleMapManager.PlayerFloor
                                            : Shared.battleMapManager.EnemyFloor;
            var cell = map.WorldToCell(transform.position);
            MoveTo(map, cell);
        }
    }

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

    // 방향 보정(추후에 필요시에 사용하고 아님 제거)
    //public void FaceTo(Vector3 worldTarget)
    //{
    //    if (!spriteRenderer) return;
    //    spriteRenderer.flipX = (worldTarget.x > transform.position.x);
    //}

    public IEnumerator AnimateMoveTo(Tilemap map, Vector3Int toCell)
    {
        Vector3 fromW = transform.position;
        Vector3 toW = map.GetCellCenterWorld(toCell);

        // 방향
        //FaceTo(toW);

        // 모션 시작
        //if (animator) animator.SetBool("IsMoving", true); //아직 애니메이션이 없음. 이동 관련 애니 없이 하면 그냥 제거

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, moveDuration);
            transform.position = Vector3.Lerp(fromW, toW, t);
            yield return null;
        }
        transform.position = toW;

        // 셀 스냅/상태 갱신
        MoveTo(map, toCell);

        //if (animator) animator.SetBool("IsMoving", false);
    }
    // 공격 애니메이션 시작 (데미지는 애니메이션 이벤트에서)
    public IEnumerator AnimateAttack(BattleUnit target)
    {
        if (target != null) /*FaceTo(target.transform.position)*/;
        if (animator) animator.SetTrigger("Attack");

        bool ended = false;
        Action onEnd = () => ended = true;
        OnAttackEnded += onEnd;

        // 안전 타임아웃(클립 세팅이 잘못돼도 빠져나오도록)
        float timeout = 2f;
        while (!ended && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
        OnAttackEnded -= onEnd;
    }

    // 피격/사망 연출
    public void PlayHit()
    {
        //if (animator) animator.SetTrigger("Hit"); //Hit 애니메이션이 추가되면 그 때 사용
    }
    public IEnumerator PlayDieAndWait(float maxWait = 1.5f)
    {
        if (animator) animator.SetTrigger("Die");
        yield return new WaitForSeconds(maxWait); // 간단 대기(필요시 애니메이션 이벤트로 대체)
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

    public bool IsDead => HP <= 0;
    public void TakeDamage(int amount)
    {
        HP = Mathf.Max(HP - Mathf.Max(0, amount), 0);
        if (HP == 0)
        {
            if (animator) animator.SetBool("Warning", false);
            OnDied?.Invoke(this);
        }
        else if(HP <= 1)
        {
            if (animator) animator.SetBool("Warning",true);
            Debug.Log("warning!");
        }
        Debug.Log($"{name} HP={HP}");
    }

    // 수동 턴 종료 회복 (최대체력 초과 금지)
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int before = HP;
        HP = Mathf.Min(MaxHP, HP + amount);
        if (HP != before)
            Debug.Log($"{name} Heal +{HP - before}  → {HP}/{MaxHP}");
    }

    // === Animation Events ===
    // Attack 클립의 임팩트 프레임에서 호출
    public void AnimEvent_AttackImpact() => OnAttackImpact?.Invoke();
    // Attack 클립 끝에서 호출(또는 트랜지션 Exit 이벤트)
    public void AnimEvent_AttackEnd() => OnAttackEnded?.Invoke();
}
