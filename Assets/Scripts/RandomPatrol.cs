using UnityEngine;

public class RandomPatrol : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 120f;
    public Vector2 areaSize = new Vector2(9, 9); 
    public float changeTargetInterval = 3f; 

    private Vector3 targetPosition;
    private float timer;
    
    private Vector3 startAnchor;

    void Start()
    {
        startAnchor = transform.parent.position; 
        PickNewTarget();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            PickNewTarget();
            timer = changeTargetInterval;
        }

        MoveTowardsTarget();
        LookAround();
    }

    void PickNewTarget()
    {
        float randomX = Random.Range(-areaSize.x, areaSize.x);
        float randomZ = Random.Range(-areaSize.y, areaSize.y);

        targetPosition = startAnchor + new Vector3(randomX, 1f, randomZ);
    }

    void MoveTowardsTarget()
    {
        // Keep movement on the XZ plane to avoid accidental Y jitter
        Vector3 flatTarget = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);

        // Use MoveTowards to avoid overshoot and oscillation
        float step = moveSpeed * Time.deltaTime;
        Vector3 newPos = Vector3.MoveTowards(transform.position, flatTarget, step);
        transform.position = newPos;

        // Rotate only on the Y axis towards target (prevents tilt/jitter)
        Vector3 flatDirection = (flatTarget - transform.position);
        if (flatDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 lookDir = new Vector3(flatDirection.x, 0f, flatDirection.z).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void LookAround()
    {
        
    }
    

    public void ResetPosition()
    {
        float randomX = Random.Range(-areaSize.x + 2, areaSize.x - 2);
        float randomZ = Random.Range(-areaSize.y + 2, areaSize.y - 2);
        transform.position = startAnchor + new Vector3(randomX, 1.5f, randomZ);
        PickNewTarget();
    }
}