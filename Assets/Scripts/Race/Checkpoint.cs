// Checkpoint.cs
// Attach to a GameObject with a Trigger Collider.
// Tag the collider with "Player" or use layer filtering.

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Place one of these on every gate around the track.
/// Set CheckpointIndex to 0, 1, 2 ... N-1 in the Inspector.
/// The finish line should be index 0 with IsFinishLine = true,
/// OR use a separate FinishLine collider that calls RaceTracker directly.
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Sequential index of this checkpoint (0-based). Finish line = 0 with IsFinishLine checked).")]
    public int CheckpointIndex = 0;

    [Tooltip("Tick this only for the start/finish line collider.")]
    public bool IsFinishLine = false;

    private void OnTriggerEnter(Collider other)
    {
        // Accept any collider whose root has a RaceTracker-capable component.
        // Works for both local and networked players.
        //Debug.Log($"Hit collider {other.gameObject}");

        var netObj = other.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.Log("No NetworkObject found");
            return;
        }

        var tracker = netObj.GetComponent<RaceTracker>();
        Debug.Log($"Tracker: {tracker}");
        if (tracker == null) return;

        if (IsFinishLine) 
        {
            Debug.Log($"Is finish {IsFinishLine}");
            tracker.OnFinishLineCrossed();
        }
        else
        {
            Debug.Log($"Hit point : {CheckpointIndex}");
            tracker.OnCheckpointHit(CheckpointIndex);
        }
    }
}
