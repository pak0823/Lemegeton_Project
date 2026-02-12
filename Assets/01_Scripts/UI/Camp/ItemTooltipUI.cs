using UnityEngine;

using UnityEngine.UI;



public class ItemTooltipUI : MonoBehaviour

{

    public static ItemTooltipUI Instance;



    [SerializeField] private Text nameText;

    [SerializeField] private Text descText;

    [SerializeField] private Text typeText;

    [SerializeField] private RectTransform rectTransform;

    private Canvas _canvas;
    private Camera _uiCamera;

    void Awake()
    {
        Instance = this;
        _canvas = GetComponent<Canvas>();

        // 직접 UICamera를 태그나 이름으로 찾거나, 부모로부터 확실히 가져옴
        if (_uiCamera == null)
        {
            // 씬에 있는 UICamera를 직접 찾아버리는 게 가장 확실함
            GameObject camGo = GameObject.Find("UICamera");
            if (camGo != null) _uiCamera = camGo.GetComponent<Camera>();
        }

        if (_canvas != null)
        {
            _canvas.worldCamera = _uiCamera;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 2000;
            // Plane Distance도 코드에서 한번 더 잡아주면 안전함
            _canvas.planeDistance = 10f; 
        }
    
        gameObject.SetActive(false);
    }



    public void Show(ItemData data)

    {

        if (data == null) return;
        gameObject.SetActive(true);

        nameText.text = data.itemName;
        descText.text = data.itemDescription;
        typeText.text = data.itemType.ToString();   // ItemData에 type이나 등급 정보가 있다면 추가 표시

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
        if (gameObject.activeSelf) UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_uiCamera == null) return;

        Vector2 mousePos = Input.mousePosition;

        // 카메라로부터 Plane Distance만큼 떨어진 지점에 마우스 월드 좌표 생성
        // 부모 캔버스의 Plane Distance가 10이면 여기서도 10을 줘야 정확한 평면에 안착한다.
        Vector3 worldPoint = _uiCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));

        // 툴팁의 월드 포지션에 직접 대입 (Z축은 0으로 자동 보정됨)
        rectTransform.position = worldPoint;

        // 피벗 보정: 커서가 툴팁을 가리지 않게 (선택 사항)
        float pivotX = (mousePos.x > Screen.width * 0.75f) ? 1f : 0f;
        float pivotY = (mousePos.y < Screen.height * 0.25f) ? 0f : 1.1f;
        rectTransform.pivot = new Vector2(pivotX, pivotY);
    
        // 중첩 캔버스 환경에서 Z축이 튀는 걸 방지하기 위해 로컬 Z를 0으로 고정
        rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0f);
    }
}