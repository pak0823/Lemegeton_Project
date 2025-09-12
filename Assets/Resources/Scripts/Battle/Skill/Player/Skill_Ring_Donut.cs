using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Battle/Skills/Player/Ring Donut (Tile)", fileName = "Skill_Ring_Donut")]
public class Skill_Ring_Donut : SkillAsset
{
    [Min(1)] public int innerRadius = 1;
    [Min(1)] public int outerRadius = 2;

    private void OnEnable()
    {
        targetMode = SkillTargetMode.Tile;
        legacyId = SkillId.Skill3; // 시전 후 이동 없음
    }

    public override IEnumerable<Vector3Int> GetAreaCells(Vector3Int originCell, bool isOddColumn)
    {
        var center = SkillLibrary.OffsetToAxial(originCell);
        // 중심 포함 여부를 원하면 아래 라인 주석 해제
        // yield return originCell;

        for (int r = innerRadius; r <= outerRadius; r++)
        {
            foreach (var ax in AxialsInRange(center, r))
            {
                if (HexDistance(center, ax) == r)
                    yield return SkillLibrary.AxialToOffset(ax);
            }
        }
    }

    public override IEnumerator ResolveOnTile(BattleManager bm, Tilemap map, Vector3Int originCell, BattleUnit caster)
    {
        var area = GetAreaCells(originCell, SkillLibrary.IsOddColumn(originCell));
        var hits = bm.GetUnitsInArea(map, area);
        bm.ExecuteSkillDamage(caster, hits, this, map, originCell);
        yield break;
    }

    public override IEnumerator ResolveOnUnit(BattleManager bm, BattleUnit caster, BattleUnit target)
    { yield break; }

    // ---------- 헬퍼(부채꼴과 동일) ----------
    static IEnumerable<Vector2Int> AxialsInRange(Vector2Int center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            int minDy = Mathf.Max(-radius, -dx - radius);
            int maxDy = Mathf.Min(radius, -dx + radius);
            for (int dy = minDy; dy <= maxDy; dy++)
            {
                int dz = -dx - dy;
                int q = center.x + dx;
                int r = center.y + dz;
                yield return new Vector2Int(q, r);
            }
        }
    }

    static int HexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = Mathf.Abs(a.x - b.x);
        int dr = Mathf.Abs(a.y - b.y);
        int ds = Mathf.Abs((a.x + a.y) - (b.x + b.y));
        return (dq + dr + ds) / 2;
    }
}
