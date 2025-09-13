// Assets/Scripts/Combat/EnemyCastState.cs
using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemyCastState : MonoBehaviour
{
    public class PendingCast
    {
        public BattleUnit owner;
        public BattleManager bm;
        public Tilemap map;
        public Vector3Int cell;
        public WebTrapController trapPrefab;
        public ProjectileController projectilePrefab;  // 스킬/유닛이 선택한 투사체
        public float projectileSpeed = 3f;        // 투사체의 속도
    }

    PendingCast _pending;
    bool _interrupted;
    int _skillPreviewToken = 0;
    bool _readyToResolve = false;

    // 캐스팅 여부 노출
    public bool IsCasting => _pending != null;

    void OnEnable()
    {
        var u = GetComponent<BattleUnit>();
        if (u != null) u.OnDamaged += OnOwnerDamaged; // BattleUnit에 이벤트 추가 필요
        BattleManager.OnAnyUnitTurnStarted += OnAnyTurnStarted; // BattleManager에 전역 이벤트 추가
    }

    void OnDisable()
    {
        var u = GetComponent<BattleUnit>();
        if (u != null) u.OnDamaged -= OnOwnerDamaged;
        BattleManager.OnAnyUnitTurnStarted -= OnAnyTurnStarted;
    }

    public bool TryTakeReady(out PendingCast p)
    {
        p = null;
        if (!_readyToResolve || _pending == null) return false;
        p = _pending;           // BM이 처리할 때까지 보존
        _readyToResolve = false;
        return true;
    }
    public void BeginCasting(PendingCast p)
    {
        _pending = p;
        _interrupted = false;
        // 프리뷰 토큰 생성 & 등록 (이 시점에 WebCast가 넘긴 map/cell 사용)
        if (_skillPreviewToken == 0 && _pending != null)
        {
            _skillPreviewToken = _pending.bm?.CreateSkillPreviewToken() ?? 0;
            if (_skillPreviewToken != 0)
                _pending.bm?.SetSkillPreviewForToken(_skillPreviewToken, _pending.map, new[] { _pending.cell });
        }
    }

    void OnOwnerDamaged(int amount)
    {
        if (_pending == null) return;        // 캐스팅 중이 아닐 때는 무시
        _interrupted = true;

        // 캐스팅 루프 즉시 종료
        _pending.owner?.SetCasting(false);

        // 프리뷰 제거
        if (_skillPreviewToken != 0)
        {
            _pending.bm?.ClearSkillPreviewToken(_skillPreviewToken);
            _skillPreviewToken = 0;
        }
        _pending.bm?.ReleaseSkillPreview();

        // 라벨 비우기
        _pending.bm?.EmitActionLabel(_pending.owner, "");

        // 다음 턴에 쓸 스킬은 미리 Plan만 해둬도 됨
        var ai = _pending.owner.GetComponent<EnemyAI>();
        if (ai != null) ai.PlanNextSkill();

        // 펜딩 정리(다음 턴 시작 시 중복 처리 방지)
        _pending = null;
        _interrupted = false;
    }

    void OnAnyTurnStarted(BattleUnit who)
    {
        if (_pending == null || who != _pending.owner) return;

        // 이 소유자의 "다음 턴 시작" 시점
        if (_interrupted)
        {
            // 실패 → 해당 토큰만 제거
            if (_skillPreviewToken != 0)
            {
                _pending.bm?.ClearSkillPreviewToken(_skillPreviewToken);
                _skillPreviewToken = 0;
            }
            _pending.owner?.SetCasting(false);       // 캐스팅 루프 종료
            _pending.bm?.ReleaseSkillPreview();
            _pending = null;
            _interrupted = false;
            return;
        }

        _readyToResolve = true;// 여기서는 '해결 준비'만 표시(생성/턴소비는 BM에서)
    }

    public void ClearPreviewAndFinalize(BattleManager bm)
    {
        if (_skillPreviewToken != 0) { bm?.ClearSkillPreviewToken(_skillPreviewToken); _skillPreviewToken = 0; }
        bm?.ReleaseSkillPreview();
        _pending = null;
        _interrupted = false;
    }
}
