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

        // 캐스팅 스킬/제압 카운트
        public EnemySkill skillSO;   // 캐스팅 중인 적 스킬 SO
        public int suppressMax;      // 필요 제압 수(0~3)
        public int suppressCur;      // 현재 남은 제압 수
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

        // 제압 카운트 초기화(스킬별 설정)
        int need = Mathf.Clamp(_pending?.skillSO ? _pending.skillSO.suppressionRequired : 0, 0, 3);
        _pending.suppressMax = need;
        _pending.suppressCur = need;

        // 프리뷰 토큰 생성 & 등록 (이 시점에 WebCast가 넘긴 map/cell 사용)
        if (_skillPreviewToken == 0 && _pending != null)
        {
            _skillPreviewToken = _pending.bm?.CreateSkillPreviewToken() ?? 0;
            if (_skillPreviewToken != 0)
                _pending.bm?.SetSkillPreviewForToken(_skillPreviewToken, _pending.map, new[] { _pending.cell });
        }

        // 라벨 컬러 반영
        UpdateCastLabelColor();
    }

    void OnOwnerDamaged(int amount)
    {
        if (_pending == null) return;

        //if (_pending.suppressMax > 0)
        //{
        //    _pending.suppressCur = Mathf.Max(0, _pending.suppressCur - 1);
        //    if (_pending.suppressCur > 0)
        //    {
        //        // 아직 캐스팅 유지 → 색만 갱신
        //        UpdateCastLabelColor();
        //        return;
        //    }
        //    // 0이 되었을 때만 중단 처리로 진행
        //}

        //_interrupted = true;
        //_pending.owner?.SetCasting(false); // 캐스팅 루프 즉시 종료

        //// 프리뷰 제거
        //if (_skillPreviewToken != 0)
        //{
        //    _pending.bm?.ClearSkillPreviewToken(_skillPreviewToken);
        //    _skillPreviewToken = 0;
        //}
        //_pending.bm?.ReleaseSkillPreview();

        //// 라벨 비우기
        //_pending.bm?.EmitActionLabel(_pending.owner, "");

        //// 다음 턴에 쓸 스킬은 미리 Plan만 해둬도 됨
        //var ai = _pending.owner.GetComponent<EnemyAI>();
        //if (ai != null) ai.PlanNextSkill();

        //// 펜딩 정리(다음 턴 시작 시 중복 처리 방지)
        //_pending = null;
        //_interrupted = false;
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
    public bool TryReduceSuppression(int amount)
    {
        if (_pending == null || amount <= 0) return false;

        // 제압 카운트가 없는 스킬이면(=need 0) 의미 없음
        if (_pending.suppressMax <= 0) return false;

        _pending.suppressCur = Mathf.Max(0, _pending.suppressCur - amount);
        UpdateCastLabelColor();

        if (_pending.suppressCur > 0) return true;

        _interrupted = true;
        _pending.owner?.SetCasting(false);

        if (_skillPreviewToken != 0)
        {
            _pending.bm?.ClearSkillPreviewToken(_skillPreviewToken);
            _skillPreviewToken = 0;
        }
        _pending.bm?.ReleaseSkillPreview();
        _pending.bm?.EmitActionLabel(_pending.owner, "");

        var ai = _pending.owner.GetComponent<EnemyAI>();
        if (ai != null) ai.PlanNextSkill();

        _pending = null;
        _interrupted = false;

        return true;
    }

    public void ClearPreviewAndFinalize(BattleManager bm)
    {
        if (_skillPreviewToken != 0) { bm?.ClearSkillPreviewToken(_skillPreviewToken); _skillPreviewToken = 0; }
        bm?.ReleaseSkillPreview();
        _pending = null;
        _interrupted = false;
    }

    void UpdateCastLabelColor()
    {
        if (_pending == null) return;
        // 스킬명 가져오기(없으면 빈 문자열)
        string name = _pending.skillSO ? _pending.skillSO.displayName : "";

        // 색 결정(3=빨강, 2=주황(FF9B00), 1=노랑, 0/없음=흰색)
        string hex = "FFFFFF";
        switch (_pending.suppressCur)
        {
            case 3: hex = "FF0000"; break;   // red
            case 2: hex = "FF9B00"; break;   // orange (255,155,0)
            case 1: hex = "FFFF00"; break;   // yellow
            default: hex = "FFFFFF"; break;  // white
        }

        // 리치 텍스트로 색 입혀서 방송
        _pending.bm?.EmitActionLabel(_pending.owner, $"<color=#{hex}>{name}</color>");
    }
}
