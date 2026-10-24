using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    public void Move(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0f)
            return;

        Vector2 moveDir = moveInput.normalized;
        Vector2 nextPos = rb.position + moveDir * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPos);
    }
}
