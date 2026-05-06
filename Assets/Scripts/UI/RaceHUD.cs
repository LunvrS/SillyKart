// RaceHUD.cs
// Attach to a Canvas that lives on the local player's camera rig, OR
// place on any scene Canvas and let it find the local RaceTracker at runtime.

using TMPro;
using UnityEngine;

/// <summary>
/// Lightweight HUD that displays "Lap X/3" and a running race timer.
/// Reads from the local player's RaceTracker and RaceCountdown.
/// </summary>
public class RaceHUD : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text LapLabel;    // e.g. "Lap 2 / 3"
    public TMP_Text TimerLabel;  // e.g. "1:23.456"

    [Header("Config")]
    public int TotalLaps = 3;

    private RaceTracker _tracker;

    private void Start()
    {
        // Auto-find the local player's RaceTracker after spawn.
        // If your kart spawns after the HUD, poll in Update instead.
        FindLocalTracker();
    }

    private void Update()
    {
        if (_tracker == null)
        {
            FindLocalTracker();
            return;
        }

        // Lap label
        if (LapLabel != null)
            LapLabel.text = $"Lap {_tracker.CurrentLap} / {TotalLaps}";

        // Timer (only ticks once race is active)
        if (TimerLabel != null && RaceCountdown.RaceStarted)
        {
            float t = RaceCountdown.RaceElapsed;
            int minutes = (int)(t / 60f);
            float seconds = t % 60f;
            TimerLabel.text = $"{minutes}:{seconds:00.000}";
        }
    }

    private void FindLocalTracker()
    {
        // Works after the local player object is spawned.
        var trackers = FindObjectsByType<RaceTracker>(FindObjectsSortMode.None);
        foreach (var t in trackers)
        {
            if (t.IsOwner) { _tracker = t; break; }
        }
    }
}
