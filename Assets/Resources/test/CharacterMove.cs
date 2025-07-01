using System.Collections.Generic;
using UnityEngine;

public class CharacterMove : MonoBehaviour
{
    public float moveSpeed = 3f;
    private Vector2 movementInput;
    private Vector3 targetPosition;
    bool moveCheck = true;
    bool isMoving = false;

    void Update()
    {
        if (moveCheck && !isMoving)
        {
            movementInput.x = Input.GetAxisRaw("Horizontal");
            movementInput.y = Input.GetAxisRaw("Vertical");
            movementInput.Normalize();
            transform.position += (Vector3)movementInput * moveSpeed * Time.deltaTime;
        }

        if (Input.GetMouseButton(1))
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);
            if (hit.collider != null)
            {
                targetPosition = new Vector3(hit.point.x, hit.point.y, 0);
                isMoving = true;
            }
        }

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                isMoving = false;
                Debug.Log("목표지점까지 이동 완료");
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision != null && collision.gameObject.CompareTag("tile"))
        {
            moveCheck = true;
            Debug.Log("Tile Here");
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision != null && collision.gameObject.CompareTag("tile"))
        {
            moveCheck = false;
            Debug.Log("Tile Out");
        }
    }
}
