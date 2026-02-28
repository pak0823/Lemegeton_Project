using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static CraftHeaderController;

public class CharacterHeaderController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button arrowLeftButton;
    [SerializeField] private Button arrowRightButton;

    [Header("Character Selection")]
    public List<Toggle> charToggles = new List<Toggle>();
    private int currentCharIndex = 0;


    private void Start()
    {
        // 테스트용: 강제로 초기화 실행해보기
        Initialize((idx) => Debug.Log($"캐릭터 변경됨: {idx}"));
    }
    public void Initialize(System.Action<int> onCharChanged)
    {
        for (int i = 0; i < charToggles.Count; i++)
        {
            int index = i;

            charToggles[i].onValueChanged.AddListener((isOn) => {
                if (isOn)
                {
                    // 클릭 시 현재 위치 동기화
                    currentCharIndex = index;
                    onCharChanged?.Invoke(index);
                }
            });
        }

        // 첫 번째 캐릭터 강제 선택 로직
        if (charToggles.Count > 0)
        {
            if (charToggles[0].isOn)
            {
                currentCharIndex = 0;
                onCharChanged?.Invoke(0); // 강제 호출
            }
            else
            {
                charToggles[0].isOn = true; // 꺼져 있었다면 켜지면서 이벤트 발생
            }
        }
            

        if (arrowLeftButton)
        {
            arrowLeftButton.onClick.AddListener(() => SelectNextCharacter(-1));
        }

        if (arrowRightButton)
        {
            arrowRightButton.onClick.AddListener(() => SelectNextCharacter(1));
        }
    }

    public void SelectNextCharacter(int direction)
    {
        if (charToggles.Count == 0) return;

        int nextIndex = Mathf.Clamp(currentCharIndex + direction, 0, charToggles.Count - 1);
        if (nextIndex != currentCharIndex)
        {
            charToggles[nextIndex].isOn = true;
        }
    }
}