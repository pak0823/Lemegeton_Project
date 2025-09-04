using UnityEngine;
using UnityEngine.UI;

public static class GamePause
{
    public static bool IsPaused = false;
}

public class GameSpeedController : MonoBehaviour
{
    [Header("Speed cycle")]
    float[] speeds = { 1f, 2f, 3f, 0f };

    [Header("UI")]
    public Image buttonImage;          // 배속 버튼의 Image
    public Sprite[] speedIcons;        // 각 배속에 대응하는 아이콘(배열 길이 = speeds 길이)
    public Text label;             // “x1 / x2 / x3” 표기용

    int index = 0;
    int lastNonZeroIndex = 0;     // 최근에 사용한 0이 아닌 속도의 인덱스
    int prevIndexForHudPause = -1;// HUD로 인한 일시정지 직전 인덱스(복귀용)
    float baseFixedDeltaTime;

    [SerializeField] private CanvasGroup uiGroup;   // 전체 UI CanvasGroup 참조
    [SerializeField] private Button speedButton;    // 배속 버튼
    [SerializeField] private Button optionButton;   // 옵션 버튼

    void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
        Apply(); // 시작은 1x
    }

    void OnDisable()
    {
        // 씬 전환/비활성 시 기본속도로 복원
        index = 0; 
        Apply();
    }

    public void CycleSpeed(int step = 1) { index = (index + step + speeds.Length) % speeds.Length; Apply(); }

    // HUD가 꺼질 때 호출 - “끄기 직전 인덱스” 기억 후 정지로 전환
    public void PauseRemember()
    {
        // 현재가 0이 아니면 그 인덱스를 복귀용으로 저장
        if (speeds[index] != 0f) prevIndexForHudPause = index;

        // 정지 인덱스를 찾아서 전환
        int pauseIdx = IndexOfSpeed(0f);
        if (pauseIdx < 0) pauseIdx = 0; // 방어
        SetSpeedIndex(pauseIdx);
    }

    // HUD가 켜질 때 호출 - “끄기 직전 인덱스(또는 마지막 유효 배속)”로 복귀
    public void ResumeRemembered()
    {
        int target =
            (prevIndexForHudPause >= 0) ? prevIndexForHudPause : lastNonZeroIndex;

        // 그래도 안전망: 만약 target이 0(정지)이면 lastNonZeroIndex로 시도
        if (speeds[target] == 0f) target = lastNonZeroIndex;

        // 둘 다 0이었다면 기본 1x(인덱스 0)로
        if (speeds[target] == 0f) target = 0;

        SetSpeedIndex(target);
        prevIndexForHudPause = -1; // 한 번 쓰고 비움
    }
    public void SetSpeedIndex(int i)
    {
        index = (i % speeds.Length + speeds.Length) % speeds.Length;
        Apply();
    }

    void Apply()
    {
        float s = speeds[index];
        Time.timeScale = s;
        Time.fixedDeltaTime = baseFixedDeltaTime * (s > 0 ? s: 1f); // 물리 보폭 동기화

        GamePause.IsPaused = (s == 0f);

        // 버튼 단위로 interactable 설정
        if(uiGroup)
        {
            // 필요시 비활성 포함하고 싶으면 (true) 사용
            var buttons = uiGroup.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (!btn) continue; // 혹시라도 파괴 중이면 건너뜀

                if ((speedButton && btn == speedButton) || (optionButton && btn == optionButton))
                    btn.interactable = true;
                else
                    btn.interactable = !GamePause.IsPaused;
            }
        }
        
        // 0이 아닌 배속으로 전환될 때마다 기록
        if (s > 0f) lastNonZeroIndex = index;

        // 아이콘/라벨 업데이트
        UpdateVisuals();

        if (s == 0f)
            Debug.Log("[Speed] 정지");
        else
        {
            Debug.Log($"[Speed] {s:0}배속");
        }
            
    }
    // 특정 속도값의 인덱스를 찾기
    int IndexOfSpeed(float value)
    {
        for (int i = 0; i < speeds.Length; i++)
            if (Mathf.Approximately(speeds[i], value)) return i;
        return -1;
    }

    void UpdateVisuals()
    {
        if (buttonImage != null && speedIcons != null && index < speedIcons.Length)
        {
            buttonImage.sprite = speedIcons[index];
            // 버튼 이미지 비율이 깨지면 주석 해제:
            // buttonImage.SetNativeSize();
            // buttonImage.preserveAspect = true;
        }

        // 숫자 라벨 표기
        if (label != null)
        {
            if (speeds[index] == 0f)
                label.text = "||"; // 정지 아이콘 텍스트
            else
            {
                label.text = $"x{speeds[index]:0}";   // 1, 2, 3처럼 정수로 보이게 하고 싶으면 0.# 대신 0 사용
            }

            label.raycastTarget = false;    // 라벨이 입력을 가로채지 않도록
        }
    }

    public float CurrentSpeed => speeds[index];
    
    //public int CurrentIndex => index;   // (원하면 외부에서 현재 인덱스도 확인 가능하게)
}
