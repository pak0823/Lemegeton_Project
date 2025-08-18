using UnityEngine;
using UnityEngine.UI;

public static class GamePause
{
    public static bool IsPaused = false;
}

public class GameSpeedController : MonoBehaviour
{
    [Header("Speed cycle")]
    public float[] speeds = { 1f, 1.5f, 2f, 0f };

    [Header("UI")]
    public Image buttonImage;          // 배속 버튼의 Image
    public Sprite[] speedIcons;        // 각 배속에 대응하는 아이콘(배열 길이 = speeds 길이)
    public Text label;             // “x1 / x1.5 / x2” 표기용

    int index = 0;
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

    public void ToggleSpeed()
    {
        index = (index + 1) % speeds.Length;
        Apply();
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
        Button[] buttons = uiGroup.GetComponentsInChildren<Button>();
        foreach (Button btn in buttons)
        {
            if (btn == speedButton || btn == optionButton)
                btn.interactable = true;   // 예외 버튼
            else
                btn.interactable = !GamePause.IsPaused;
        }

        // 아이콘/라벨 업데이트
        UpdateVisuals();

        if (s == 0f)
            Debug.Log("[Speed] 정지");
        else
            Debug.Log($"[Speed] {s:0.#}배속");
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
                label.text = $"x{speeds[index]:0.#}";   // 1, 2, 3처럼 정수로 보이게 하고 싶으면 0.# 대신 0 사용
            }

            label.raycastTarget = false;    // 라벨이 입력을 가로채지 않도록
        }
    }

    public float CurrentSpeed => speeds[index];
}
