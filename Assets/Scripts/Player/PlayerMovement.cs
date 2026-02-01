using FishNet.Object;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    public float speed = 2.0f;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 direction = new Vector2(x, y);

        if (direction == Vector2.zero)
            return;

        direction.Normalize();

        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

}
