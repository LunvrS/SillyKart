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
        var tracker = FindRaceTracker(other);
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

    private static RaceTracker FindRaceTracker(Collider other)
    {
        var checkpointTarget = other.GetComponent<KartCheckpointTarget>();
        if (checkpointTarget != null && checkpointTarget.Tracker != null)
            return checkpointTarget.Tracker;

        if (other.attachedRigidbody != null)
        {
            checkpointTarget = other.attachedRigidbody.GetComponent<KartCheckpointTarget>();
            if (checkpointTarget != null && checkpointTarget.Tracker != null)
                return checkpointTarget.Tracker;
        }

        var tracker = other.GetComponentInParent<RaceTracker>();
        if (tracker != null)
            return tracker;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj != null)
            return netObj.GetComponent<RaceTracker>();

        Debug.Log($"[Checkpoint] No RaceTracker found for collider '{other.name}'.");
        return null;
    }
}

public class KartCheckpointTarget : MonoBehaviour
{
    public RaceTracker Tracker { get; private set; }

    public void Initialize(RaceTracker tracker)
    {
        Tracker = tracker;
    }
}
