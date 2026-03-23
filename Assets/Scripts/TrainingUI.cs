using UnityEngine;
using TMPro; 

public class TrainingUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI statsText;

    [Header("Target Agent")]
    public HideAndSeekAgent agentToTrack; 

    void Update()
    {
        if (agentToTrack != null)
        {
            float reward = agentToTrack.GetCumulativeReward();
            int step = agentToTrack.StepCount;
            int episode = agentToTrack.CompletedEpisodes;

            statsText.text = $"Episode: {episode}\n" +
                             $"Step: {step}\n" +
                             $"Reward: {reward:F5}"; 
        }
    }
}