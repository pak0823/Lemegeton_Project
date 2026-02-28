using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public struct SkillDefinition
{
    public SkillId id;
    public string name;
    public SkillTargetMode targetMode;

    // 기준 셀(origin)과 '컬럼 홀짝'을 받아 범위 셀 컬렉션 반환
    public Func<Vector3Int, bool, IEnumerable<Vector3Int>> GetAreaCells;
}

public static class SkillLibrary
{
    public static Vector2Int ToAxial(Vector3Int cell) => OffsetToAxial(cell);
    public static Vector3Int ToOffset(Vector2Int axial, int z = 0) => AxialToOffset(axial, z);

    public static Vector2Int OffsetToAxial(Vector3Int cell)
    {
        int y = cell.y;
        int q = cell.x - ((y - (y & 1)) / 2);
        int r = y;
        return new Vector2Int(q, r);
    }

    public static Vector3Int AxialToOffset(Vector2Int axial, int z = 0)
    {
        int q = axial.x;
        int r = axial.y;
        int x = q + ((r - (r & 1)) / 2);
        int y = r;
        return new Vector3Int(x, y, z);
    }

    // 외부 코드 호환을 위해 남겨둠(이름은 column이지만 실제론 row 홀짝을 쓴다)
    public static bool IsOddColumn(Vector3Int cell)
    {
        // 행(y) 홀짝으로 판단 (odd-r)
        return (cell.y & 1) != 0;
    }
    static IEnumerable<Vector3Int> ToOffsetCells(Vector3Int originOffset, IEnumerable<Vector2Int> axialOffsets)
    {
        var originAx = OffsetToAxial(originOffset);
        foreach (var d in axialOffsets)
        {
            var ax = new Vector2Int(originAx.x + d.x, originAx.y + d.y);
            yield return AxialToOffset(ax);
        }
    }

    static IEnumerable<Vector3Int> Unique(IEnumerable<Vector3Int> cells)
        => cells.Distinct();

    // =========================
    // 2) 스킬별 범위 (axial 기준)
    //
    // Pointed-Top 기준 6방향(축좌표) 메모:
    //  E (1, 0), NE (1,-1), NW (0,-1), W (-1,0), SW (-1,1), SE (0,1)
    // =========================

    // axial 기준 6방향(이웃) - 5번 도넛 스킬
    static readonly Vector2Int[] AXIAL_NEIGHBORS = new[]
    {
        new Vector2Int( 1, 0),  // E
        new Vector2Int( 1,-1),  // NE
        new Vector2Int( 0,-1),  // NW
        new Vector2Int(-1, 0),  // W
        new Vector2Int(-1, 1),  // SW
        new Vector2Int( 0, 1),  // SE
    };
    // 중심 포함 R=1 (중심 + 주변 6칸)
    static readonly Vector2Int[] AXIAL_RADIUS1_WITH_CENTER = new[]
    {
        new Vector2Int( 0, 0),  // Center
        new Vector2Int( 1, 0),  // E
        new Vector2Int( 1,-1),  // NE
        new Vector2Int( 0,-1),  // NW
        new Vector2Int(-1, 0),  // W
        new Vector2Int(-1, 1),  // SW
        new Vector2Int( 0, 1)  // SE
    };

    // 방향 인덱스(0..5) → axial(q,r) 오프셋
    public static Vector2Int DirIndexToAxial(int idx)
    {
        idx = ((idx % 6) + 6) % 6;
        return AXIAL_NEIGHBORS[idx];
    }
    // 두 오프셋 셀 사이의 '가장 가까운 6방향' 인덱스(0..5)
    public static int NearestDirectionIndex(Vector3Int fromOffset, Vector3Int toOffset)
    {
        var a = OffsetToAxial(fromOffset);
        var b = OffsetToAxial(toOffset);
        var d = new Vector2Int(b.x - a.x, b.y - a.y);
        if (d.x == 0 && d.y == 0) return 0; // 같으면 임의 0

        int best = 0; int bestDot = int.MinValue;
        for (int i = 0; i < 6; i++)
        {
            var v = AXIAL_NEIGHBORS[i];
            int dot = d.x * v.x + d.y * v.y; // 근사 방향 비교
            if (dot > bestDot) { bestDot = dot; best = i; }
        }
        return best;
    }

    // 가로3
    public static IEnumerable<Vector3Int> GetAreaHorizontal3(Vector3Int originCell)
    {
        // 기존 직접 계산 → AreaShapes 위임
        return AreaShapes.GetCells(originCell, AreaPreset.LineHorizontal, false);
    }

    // 세로3
    public static IEnumerable<Vector3Int> GetAreaVertical3(Vector3Int originCell)
    {
        return AreaShapes.LineVertical3(originCell);
    }

    // 원형(반경1, 중심 포함)
    public static IEnumerable<Vector3Int> GetAreaRing1(Vector3Int originCell)
    {
        return AreaShapes.GetCells(originCell, AreaPreset.Ring, false);
    }

    // 도넛(반경1, 중심 제외)
    public static IEnumerable<Vector3Int> GetAreaDonut1(Vector3Int originCell)
    {
        return AreaShapes.DonutRadius1(originCell);
    }

    // 부채꼴(반경1, 전방 3칸)
    //  - 기존 코드가 정면을 유닛 에이밍이나 'caster→target'으로 정했다면,
    //    거기서 axial 정면 벡터를 만들어 이 함수에 넘겨줘.
    public static IEnumerable<Vector3Int> GetAreaFanForwardR1(Vector3Int originCell, Vector3 casterWorld, Vector3 targetWorld)
    {
        // 월드 정면 → 축 좌표 정면 근사
        Vector2 aim = (targetWorld - casterWorld);
        if (aim.sqrMagnitude < 1e-6f) aim = Vector2.right;
        aim.Normalize();

        // 월드->axial 6방향 중 가장 가까운 축 선택
        var axialDirs = new[]
        {
        new Vector2Int( 1, 0), new Vector2Int( 1,-1), new Vector2Int( 0,-1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int( 0, 1),
    };

        // 축 벡터를 월드로 투영해 가장 큰 dot을 정면으로 선택
        Vector3 right = Vector3.right, up = Vector3.up; // 필요시 Grid 변환 축을 사용하도록 개선
        int best = 0; float bestDot = float.NegativeInfinity;
        for (int i = 0; i < axialDirs.Length; i++)
        {
            // 간단히: (ax.x * right + ax.y * up) 로 근사 (프로젝트 좌표계에 맞게 교체 가능)
            Vector2 dirW = axialDirs[i].x * (Vector2)right + axialDirs[i].y * (Vector2)up;
            float d = Vector2.Dot(aim, dirW.normalized);
            if (d > bestDot) { bestDot = d; best = i; }
        }

        return AreaShapes.FanForwardR1(originCell, axialDirs[best]);
    }

    //public static SkillDefinition Get(SkillId id)
    //{

    //    switch (id)
    //    {
    //        // a) 대상 지목 가로열: {W, SELF, E}
    //        case SkillId.Skill1:
    //            return new SkillDefinition
    //            {
    //                id = id,
    //                name = "Skill 1: 가로열(Unit)",
    //                targetMode = SkillTargetMode.Unit,
    //                GetAreaCells = (origin, _) =>
    //                    Unique(ToOffsetCells(origin, new[]
    //                    {
    //                        new Vector2Int(-1, 0), // W
    //                        new Vector2Int( 0, 0), // SELF
    //                        new Vector2Int( 1, 0), // E
    //                    }))
    //            };

    //        // b) 대상 지목 세로열: {NW, SELF, SE}  → axial {(0,-1),(0,0),(0,1)}
    //        case SkillId.Skill2:
    //            return new SkillDefinition
    //            {
    //                id = id,
    //                name = "Skill 2: 세로열(Unit)",
    //                targetMode = SkillTargetMode.Unit,
    //                GetAreaCells = (origin, _) =>
    //                    Unique(ToOffsetCells(origin, new[]
    //                    {
    //                        new Vector2Int( 0,-1), // NW
    //                        new Vector2Int( 0, 0), // SELF
    //                        new Vector2Int( 0, 1), // SE
    //                    }))
    //            };

    //        // c) 대상 지목 부채꼴(대상 포함, '왼쪽-위 방향'으로 펼치는 3칸)
    //        //    요청의 예시 {(0,0),(-1,0),(-1,-1)}는 odd-q 기준이었다고 보이지만,
    //        //    axial로 동일 의도를 반영: {SELF, W(-1,0), NW(0,-1)}
    //        case SkillId.Skill3:
    //            return new SkillDefinition
    //            {
    //                id = id,
    //                name = "Skill 3: 부채꼴(Unit)",
    //                targetMode = SkillTargetMode.Unit,
    //                GetAreaCells = (origin, _) =>
    //                    Unique(ToOffsetCells(origin, new[]
    //                    {
    //                        new Vector2Int( 0, 0), // SELF
    //                        new Vector2Int(-1, 0), // W
    //                        new Vector2Int( 0,-1), // NW
    //                    }))
    //            };

    //        // d) 타일 지목: 중심 포함 R=1 (중심 + 주변 6칸)
    //        case SkillId.Skill4:
    //            return new SkillDefinition
    //            {
    //                id = id,
    //                name = "Skill 4: 원형(Tile)",
    //                targetMode = SkillTargetMode.Tile,
    //                GetAreaCells = (origin, _) =>
    //                    Unique(ToOffsetCells(origin, AXIAL_RADIUS1_WITH_CENTER))
    //            };
    //        // d) 타일 지목 도넛형6 - 수정 필요
    //        case SkillId.Skill5:
    //            return new SkillDefinition
    //            {
    //                id = id,
    //                name = "Skill 5: 도넛형(Tile)",
    //                targetMode = SkillTargetMode.Tile,
    //                GetAreaCells = (origin, _) =>
    //                    Unique(ToOffsetCells(origin, AXIAL_NEIGHBORS))
    //            };

    //        default:
    //            return new SkillDefinition
    //            {
    //                id = id,
    //                name = id.ToString(),
    //                targetMode = SkillTargetMode.Unit,
    //                GetAreaCells = (origin, _) => new[] { origin }
    //            };
    //    }
    //}
}
