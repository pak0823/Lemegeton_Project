using UnityEngine;
using UnityEngine.UI;

public class ItemTooltipUI : MonoBehaviour
{
    public static ItemTooltipUI Instance;

    [SerializeField] private Text nameText;
    [SerializeField] private Text descText;
    [SerializeField] private Text typeText;
    [SerializeField] private RectTransform rectTransform;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false); // 처음엔 끔
    }

    public void Show(ItemData data)
    {
        if (data == null) return;

        gameObject.SetActive(true);
        nameText.text = data.itemName;
        descText.text = data.itemDescription;
        // ItemData에 type이나 등급 정보가 있다면 추가 표시
        // typeText.text = data.itemType.ToString();

        // 마우스 위치로 즉시 이동
        UpdatePosition();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        // 툴팁이 켜져 있는 동안 마우스를 따라다니게 함
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        Vector2 mousePos = Input.mousePosition;
        // 툴팁이 마우스 커서에 가려지지 않게 약간 오프셋을 줌
        rectTransform.position = mousePos + new Vector2(10f, -10f);
    }
}