using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalController : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string targetScene = "BattleScene";

    private bool playerInRange; //플레이어 감지

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log("playerInRange is True");
            // 화면에 "F: 포탈로 이동" 같은 UI 띄우기 추가 가능
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            Debug.Log("playerInRange is False");
            // UI 숨기기 추가 가능
        }
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            // 페이드 전환 호출
            Shared.SceneTransitionManager.FadeToScene(targetScene);
            Debug.Log("씬 전환함");
        }
    }
}
