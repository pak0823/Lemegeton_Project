using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Progress;

public class TurnBarUI : MonoBehaviour
{
    [Header("UI Settings")]
    public RectTransform barImage;          // 바(이미지)의 RectTransform
    public GameObject unitIconPrefab;  // 아이콘 프리팹(Anchor=Left, Pivot=Center 권장)

    [Tooltip("플레이어/적 아이콘 Y 오프셋")]
    public float playerRowY = 20f;
    public float enemyRowY = -20f;

    [Tooltip("아이콘 간 추가 간격(아이콘 너비에 더해짐)")]
    public float extraSpacing = 0f;

    [Tooltip("체크 시 아이콘 간격 보정 없이 '겹치도록' 배치합니다.")]
    public bool allowOverlap = false;

    public float barWidth;      // 현재 바 너비
    Dictionary<BattleUnit, Image> unitIcons = new();

    void Start()
    {
        var battle = FindObjectOfType<BattleManager>();
        barWidth = barImage.rect.width;

        if (battle != null)
        {
            battle.OnATBChanged += OnATBChanged_RelayoutAll;
            InitializeIcons();
            // 최초 1회 전체 그리기
            RelayoutAll();
        }
    }
    void OnDestroy()
    {
        var battle = FindObjectOfType<BattleManager>();
        if (battle != null) battle.OnATBChanged -= OnATBChanged_RelayoutAll;
    }

    void InitializeIcons()
    {
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            var iconGO = Instantiate(unitIconPrefab, barImage);
            var img = iconGO.GetComponent<Image>();
            if (u.data != null) img.sprite = u.data.UnitIcon;

            unitIcons[u] = img;

            var rt = img.rectTransform;
            // 초기 Y를 팀별로 분리
            rt.anchoredPosition = new Vector2(0f, u.team == Team.Player ? playerRowY : enemyRowY);

            // 사망 이벤트 구독 → 제거
            u.OnDied += RemoveUnitIcon;
        }
    }

    void OnATBChanged_RelayoutAll(BattleUnit _, float __, float ___)
    {
        // 개별 변화 시에도 전체를 재배치(겹침 방지 위해)
        RelayoutAll();
    }

    void RelayoutAll()
    {
        if (barImage == null || unitIcons.Count == 0) return;

        // 두 줄로 분리
        var players = new List<Item>();
        var enemies = new List<Item>();

        foreach (var kv in unitIcons.ToArray()) // 혹시 파괴된 유닛/아이콘이 사전에 남아있을 수 있음
        {
            var unit = kv.Key;
            var img = kv.Value;
            if (unit == null || img == null) { unitIcons.Remove(unit); continue; }

            var rt = img.rectTransform;
            float width = rt.rect.width;
            float half = Mathf.Max(1f, width * 0.5f);
            float pivot = rt.pivot.x;
            float minX = width * pivot;
            float maxX = Mathf.Max(minX, barWidth - width * (1f - pivot));

            // 0~1 정규화 ATB
            float normalized = unit.MaxATB > 0 ? Mathf.Clamp01(unit.ATB / unit.MaxATB) : 0f;
            float desired = Mathf.Lerp(minX, maxX, normalized);

            var item = new Item
            {
                unit = unit,
                rt = rt,
                half = half,
                width = width,
                pivot = pivot,
                desired = desired,
                y = (unit.team == Team.Player) ? playerRowY : enemyRowY
            };

            if (unit.team == Team.Player) players.Add(item); else enemies.Add(item);
        }

        ArrangeRow(players);
        ArrangeRow(enemies);
    }
    void ArrangeRow(List<Item> row)
    {
        if (row.Count == 0) return;

        // 원하는 위치 기준으로 정렬
        row.Sort((a, b) => a.desired.CompareTo(b.desired));

        //  겹침 허용
        if (allowOverlap)
        {
            for (int i = 0; i < row.Count; i++)
            {
                var it = row[i];

                float minX = it.width * it.pivot;
                float maxX = barWidth - it.width * (1f - it.pivot);
                float x = Mathf.Clamp(it.desired, minX, maxX);

                it.rt.anchoredPosition = new Vector2(x, it.y);

                // 오른쪽(더 뒤 순번)일수록 위로 올리려면
                it.rt.SetSiblingIndex(Mathf.Min(barImage.childCount - 1, i));

                // 반대로 왼쪽이 위로 오게 하려면:
                //it.rt.SetSiblingIndex(Mathf.Min(barImage.childCount - 1, row.Count - 1 - i));
            }
            return;
        }

        // 좌→우로 진행하며 겹치지 않게 '오른쪽으로' 민다
        for (int i = 0; i < row.Count; i++)
        {
            var cur = row[i]; // 복사본
            float minX = cur.width * cur.pivot;                   // 왼쪽 경계 내 피벗 최소
            float maxX = barWidth - cur.width * (1f - cur.pivot);        // 오른쪽 경계 내 피벗 최대

            float x = Mathf.Clamp(cur.desired, minX, maxX);

            if (i > 0)
            {
                var prev = row[i - 1]; // 이미 확정된 값 사용
                float minSep = prev.x + prev.half + cur.half + extraSpacing;
                if (x < minSep) x = minSep; //옆 아이콘과 겹치지 않기
            }

            cur.x = Mathf.Clamp(x, minX, maxX);
            row[i] = cur;   // 수정된 값 되돌려놓기
        }

        // 3) 우→좌로 진행하며 오른쪽 경계 초과/겹침을 좌측으로 보정
        for (int i = row.Count - 1; i >= 0; i--)
        {
            var cur = row[i]; // 복사본
            float minX = cur.width * cur.pivot;
            float maxX = barWidth - cur.width * (1f - cur.pivot);

            if (cur.x > maxX) cur.x = maxX;

            if (i < row.Count - 1)
            {
                var next = row[i + 1]; // 오른쪽 아이콘
                float maxLeft = next.x - (next.half + cur.half + extraSpacing);
                if (cur.x > maxLeft) cur.x = Mathf.Max(minX, maxLeft);
            }

            row[i] = cur; // 수정된 값 되돌려놓기
        }

        // 적용
        foreach (var it in row)
        {
            it.rt.anchoredPosition = new Vector2(it.x, it.y);
        }
    }


    // 유닛 사망 시 아이콘 제거
    void RemoveUnitIcon(BattleUnit unit)
    {
        if (!unitIcons.ContainsKey(unit)) return;

        Destroy(unitIcons[unit].gameObject);
        unitIcons.Remove(unit);
    }

    struct Item
    {
        public BattleUnit unit;
        public RectTransform rt;
        public float half;
        public float width;
        public float pivot; // rt.pivot.x
        public float desired;
        public float x;
        public float y;
    }
}
