using UnityEngine;
using UnityEngine.UI;
using System;

public class CampPassiveSlot : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Text passiveNameText;   // 일반/진행중 텍스트
    [SerializeField] private Button awakenButton;    // 해금 버튼 (노란색 Awakened!!)
    [SerializeField] private Text awakenBtnText;     // 버튼 내부 텍스트
    [SerializeField] private Button selfButton;      // 이 슬롯 자체에 달린 버튼 (클릭 감지용)
    [SerializeField] private UI_ButtonFeedback visualFeedback;

    private PassiveAsset _passive;

    // 데이터 세팅 및 UI 갱신
    public void Setup(PassiveAsset passive, Action<string, Transform> onSelected)
    {
        _passive = passive;

        // 버튼 컴포넌트 자동 찾기
        if (selfButton == null) selfButton = GetComponent<Button>();
        // 피드백 컴포넌트 자동 찾기
        if (visualFeedback == null) visualFeedback = GetComponent<UI_ButtonFeedback>();

        // 버튼 이벤트 연결
        selfButton.onClick.RemoveAllListeners();
        selfButton.onClick.AddListener(() =>
        {
            if (_passive != null && _passive.IsUnlocked())
            {
                Transform target = (passiveNameText != null) ? passiveNameText.transform : this.transform;
                onSelected?.Invoke(_passive.description, target);
            }
        });

        RefreshState();
    }

    private void RefreshState()
    {
        if (_passive == null) return;

        bool isUnlocked = _passive.IsUnlocked();
        float progress = _passive.GetProgress(); // 0.0 ~ 1.0

        if (isUnlocked)
        {
            // 이미 해금됨 -> 패시브 이름 표시
            passiveNameText.gameObject.SetActive(true);
            awakenButton.gameObject.SetActive(false);

            passiveNameText.text = _passive.displayName;

            if (visualFeedback != null)
            {
                visualFeedback.enabled = true; // 스크립트 활성화
                visualFeedback.SetNormalColor(Color.white); // 기준 색상 흰색
            }
            else
            {
                passiveNameText.color = Color.white;
            }
        }
        else
        {
            // 해금 안 됨. 진행도 체크
            if (progress >= 1.0f)
            {
                // 100% 달성 -> Awakened 버튼 활성화
                passiveNameText.gameObject.SetActive(false);
                awakenButton.gameObject.SetActive(true);

                awakenBtnText.text = "Awakened!!";

                // 버튼 클릭 이벤트 연결 (중복 방지 위해 리스너 초기화)
                awakenButton.onClick.RemoveAllListeners();
                awakenButton.onClick.AddListener(OnClickAwaken);
            }
            else
            {
                // 진행 중 -> "Awakening... (88%)"
                passiveNameText.gameObject.SetActive(true);
                awakenButton.gameObject.SetActive(false);

                int percent = Mathf.FloorToInt(progress * 100f);
                passiveNameText.text = $"Awakening... ({percent}%)";

                if (visualFeedback != null)
                {
                    visualFeedback.enabled = false;
                }

                // 강제로 회색 적용
                passiveNameText.color = Color.gray;
            }
        }
    }

    // 버튼 클릭 시 실행
    private void OnClickAwaken()
    {
        if (_passive != null)
        {
            _passive.Unlock(); // 데이터 해금 처리
            RefreshState();    // UI 즉시 갱신 (버튼 -> 이름 텍스트로 변경됨)

            // 필요하다면 상위 페이지에 알림을 보내서 전체 갱신을 할 수도 있음
        }
    }
}