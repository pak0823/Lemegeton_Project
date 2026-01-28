using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Recipe", menuName = "Data/CraftRecipe")]
public class CraftRecipe : ScriptableObject
{
    [Header("레시피 ID (해금 저장용)")]
    public string recipeID;         // 예: "RECIPE_WOOD_SWORD"

    [Header("제작 결과물")]
    public ItemData resultItem;     // 만들어질 아이템
    public int resultCount = 1;     // 몇 개 만들어지는지

    [Header("필요 재료 목록")]
    public List<Ingredient> ingredients;

    // 재료 구조체 (내부 클래스로 정의)
    [System.Serializable]
    public struct Ingredient
    {
        public ItemData material;   // 재료 아이템 데이터
        public int requiredCount;   // 필요 개수
    }
}