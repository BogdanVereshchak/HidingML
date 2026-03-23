using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 180f;
    
    [Header("Area Settings")]
    public Vector2 areaSize = new Vector2(9, 9);
    
    private Vector3 startAnchor;
    private Vector2 moveInput;

    void Start()
    {
        startAnchor = transform.parent != null ? transform.parent.position : Vector3.zero;
    }

    void Update()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        
        if (keyboard == null) return;

        // ОтримуємоInput
        moveInput = Vector2.zero;
        
        if (keyboard.wKey.isPressed) moveInput.y = 1f;
        if (keyboard.sKey.isPressed) moveInput.y = -1f;
        if (keyboard.aKey.isPressed) moveInput.x = -1f;
        if (keyboard.dKey.isPressed) moveInput.x = 1f;

        // Нормалізуємо для діагонального руху
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        // Рух вперед/назад
        Vector3 moveDir = transform.forward * moveInput.y;
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Поворот вліво/вправо
        if (moveInput.x != 0)
        {
            transform.Rotate(0, moveInput.x * rotationSpeed * Time.deltaTime, 0);
        }

        // Обмеження руху в межах арени (опціонально)
        ClampPositionToArea();
    }

    void ClampPositionToArea()
    {
        Vector3 localPos = transform.position - startAnchor;
        localPos.x = Mathf.Clamp(localPos.x, -areaSize.x, areaSize.x);
        localPos.z = Mathf.Clamp(localPos.z, -areaSize.y, areaSize.y);
        transform.position = startAnchor + localPos;
    }

    public void ResetPosition()
    {
        float randomX = Random.Range(-areaSize.x + 2, areaSize.x - 2);
        float randomZ = Random.Range(-areaSize.y + 2, areaSize.y - 2);
        transform.position = startAnchor + new Vector3(randomX, 1.5f, randomZ);
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
    }
}