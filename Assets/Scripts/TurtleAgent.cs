using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System;
using Unity.VisualScripting;
using System.Collections;

public class TurtleAgent : Agent
{
    [SerializeField]private Transform _goal;
    [SerializeField]private Renderer _groundRenderer;
    [SerializeField]private float _moveSpeed = 1.5f;
    [SerializeField]private float _rotationSpeed = 180f;

    private Renderer _renderer;

    [HideInInspector]public int CurrentEpisode = 0;
    [HideInInspector]public float CumulativeReward = 0f;

    private Color _defaultGroundColor;
    private Coroutine _flashGroundCoroutine;

    public override void Initialize()
    {
        _renderer = GetComponent<Renderer>();
        CumulativeReward = 0f;
        CurrentEpisode = 0;

        if (_groundRenderer != null)
        {
            _defaultGroundColor = _groundRenderer.material.color;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (_groundRenderer != null && CumulativeReward != 0f)
        {
            Color flashColor = CumulativeReward > 0f ? Color.green : Color.red;

            if (_flashGroundCoroutine != null)
            {
                StopCoroutine(_flashGroundCoroutine);
            }
            _flashGroundCoroutine = StartCoroutine(FlashGround(flashColor, 3.0f));
        }
        CurrentEpisode++;
        CumulativeReward = 0f;
        _renderer.material.color = Color.blue;

        SpawnObjects();
    }

    private IEnumerator FlashGround(Color flashColor, float duration)
    {
        float elapsedTime = 0f;

        _groundRenderer.material.color = flashColor;
        while (elapsedTime < duration)
        {
            _groundRenderer.material.color = Color.Lerp(flashColor, _defaultGroundColor, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private void SpawnObjects()
    {
        transform.localRotation = Quaternion.identity;
        transform.localPosition = new Vector3(0f,0.15f,0f);

        float randomAngle = UnityEngine.Random.Range(0f, 360f);
        Vector3 randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
        float randomDistance = UnityEngine.Random.Range(1f, 4.5f);
        Vector3 goalPosition = transform.localPosition + randomDirection * randomDistance;
        _goal.localPosition = new Vector3(goalPosition.x, 0.3f, goalPosition.z);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float goalPosX_normalized = _goal.localPosition.x / 5f;
        float goalPosZ_normalized = _goal.localPosition.z / 5f;
        
        float turtlePosX_normalized = transform.localPosition.x / 5f;
        float turtlePosZ_normalized = transform.localPosition.z / 5f;

        float turtleRotation_normalized = (transform.localRotation.eulerAngles.y / 360f)*2-1;
        sensor.AddObservation(goalPosX_normalized);
        sensor.AddObservation(goalPosZ_normalized);
        sensor.AddObservation(turtlePosX_normalized);
        sensor.AddObservation(turtlePosZ_normalized);
        sensor.AddObservation(turtleRotation_normalized);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0;

        Keyboard keyboard = Keyboard.current;
        
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed)
            {
                discreteActionsOut[0] = 1;
            }
            else if (keyboard.dKey.isPressed)
            {
                discreteActionsOut[0] = 2;
            }
            else if (keyboard.aKey.isPressed)
            {
                discreteActionsOut[0] = 3;
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        MoveAgent(actions.DiscreteActions);
        AddReward(-2f/MaxStep);
        CumulativeReward = GetCumulativeReward();
    }
    public void MoveAgent(ActionSegment<int> discreteActions)
    {
        int action = discreteActions[0];
        

        switch (action)
        {
            case 1:
                transform.position += transform.forward * _moveSpeed * Time.deltaTime;
                break;
            case 2:
                transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f);
                break;
            case 3:
                transform.Rotate(0f, -_rotationSpeed * Time.deltaTime, 0f);
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Goal"))
        {
            GoalReached();
        }
    }

    private void GoalReached()
    {
        AddReward(1.0f);
        CumulativeReward = GetCumulativeReward();
        EndEpisode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.05f);
            if (_renderer != null)
            {
                _renderer.material.color = Color.red;
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            if (_renderer != null)
            {
                _renderer.material.color = Color.blue;
            }
        }
    }
}