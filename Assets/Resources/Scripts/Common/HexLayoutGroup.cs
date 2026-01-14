using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HexLayoutGroup : MonoBehaviour
{
    [Header("설정")]
    public float tileSize = 80f;     // 타일 크기 (변경하면 Width/Height도 바뀜)
    public float xSpacing = 80f;     // 가로 간격 (보통 타일 크기랑 비슷하게)
    public float ySpacing = 70f;     // 세로 간격 (타일 크기보다 좀 작게, 겹치게)

    [Header("타일들 (자동 할당됨)")]
    public RectTransform[] hexTiles;

    // 3-4-5-4-3 구조의 각 줄별 개수
    private readonly int[] rows = new int[] { 3, 4, 5, 4, 3 };

    // 인스펙터에서 버튼 누르거나 값 바꿀 때 실행
    public void AlignNodes()
    {
        if (transform.childCount == 0) return;

        // 자식들 가져오기 (이름 순서대로 정렬되었다고 가정)
        hexTiles = new RectTransform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            hexTiles[i] = transform.GetChild(i) as RectTransform;

            // 타일 크기 강제 적용 (원치 않으면 이 두 줄 삭제)
            hexTiles[i].sizeDelta = new Vector2(tileSize, tileSize);
        }

        int currentIndex = 0;

        // 중앙 기준점 잡기 (5줄이니까 0,1,2,3,4 중 2번 줄이 중앙)
        // 세로 위치 계산: (행 번호 - 2) * ySpacing
        // 근데 0번 줄이 맨 위여야 하니까 Y축은 반대로 가야 함.

        for (int r = 0; r < rows.Length; r++)
        {
            int countInRow = rows[r];

            // 이 줄의 시작 X 좌표 계산 (중앙 정렬)
            // 개수가 3개면: -1, 0, 1 (간격 곱하기)
            // 개수가 4개면: -1.5, -0.5, 0.5, 1.5
            float startX = -((countInRow - 1) * xSpacing) / 2f;

            // Y 좌표: 중앙(2행)을 0으로 기준 잡음. 위는 +, 아래는 -
            // 0행(3개): y = +2칸
            // 4행(3개): y = -2칸
            float posY = (2 - r) * ySpacing;

            for (int c = 0; c < countInRow; c++)
            {
                if (currentIndex >= hexTiles.Length) break;

                float posX = startX + (c * xSpacing);

                RectTransform tile = hexTiles[currentIndex];

                // 앵커가 Middle-Center라고 가정하고 좌표 꽂음
                tile.anchoredPosition = new Vector2(posX, posY);

                currentIndex++;
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HexLayoutGroup))]
public class HexLayoutGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        HexLayoutGroup script = (HexLayoutGroup)target;

        GUILayout.Space(10);
        if (GUILayout.Button("정렬 맞추기 (Align)"))
        {
            script.AlignNodes();
        }
    }
}
#endif