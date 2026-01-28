using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; // 리스트 필터링용

public class CampCraftPage : MonoBehaviour
{
    [Header("Data Source")]
    public List<CraftRecipe> allDatabaseRecipes; // 게임의 모든 레시피를 여기에 등록 (Project 탭에서 드래그)

    [Header("Right List")]
    public Transform listContent;
    public GameObject recipeSlotPrefab; // 리스트에 뜰 슬롯 프리팹

    [Header("Left Detail")]
    public Image resultIcon;
    //public Text resultNameText;
    public Transform materialGrid;
    public GameObject materialIconPrefab; // 재료 아이콘 프리팹
    public Button craftButton;
    public Text warningText;

    [Header("Popup")]
    public CraftResultPopup resultPopup; // 팝업창

    private CraftRecipe currentSelectedRecipe;

    private void Start()
    {
        // 제작 버튼 클릭 시 TryCraft 실행 연결
        if (craftButton != null)
            craftButton.onClick.AddListener(TryCraft);
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
        currentSelectedRecipe = recipe;
        resultIcon.sprite = recipe.resultItem.itemIcon;
        resultIcon.gameObject.SetActive(true);
        //resultNameText.text = recipe.resultItem.itemName;

        // 재료 그리드 갱신
        foreach (Transform child in materialGrid) Destroy(child.gameObject);

        bool isCraftable = true;

        foreach (var ing in recipe.ingredients)
        {
            GameObject matObj = Instantiate(materialIconPrefab, materialGrid);

            MaterialSlotUI matUI = matObj.GetComponent<MaterialSlotUI>();

            // 임시 보유량 (나중에 인벤토리 연결)
            int myCount = 10;

            if (matUI != null)
            {
                matUI.Setup(ing.material, myCount, ing.requiredCount);
            }

            if (myCount < ing.requiredCount) isCraftable = false;
        }

        craftButton.interactable = isCraftable;
        warningText.text = isCraftable ? "제작 가능" : "재료가 부족합니다";
    }

    // 제작 실행 함수
    private void TryCraft()
    {
        if (currentSelectedRecipe == null) return;

        // 1. 인벤토리 소모 로직 (지금은 생략, 나중에 추가)
        // Inventory.Consume(currentSelectedRecipe.ingredients);
        // Inventory.Add(currentSelectedRecipe.resultItem);

        Debug.Log($"제작 성공: {currentSelectedRecipe.resultItem.itemName}");

        // 결과 팝업 띄우기
        if (resultPopup != null)
        {
            resultPopup.Show(currentSelectedRecipe.resultItem);
        }
    }

    private void ClearDetail()
    {
        resultIcon.gameObject.SetActive(false);
        //resultNameText.text = "";
        foreach (Transform child in materialGrid) Destroy(child.gameObject);
        craftButton.interactable = false;
        warningText.text = "레시피를 선택해주세요.";
    }
}