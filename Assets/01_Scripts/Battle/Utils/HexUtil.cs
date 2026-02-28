using UnityEngine;

public static class HexUtil
{
    public static int GetDistance(Vector3Int a, Vector3Int b)
    {
        // SkillLibrary의 좌표 변환 활용
        var axA = SkillLibrary.OffsetToAxial(a);
        var axB = SkillLibrary.OffsetToAxial(b);

        int dq = Mathf.Abs(axA.x - axB.x);
        int dr = Mathf.Abs(axA.y - axB.y);
        int ds = Mathf.Abs((-axA.x - axA.y) - (-axB.x - axB.y));

        return (dq + dr + ds) / 2;
    }
}