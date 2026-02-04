using System.Collections.Generic;
using System.Linq; // 리스트 필터링용
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class CampCraftPage : MonoBehaviour
{
    [Header("Data Source")]
    public List<CraftRecipe> allDatabaseRecipes; // 게임의 모든 레시피를 여기에 등록 (Project 탭에서 드래그)

    [Header("Left Detail")]
    public Transform listContent;
    public GameObject recipeSlotPrefab; // 리스트에 뜰 슬롯 프리팹

    [Header("Right List")]
    public Transform materialGrid;
    public GameObject materialIconPrefab; // 재료 아이콘 프리팹
    public Button craftButton;
    public Text warningText;

    [Header("UI - Panels")]
    public GameObject panelPreCraft;  // 준비 패널
    public GameObject panelPostCraft; // 결과 패널

    [Header("UI - Post Craft (Result)")]
    public Image resultDetailIcon;    // 결과창의 큰 아이콘
    public Button btnCollect;         // 모두 획득 버튼
    public Button btnDiscard;         // 포기 버튼
    public Text txtInvenFull; // 인벤토리 부족 경고

    [Header("Description")] // 설명창 관련 변수
    public Text txtSelectedName; // 하단 아이템 이름
    public Text txtSelectedDesc; // 하단 아이템 설명


    // 상태 관리용 변수
    private bool isResultState = false;

    private AsyncOperationHandle<Sprite> _handle;
    private CraftRecipe currentSelectedRecipe;

    private void Start()
    {
        if (craftButton != null) craftButton.onClick.AddListener(TryCraft);

        if (btnCollect != null) btnCollect.onClick.AddListener(OnClickCollect);
        if (btnDiscard != null) btnDiscard.onClick.AddListener(OnClickDiscard);

        SwitchState(false);
    }
    private void OnDestroy() 
    {
        if (_handle.IsValid()) Addressables.Release(_handle);
    }

    private void SwitchState(bool isResult)
    {
        isResultState = isResult;
        panelPreCraft.SetActive(!isResult);
        panelPostCraft.SetActive(isResult);

        // 결과창이 떴을 때는 상위 매니저에게 "닫지 마!" 신호 보내기 (옵션)
        // CampUIManager.Instance.SetCloseLock(isResult); 
    }

    // 헤더에서 호출하는 함수 (필터링)
    public void FilterRecipesByMaterial(ItemData material)
    {
        // 기존 리스트 클리어
        foreach (Transform child in listContent) Destroy(child.gameObject);

        // 해당 재료를 하나라도 포함하는 레시피만 찾기 (LINQ 사용)
        var filteredRecipes = allDatabaseRecipes.Where(r =>
            r.ingredients.Any(i => i.material == material)
        ).ToList();

        // 찾은 레시피들을 리스트에 생성
        foreach (var recipe in filteredRecipes)
        {
            GameObject slotObj = Instantiate(recipeSlotPrefab, listContent);

            CraftSlotUI slotUI = slotObj.GetComponent<CraftSlotUI>();

            if (slotUI != null)
            {
                // Setup 함수에 레시피랑, 클릭했을 때 할 행동(ShowDetail)을 넘겨줌
                slotUI.Setup(recipe, () => ShowDetail(recipe));
            }
        }

        // 목록이 있으면 첫 번째 놈 자동 선택
        if (filteredRecipes.Count > 0) ShowDetail(filteredRecipes[0]);
        else ClearDetail();
    }

    // 상세 정보 표시
    private void ShowDetail(CraftRecipe recipe)
    {
        // 결과 창이 떠있는 상태라면 다른 레시피 클릭을 막거나 무시해야 함
        if (isResultState) return;

        currentSelectedRecipe = recipe;
        SwitchState(false); // 준비 화면으로 전환

        // 재료 그리드 갱신
        foreach (Transform child in materialGrid) Destroy(child.gameObject);

        // 하단 텍스트 갱신 로직 -----------------------
        if (txtSelectedName != null)
            txtSelectedName.text = recipe.resultItem.itemName;

        if (txtSelectedDesc != null)
            txtSelectedDesc.text = recipe.resultItem.itemDescription;

        bool isCraftable = true;

        foreach (var ing in recipe.ingredients)
        {
            GameObject matObj = Instantiate(materialIconPrefab, materialGrid);

            MaterialSlotUI matUI = matObj.GetComponent<MaterialSlotUI>();

            // 임시 보유량 (나중에 인벤토리 연결)
            int myCount = InventoryManager.Instance.GetItemCount(ing.material.itemID); // 실시간 데이터 연동

            if (matUI != null)
            {
                matUI.Setup(ing.material, myCount, ing.requiredCount);
            }

            if (myCount < ing.requiredCount) isCraftable = false;
        }

        craftButton.interactable = isCraftable;
        warningText.text = isCraftable ? "" : "재료가 부족합니다";
    }

    // 제작 실행 함수
    private void TryCraft()
    {
        if (currentSelectedRecipe == null) return;

        // 재료 소모
        foreach (var ing in currentSelectedRecipe.ingredients)
        {
            InventoryManager.Instance.ConsumeItem(ing.material.itemID, ing.requiredCount);
        }

        // 결과 화면 세팅
        _handle = Addressables.LoadAssetAsync<Sprite>(currentSelectedRecipe.resultItem.GetAtlasKey());
        _handle.Completed += h => {
            if (h.Status == AsyncOperationStatus.Succeeded) resultDetailIcon.sprite = h.Result;
        };
        txtInvenFull.gameObject.SetActive(false); // 경고 끄기

        Debug.Log($"제작 성공: {currentSelectedRecipe.resultItem.itemName}");

        // 화면 전환 (준비 -> 결과)
        SwitchState(true);
    }

    private void OnClickCollect()
    {
        // 인벤토리 공간 체크 (가정)
        // if (!InventoryManager.Instance.HasSpace()) 
        // {
        //     txtInvenFull.gameObject.SetActive(true);
        //     return; 
        // }

        // 아이템 지급
        InventoryManager.Instance.AddItem(currentSelectedRecipe.resultItem.itemID, 1);

        // 다시 준비 화면으로 복귀
        SwitchState(false);

        // 재료가 소모됐으니 UI 갱신 (다시 그리기)
        ShowDetail(currentSelectedRecipe);
    }
    // [포기] 버튼 로직
    private void OnClickDiscard()
    {
        // 그냥 획득 안 하고 돌아감
        SwitchState(false);
        ShowDetail(currentSelectedRecipe);
    }

    private void ClearDetail()
    {
        foreach (Transform child in materialGrid) Destroy(child.gameObject);
        craftButton.interactable = false;
        warningText.text = "레시피를 선택해주세요.";

        if (txtSelectedName != null) txtSelectedName.text = "";
        if (txtSelectedDesc != null) txtSelectedDesc.text = "";
    }
}