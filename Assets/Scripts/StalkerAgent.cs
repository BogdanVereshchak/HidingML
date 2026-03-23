using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.InputSystem;

public class StalkerAgent : Agent
{
    [Header("Target References")]
    public Transform playerTransform;
    public Camera playerCamera;
    public MonoBehaviour playerScript; // Може бути RandomPatrol або PlayerController
    public Transform areaCenter; // Центр кімнати (TrainingArea)

    [Header("Stats")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 200f;
    
    private Rigidbody rb;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        // Фіксація, щоб не падав
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Auto-assign if missing
        if (areaCenter == null && transform.parent != null)
        {
            areaCenter = transform.parent;
        }

        if (playerTransform == null)
        {
            var found = GameObject.FindWithTag("Player");
            if (found != null) playerTransform = found.transform;
        }

        if (playerCamera == null)
        {
            if (Camera.main != null) playerCamera = Camera.main;
        }

        if (playerTransform == null)
            Debug.LogWarning($"{name}: playerTransform not assigned!");
        if (playerCamera == null)
            Debug.LogWarning($"{name}: playerCamera not assigned!");
        if (areaCenter == null)
            Debug.LogWarning($"{name}: areaCenter not assigned!");
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        // --- 1. СПАВН СТАЛКЕРА (РОЗУМНИЙ) ---
        bool validPositionFound = false;
        int attempts = 0;
        Vector3 centerPos = areaCenter != null ? areaCenter.position : Vector3.zero;
        
        while (!validPositionFound && attempts < 10)
        {
            // Рандомна позиція в радіусі 9м від центру
            Vector3 potentialPos = centerPos + (Random.insideUnitSphere * 9f);
            potentialPos.y = 0.5f; // Фіксована висота

            // Перевіряємо, чи не спавнимось ми всередині стіни (SphereCast)
            if (!Physics.CheckSphere(potentialPos, 0.4f, LayerMask.GetMask("Wall")))
            {
                transform.position = potentialPos;
                validPositionFound = true;
            }
            attempts++;
        }

        // Поворот: дивимось приблизно в центр, а не в стіну (тільки по Y осі!)
        if (areaCenter != null)
        {
            Vector3 lookDir = areaCenter.position - transform.position;
            lookDir.y = 0; // Ігноруємо висоту
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // --- 2. СПАВН ГРАВЦЯ ---
        if (playerScript != null)
        {
            // Спробуємо викликати метод ResetPosition через рефлексію
            var resetMethod = playerScript.GetType().GetMethod("ResetPosition");
            if (resetMethod != null)
            {
                resetMethod.Invoke(playerScript, null);
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerTransform == null)
        {
            // Provide default observations: 3 + 1 + 1 + 1 = 6 floats
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        // Вектор до гравця
        Vector3 toPlayer = playerTransform.position - transform.position;
        
        // 1. Напрямок до гравця (нормалізований) [3 floats]
        sensor.AddObservation(toPlayer.normalized);
        
        // 2. Відстань до гравця (нормалізована: ділимо на діагональ кімнати ~28м) [1 float]
        sensor.AddObservation(toPlayer.magnitude / 30f);
        
        // 3. Кут між поглядом гравця і сталкером (Чи дивиться він на мене?) [1 float]
        // 1.0 = дивиться прямо на мене, -1.0 = спиною до мене
        float dotProduct = Vector3.Dot(playerTransform.forward, -toPlayer.normalized);
        sensor.AddObservation(dotProduct);

        // 4. Чи є пряма видимість (Raycast) [1 float]
        bool isLineOfSight = CheckLineOfSight();
        sensor.AddObservation(isLineOfSight ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (playerTransform == null) return;

        // --- РУХ ---
        int moveAction = actions.DiscreteActions[0]; // 0=Wait, 1=Fwd, 2=Back
        int rotateAction = actions.DiscreteActions[1]; // 0=Wait, 1=Left, 2=Right

        // Поворот (ТІЛЬКИ ПО Y ОСІ!)
        float rotateDir = 0f;
        if (rotateAction == 1) rotateDir = -1f;
        if (rotateAction == 2) rotateDir = 1f;
        
        // Використовуємо Euler angles щоб гарантувати поворот тільки по Y
        float currentYRotation = transform.eulerAngles.y;
        float newYRotation = currentYRotation + (rotateDir * rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, newYRotation, 0);

        // Рух
        Vector3 moveDir = Vector3.zero;
        if (moveAction == 1) moveDir = transform.forward;
        if (moveAction == 2) moveDir = -transform.forward;

        // Застосування швидкості
        Vector3 targetVelocity = moveDir * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; // Зберігаємо гравітацію
        rb.linearVelocity = targetVelocity;

        // --- ЛОГІКА ВИНАГОРОД (REWARD SHAPING) ---
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool isSeen = IsVisibleToCamera(); // Чи ми в кадрі?
        
        // 1. EXISTENCE PENALTY (Маленький штраф, щоб діяв швидко)
        AddReward(-0.0005f);

        // 2. VELOCITY REWARD (Нагорода за наближення)
        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        float movingTowardsPlayer = Vector3.Dot(rb.linearVelocity.normalized, dirToPlayer);

        if (movingTowardsPlayer > 0)
        {
            // Якщо рухаємось до гравця - отримуємо бонус
            float stealthMultiplier = isSeen ? 0.1f : 1.0f; 
            AddReward(0.005f * movingTowardsPlayer * stealthMultiplier);
        }

        // 3. STEALTH LOGIC
        if (isSeen)
        {
            // Якщо нас бачать, але ми далеко -> штраф
            if (distanceToPlayer > 3f)
            {
                AddReward(-0.01f); // Тікай!
            }
        }
        else
        {
            if (distanceToPlayer < 8f)
            {
                AddReward(0.002f);
            }
        }

        if (distanceToPlayer < 1.5f)
        {
            AddReward(0.0001f);
        }

        if (transform.position.y < -1f)
        {
            SetReward(-1f);
            EndEpisode();
        }
    }

    // Допоміжні методи перевірки
    private bool CheckLineOfSight()
    {
        if (playerCamera == null || playerTransform == null) return false;

        Vector3 directionToStalker = transform.position - playerCamera.transform.position;
        float distanceToStalker = directionToStalker.magnitude;

        RaycastHit hit;
        // Використовуємо Raycast замість Linecast для кращого контролю
        if (Physics.Raycast(playerCamera.transform.position, directionToStalker.normalized, out hit, distanceToStalker + 0.5f))
        {
            // Перевіряємо, чи промінь влучив в сталкера (або його дітей/батьків)
            Transform hitTransform = hit.transform;
            
            // Перевірка: чи це сам сталкер?
            if (hitTransform == transform) return true;
            
            // Перевірка: чи це частина сталкера (дочірній об'єкт)?
            if (hitTransform.IsChildOf(transform)) return true;
            
            // Перевірка: чи сталкер є дочірнім об'єктом того, в що влучили?
            if (transform.IsChildOf(hitTransform)) return true;
        }
        
        return false;
    }

    private bool IsVisibleToCamera()
    {
        if (playerCamera == null || playerTransform == null) return false;

        // Спочатку перевіряємо, чи ми взагалі в секторі огляду камери
        Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
        
        // Розширюємо зону видимості трохи за межі екрану для кращої детекції
        if (viewPos.x >= -0.1f && viewPos.x <= 1.1f && 
            viewPos.y >= -0.1f && viewPos.y <= 1.1f && 
            viewPos.z > 0)
        {
            // Якщо так, перевіряємо стіни
            return CheckLineOfSight();
        }
        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.DiscreteActions;
        actions[0] = 0; 
        actions[1] = 0;

        Keyboard keyboard = Keyboard.current;
        
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) actions[0] = 1;
            if (keyboard.sKey.isPressed) actions[0] = 2;
            if (keyboard.aKey.isPressed) actions[1] = 1;
            if (keyboard.dKey.isPressed) actions[1] = 2;
        }
    }
}