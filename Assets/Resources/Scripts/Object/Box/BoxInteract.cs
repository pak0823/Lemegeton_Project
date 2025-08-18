using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BoxInteract : MonoBehaviour
{
    public Animator animator;
    public GameObject fKeyPrompt;   // F키 안내 UI (Text, Image 등)
    private bool isPlayerNear = false;
    private bool isOpened = false;

    // === 하이라이트 필드 ===
    [Header("하이라이트 처리")]
    public SpriteRenderer highlightRenderer;   // 지정 없으면 GetComponent로 자동 할당
    private Color originalColor;
    private bool isHighlighted = false;

    // 인식된 상자 확인
    private bool isFocused = false;

    // === 외부 확인용 프로퍼티 ===
    public bool IsOpened => isOpened;

    private void Awake()
    {
        isOpened = false;

        // SpriteRenderer 캐시 및 원본 색상 저장
        if (highlightRenderer == null)
            highlightRenderer = GetComponent<SpriteRenderer>();
        if (highlightRenderer != null)
            originalColor = highlightRenderer.color;

        // 시작 시 안내 UI OFF
        if (fKeyPrompt != null) fKeyPrompt.SetActive(false);
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (fKeyPrompt != null)
            fKeyPrompt.SetActive(false); // 시작시 꺼두기
    }

    void Update(){}
    // 외부에서 포커스 지정
    public void SetFocused(bool on)
    {
        if (isFocused == on) return;
        isFocused = on;

        // 포커스 기준으로만 안내 UI 제어
        if (fKeyPrompt != null)
            fKeyPrompt.SetActive(isFocused && !isOpened);

        // 포커스 해제 시 안내 UI/하이라이트 정리
        //if (!isFocused)
        //{
        //    if (fKeyPrompt != null) fKeyPrompt.SetActive(false);
        //    SetHighlight(false);
        //}
        // 포커스 획득 시에는 PlayerMovement에서 하이라이트를 켜준다
    }

    // PushObject와 동일 패턴의 하이라이트 메서드
    public void SetHighlight(bool on)
    {
        if (highlightRenderer != null && isHighlighted != on)
        {
            // 열렸으면 굳이 켜지 않도록 방어
            if (on && isOpened) on = false;

            // PushObject는 노란색, 상자는 시각적으로 구분되도록 청록 계열 예시
            highlightRenderer.color = on ? new Color(0.4f, 1f, 1f, 1f) : originalColor;
            isHighlighted = on;
        }
    }

    // 실제 열기 동작 함수
    public void OpenChest()
    {
        if (isOpened) return;
        isOpened = true;
        animator.SetBool("IsOpen", isOpened);
        Shared.ObjectGaugeManager.IncrementChest();

        // 열린 뒤에는 포커스/하이라이트/안내 UI 정리
        SetHighlight(false);
        if (fKeyPrompt != null) fKeyPrompt.SetActive(false);
        isFocused = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;

            // 안전하게 안내 UI/하이라이트 꺼줌
            if (!isOpened)
            {
                SetHighlight(false);
                if (fKeyPrompt != null) fKeyPrompt.SetActive(false);
                isFocused = false;
            }
        }
    }
    
}
