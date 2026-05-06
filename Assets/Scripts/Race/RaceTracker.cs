// RaceTracker.cs
// Attach to every player kart (owner-only logic, reports to RaceManager).

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tracks checkpoints and laps for ONE player.
/// Only the owning client processes input; results are reported to the server
/// via ServerRpc so RaceManager (server-authoritative) can record them.
/// </summary>
public class RaceTracker : NetworkBehaviour
{
    // ── Config ─────────────────────────────────────────────────────────────
    [Header("Race Config")]
    [Tooltip("Total number of checkpoints on the track (excluding finish line).")]
    public int TotalCheckpoints = 6;

    [Tooltip("Number of laps to complete.")]
    public int TotalLaps = 3;

    // ── Runtime state ──────────────────────────────────────────────────────
    private int _nextExpectedCheckpoint = 0; // 0-based index of the NEXT gate to hit
    private int _currentLap = 1;
    private bool _raceFinished = false;

    // ── Public read-only accessors used by HUD ─────────────────────────────
    public int CurrentLap   => _currentLap;
    public bool RaceFinished => _raceFinished;

    // ── Called by Checkpoint.OnTriggerEnter ────────────────────────────────

    /// <summary>Called when a numbered gate is crossed.</summary>
    public void OnCheckpointHit(int index)
    {
        if (!IsOwner || _raceFinished) return;

        if (index != _nextExpectedCheckpoint)
        {
            Debug.Log($"[RaceTracker] Expected checkpoint {_nextExpectedCheckpoint}, got {index}. Ignoring.");
            return;
        }

        _nextExpectedCheckpoint++;
        Debug.Log($"[RaceTracker] Checkpoint {index} ✓  (next: {_nextExpectedCheckpoint})");
    }

    /// <summary>Called when the finish-line trigger is crossed.</summary>
    public void OnFinishLineCrossed()
    {
        if (!IsOwner || _raceFinished) return;

        // Anti-cheat: player must have hit ALL checkpoints this lap first.
        if (_nextExpectedCheckpoint < TotalCheckpoints)
        {
            Debug.Log($"[RaceTracker] Finish line hit but only {_nextExpectedCheckpoint}/{TotalCheckpoints} checkpoints done. Ignoring.");
            return;
        }

        // Valid lap completion.
        _nextExpectedCheckpoint = 0; // reset for next lap

        if (_currentLap >= TotalLaps)
        {
            _raceFinished = true;
            float finishTime = RaceCountdown.RaceElapsed; // see RaceCountdown.cs
            Debug.Log($"[RaceTracker] RACE COMPLETE! Time: {finishTime:F2}s");
            NotifyRaceFinishedServerRpc(finishTime);
        }
        else
        {
            _currentLap++;
            Debug.Log($"[RaceTracker] Lap {_currentLap}/{TotalLaps} started.");
            NotifyLapCompleteServerRpc(_currentLap - 1);
        }
    }

    // ── Server RPCs ────────────────────────────────────────────────────────

    [ServerRpc]
    private void NotifyLapCompleteServerRpc(int completedLap)
    {
        RaceManager.Instance?.OnPlayerLapComplete(OwnerClientId, completedLap);
    }

    [ServerRpc]
    private void NotifyRaceFinishedServerRpc(float time)
    {
        RaceManager.Instance?.OnPlayerFinished(OwnerClientId, time);
    }
}
