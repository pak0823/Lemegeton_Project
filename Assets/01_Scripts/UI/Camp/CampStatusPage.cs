using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CampStatusPage : MonoBehaviour
{
    [Header("Text Components")]
    [SerializeField] private Text nameText; // 캐릭터 이름

    [Header("Stats Texts")]
    [SerializeField] private Text strText; // 근력 (PhysicalDamage)
    [SerializeField] private Text magText; // 총명 (MagicDamage)
    [SerializeField] private Text agiText; // 민첩 (AGI)
    [SerializeField] private Text bdyText; // 신체 (BDY)
    [SerializeField] private Text mndText; // 정신 (MND)
    [SerializeField] private Text insText; // 통찰 (INS)

    [Header("Passive/Trait Roots")]
    [SerializeField] private Transform passiveListRoot; // Left_Passive_Area
    [SerializeField] private Transform traitListRoot;   // Right_Trait_Area

    [Header("Prefabs")]
    [SerializeField] private GameObject passiveSlotPrefab; // 패시브 슬롯 프리팹
    [SerializeField] private GameObject traitSlotPrefab; // 성격 슬롯 프리팹
    [SerializeField] private Slider bondSlider; // 유대 게이지

    [Header("Description UI")]
    [SerializeField] private Text descriptionTitle; // 설명 텍스트
    [SerializeField] private Text descriptionText; // 설명 텍스트 (Bottom_Context에 있는거)
    [SerializeField] private RectTransform selectionArrow; // 아까 만든 화살표
    [SerializeField] private Vector2 arrowOffset = new Vector2(-150f, 0f); // 화살표 위치 보정값

    // 페이지가 켜질 때마다 자동 갱신
    private void OnEnable()
    {
        RefreshUI();
    }

    // 선택 상태 초기화 함수
    private void ResetSelection()
    {
        // 화살표 끄기
        if (selectionArrow != null)
            selectionArrow.gameObject.SetActive(false);

        // 제목 텍스트 비우기
        if (descriptionTitle != null)
            descriptionTitle.text = "";

        // 설명 텍스트 비우기
        if (descriptionText != null)
            descriptionText.text = "";
    }

    // 외부(CampUIManager)에서 캐릭터를 바꿀 때 호출할 함수
    public void RefreshUI()
    {
        // 갱신 시작 전에 무조건 선택 상태부터 초기화
        ResetSelection();

        // 현재 선택된 유닛 가져오기
        if (CampUIManager.Instance == null) return;
        UnitData unit = CampUIManager.Instance.selectedUnit;

        // 유닛이 없으면 비워두기
        if (unit == null)
        {
            ClearUI();
            return;
        }

        // UI 갱신
        nameText.text = unit.DisplayName;

        // 스탯 표시 형식: "최종값 (기본값 + 추가값)"
        // 지금은 장비가 없으니 bonus는 0으로 고정. 나중에 여기서 장비 스탯을 가져오면 됨.
        strText.text = GetStatString(unit.baseSTR, 0);
        magText.text = GetStatString(unit.baseCLV, 0);
        agiText.text = GetStatString(unit.baseAGI, 0);
        bdyText.text = GetStatString(unit.baseBDY, 0);
        mndText.text = GetStatString(unit.baseMND, 0);
        insText.text = GetStatString(unit.baseINS, 0);

        // 소질(Passive) 리스트 갱신
        RefreshPassives(unit);
        // 성격(Trait) 리스트 갱신
        RefreshTraits(unit);
    }

    // 포맷팅 헬퍼 함수
    private string GetStatString(int baseVal, int bonusVal)
    {
        int total = baseVal + bonusVal;
        // 예: 50 (50 + 0)
        return $"{total} ({baseVal} + {bonusVal})";
    }

    private void RefreshPassives(UnitData unit)
    {
        // 기존 슬롯 싹 다 지우기 (초기화)
        foreach (Transform child in passiveListRoot)
        {
            if (child.name == "Text_Title") continue;

            Destroy(child.gameObject);
        }

        // 유닛 데이터에 있는 패시브 순회하며 생성
        if (unit.passives != null)
        {
            foreach (var passive in unit.passives)
            {
                if (passive == null) continue;

                // 프리팹 생성
                GameObject go = Instantiate(passiveSlotPrefab, passiveListRoot);
                // 데이터 주입
                CampPassiveSlot slot = go.GetComponent<CampPassiveSlot>();

                if (slot != null)
                {
                    slot.Setup(passive, OnItemSelected);
                }
            }
        }
    }
    // 유대/성격 갱신 로직
    private void RefreshTraits(UnitData unit)
    {
        foreach (Transform child in traitListRoot)
        {
            if (child.name == "Trait_Header") continue; // 헤더는 살림
            Destroy(child.gameObject);
        }

        // 게이지 갱신
        if (bondSlider != null)
        {
            bondSlider.value = unit.currentBond;
        }

        // 성격 슬롯 생성 (조건부 해금)
        if (unit.traits != null)
        {
            // 해금 컷라인: 0번->10, 1번->30, 2번->60
            int[] unlockThresholds = { 10, 30, 60 };

            for (int i = 0; i < unit.traits.Length; i++)
            {
                // 데이터 없거나 인덱스 초과면 패스
                if (unit.traits[i] == null) continue;
                if (i >= unlockThresholds.Length) break;

                // 조건: 현재 유대 수치가 해금 컷라인 이상이어야 함
                if (unit.currentBond >= unlockThresholds[i])
                {
                    GameObject go = Instantiate(traitSlotPrefab, traitListRoot);
                    CampTraitSlot slot = go.GetComponent<CampTraitSlot>();
                    if (slot != null)
                    {
                        bool isActive = (unit.activeTrait == unit.traits[i]);
                        // Setup 호출 (OnTraitEquip 콜백 추가)
                        slot.Setup(
                            unit.traits[i],
                            isActive,
                            OnItemSelected,
                            (selectedTrait) => OnTraitEquip(selectedTrait)
                        );
                    }
                }
                // 조건 불만족 시 아무것도 안 만듦 (Hidden)
            }
        }
    }

    // 성격이 클릭(장착)되었을 때 실행
    private void OnTraitEquip(TraitAsset newTrait)
    {
        UnitData unit = CampUIManager.Instance.selectedUnit;
        if (unit == null) return;

        // 데이터 변경
        if (unit.activeTrait != newTrait)
        {
            unit.activeTrait = newTrait;
            Debug.Log($"성격 변경: {newTrait.displayName}");

            // UI 전체 갱신 (색상 다시 칠하기 위해)
            RefreshTraits(unit);
        }
    }

    // 아이템 선택 시 실행되는 함수
    private void OnItemSelected(string title, string desc, Transform targetTransform)
    {
        // 설명 텍스트 갱신
        if (descriptionTitle != null && descriptionText != null)
        {
            descriptionTitle.text = title;
            descriptionText.text = desc;
        }

        // 화살표 위치 이동 및 활성화
        if (selectionArrow != null && targetTransform != null)
        {
            selectionArrow.gameObject.SetActive(true);

            // 타겟의 RectTransform 가져오기
            RectTransform targetRect = targetTransform.GetComponent<RectTransform>();

            if (targetRect != null)
            {
                // 월드 좌표계 기준의 네 모서리 위치를 가져옴
                // corners[0]: 좌하단, [1]: 좌상단, [2]: 우상단, [3]: 우하단
                Vector3[] corners = new Vector3[4];
                targetRect.GetWorldCorners(corners);

                // 왼쪽 변의 중심점 계산 ((좌하단 + 좌상단) / 2)
                Vector3 leftEdgeCenter = (corners[0] + corners[1]) / 2f;

                // 화살표를 그 위치로 이동 + X축 오프셋 (arrowOffset.x 만큼 왼쪽으로)
                // 주의: 화살표의 pivot이 (1, 0.5) 즉 오른쪽 중앙이어야 자연스러움.
                // 일단 월드 좌표 기준으로 바로 꽂아버림.

                // 설정한 arrowOffset.x가 -150f라면 여기서 + 해주면 됨.
                // (월드 좌표계이므로 방향만 맞으면 됨)
                Vector3 finalPos = leftEdgeCenter;
                finalPos.x += arrowOffset.x; // 오프셋 적용 (왼쪽으로 띄우기)

                selectionArrow.position = finalPos;

                // Y축 미세 조정이 필요하면 finalPos.y += arrowOffset.y; 추가
            }
            else
            {
                // RectTransform 없으면 그냥 기존 방식대로 중앙 기준
                selectionArrow.position = targetTransform.position;
            }
        }
    }
    private void ClearUI()
    {
        ResetSelection();

        nameText.text = "-";
        strText.text = "-";
        magText.text = "-";
        agiText.text = "-";
        bdyText.text = "-";
        mndText.text = "-";
        insText.text = "-";
    }
}