using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.InputSystem; 

public enum AgentRole { Seeker, Hider }

public class HideAndSeekAgent : Agent
{
    [Header("Settings")]
    public AgentRole role; 
    public float moveSpeed = 5f;
    public float rotationSpeed = 200f;
    
    [Header("Game Balance")]
    [Tooltip("Множник швидкості")]
    public float hiderSpeedMultiplier = 1.2f;

    [Header("References")]
    public Transform myOpponent;
    public Transform areaCenter;
    public float areaRadius = 9f;

    private Rigidbody rb;
    private float bufferTime = 2.0f; 

    private float m_EpisodeStartTime;
    private int m_TotalFrames;
    private int m_VisibleFrames;
    private float m_CumulativeDistance;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; 
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;

        rb.angularVelocity = Vector3.zero;

        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

        MoveToSafeRandomPosition();
        bufferTime = 2.0f;
        m_EpisodeStartTime = Time.time;
        m_TotalFrames = 0;
        m_VisibleFrames = 0;
        m_CumulativeDistance = 0f;
        
    }

    void MoveToSafeRandomPosition()
    {
        bool safe = false;
        int attempts = 0;
        float spawnRadius = Mathf.Max(1f, areaRadius - 1f);
        while (!safe && attempts < 100)
        {
            Vector3 randomPos = Random.insideUnitSphere * spawnRadius;
            randomPos.y = 0.5f;
            Vector3 spawnPos = areaCenter.position + randomPos;

            if (!Physics.CheckSphere(spawnPos, 0.5f, LayerMask.GetMask("Wall")))
            {
                float distance = Vector3.Distance(spawnPos, myOpponent.position);
                if (distance > areaRadius/2)
                {
                    if (!Physics.Linecast(spawnPos, myOpponent.position, out RaycastHit hit) || hit.transform != myOpponent)
                    {
                        transform.position = spawnPos;
                        transform.LookAt(areaCenter);
                        safe = true;
                    }
                }
            }
            attempts++;
        }
        if (!safe)
        {
             transform.position = areaCenter.position + (Random.insideUnitSphere * spawnRadius);
             transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
        }
    }

    public void FixedUpdate()
    {
        bufferTime -= Time.fixedDeltaTime;
        rb.angularVelocity = Vector3.zero; 

        Vector3 currentRot = transform.rotation.eulerAngles;
        if (Mathf.Abs(NormalizeAngle(currentRot.x)) > 0.1f || Mathf.Abs(NormalizeAngle(currentRot.z)) > 0.1f)
        {
            transform.rotation = Quaternion.Euler(0, currentRot.y, 0);
        }

        m_TotalFrames++;
        float currentDist = Vector3.Distance(transform.position, myOpponent.position);
        m_CumulativeDistance += currentDist;
        
        bool isVisible = false;
        if (role == AgentRole.Seeker) isVisible = CanSeeOpponent();
        else isVisible = myOpponent.GetComponent<HideAndSeekAgent>().CanSeeOpponent();

        if (isVisible) m_VisibleFrames++;

        if (role == AgentRole.Hider)
        {
            AddReward(0.2f / MaxStep);
            
            if (!isVisible) AddReward(0.5f / MaxStep);
            if (currentDist > areaRadius / 2)
            {
                AddReward(0.05f / MaxStep);
            }
        }
        else
        {
            AddReward(-0.05f / MaxStep); 
            
            if (isVisible) AddReward(0.3f / MaxStep); 
            AddReward((areaRadius - currentDist) / (areaRadius * MaxStep) * 0.1f);
        }
        
        if (StepCount >= MaxStep-1 && role == AgentRole.Hider)
        {
            RecordGameStats(seekerWon: false);
            EndEpisode();
        }
    }

    void RecordGameStats(bool seekerWon)
    {

        Academy.Instance.StatsRecorder.Add("Game/SeekerWinRate", seekerWon ? 1.0f : 0.0f);

        float avgDist = m_TotalFrames > 0 ? m_CumulativeDistance / m_TotalFrames : 0;
        Academy.Instance.StatsRecorder.Add("Game/AvgDistance", avgDist);

        float visRatio = m_TotalFrames > 0 ? (float)m_VisibleFrames / m_TotalFrames : 0;
        Academy.Instance.StatsRecorder.Add("Game/VisibilityRatio", visRatio);

        Academy.Instance.StatsRecorder.Add("Game/EpisodeDuration", Time.time - m_EpisodeStartTime);
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toOpponent = myOpponent.position - transform.position;
        float maxDistance = areaRadius * 2f; // Diameter of play area

        sensor.AddObservation(toOpponent.normalized);
        sensor.AddObservation(Mathf.Clamp01(toOpponent.magnitude / maxDistance));
        sensor.AddObservation(role == AgentRole.Hider ? 1f : 0f);
        sensor.AddObservation(CanSeeOpponent() ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAct = actions.DiscreteActions[0]; 
        int rotAct = actions.DiscreteActions[1];  

        float rotateDir = 0f;
        if (rotAct == 1) rotateDir = -1f;
        if (rotAct == 2) rotateDir = 1f;
        
        if (rotateDir != 0)
        {
            transform.Rotate(0, rotateDir * rotationSpeed * Time.fixedDeltaTime, 0, Space.World);
        }

        Vector3 moveDir = Vector3.zero;
        if (moveAct == 1) moveDir = transform.forward;
        if (moveAct == 2) moveDir = -transform.forward;

        float finalSpeed = moveSpeed;
        if (role == AgentRole.Hider)
        {
            finalSpeed *= hiderSpeedMultiplier;
        }

        Vector3 targetVel = moveDir * finalSpeed;
        targetVel.y = rb.linearVelocity.y;
        rb.linearVelocity = targetVel;

    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0; 
        discreteActions[1] = 0;

        if (Keyboard.current == null) return;

        if (role == AgentRole.Hider)
        {
            if (Keyboard.current.wKey.isPressed) discreteActions[0] = 1;
            if (Keyboard.current.sKey.isPressed) discreteActions[0] = 2;
            if (Keyboard.current.aKey.isPressed) discreteActions[1] = 1;
            if (Keyboard.current.dKey.isPressed) discreteActions[1] = 2;
        }
        else if (role == AgentRole.Seeker)
        {
            if (Keyboard.current.upArrowKey.isPressed) discreteActions[0] = 1;
            if (Keyboard.current.downArrowKey.isPressed) discreteActions[0] = 2;
            if (Keyboard.current.leftArrowKey.isPressed) discreteActions[1] = 1;
            if (Keyboard.current.rightArrowKey.isPressed) discreteActions[1] = 2;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (bufferTime > 0) return; 

        if (collision.gameObject.transform == myOpponent)
        {
            if (role == AgentRole.Seeker)
            {
                AddReward(1.0f);
                RecordGameStats(seekerWon: true);
                EndEpisode();
                
                var opponentAgent = myOpponent.GetComponent<Agent>();
                if (opponentAgent != null)
                {
                    opponentAgent.AddReward(-1.0f);
                    opponentAgent.EndEpisode();
                }
            }
        }
    }

    public bool CanSeeOpponent()
    {
        Vector3 directionToOpponent = myOpponent.position - transform.position;
        float distanceToOpponent = directionToOpponent.magnitude;
        
        float visionRange = 15f;
        if (distanceToOpponent > visionRange)
            return false;
        
        float visionAngle = 60f;
        float angleToOpponent = Vector3.Angle(transform.forward, directionToOpponent);
        if (angleToOpponent > visionAngle / 2f)
            return false;
        
        RaycastHit hit;
        if (Physics.Linecast(transform.position, myOpponent.position, out hit))
        {
            if (hit.transform == myOpponent)
                return true;
            return false;
        }
        return true;
    }
}