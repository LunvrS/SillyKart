// RaceManager.cs
// Single server-side authority that collects finish times and triggers
// the end-of-race leaderboard UI on all clients.

using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct FinishEntry
{
    public ulong ClientId;
    public float Time;
    public int   Position; // 1st, 2nd, 3rd ...
}

/// <summary>
/// Singleton server manager.  Receives ServerRpc calls from RaceTracker,
/// then broadcasts results to all clients via ClientRpc.
/// Also owns the leaderboard / end-screen UI activation.
/// </summary>
public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Race Config")]
    public int ExpectedPlayerCount = 2; // set before the race starts

    [Header("End Screen UI (assign on every client via scene reference)")]
    public GameObject EndScreenPanel;       // the overlay panel
    public TMP_Text   LeaderboardText;      // multi-line results
    public Button     ReturnToMenuButton;   // calls NetworkManager.Shutdown

    // ── Runtime ────────────────────────────────────────────────────────────
    private readonly List<FinishEntry> _finishers = new();
    private bool _raceOver = false;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (EndScreenPanel != null)
            EndScreenPanel.SetActive(false);

        if (ReturnToMenuButton != null)
            ReturnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
    }

    // ── Called by RaceTracker via ServerRpc ────────────────────────────────

    public void OnPlayerLapComplete(ulong clientId, int completedLap)
    {
        if (!IsServer) return;
        Debug.Log($"[RaceManager] Client {clientId} completed lap {completedLap}.");
        // Optionally broadcast to all clients for a "lap complete" notification.
    }

    public void OnPlayerFinished(ulong clientId, float time)
    {
        if (!IsServer || _raceOver) return;

        // Prevent duplicate entries.
        if (_finishers.Any(f => f.ClientId == clientId)) return;

        _finishers.Add(new FinishEntry
        {
            ClientId = clientId,
            Time     = time,
            Position = _finishers.Count + 1
        });

        Debug.Log($"[RaceManager] Client {clientId} finished P{_finishers.Count} in {time:F2}s");

        if (_finishers.Count >= ExpectedPlayerCount)
        {
            _raceOver = true;
            BroadcastResultsClientRpc(BuildResultsString());
        }
    }

    // ── Server helper ──────────────────────────────────────────────────────

    private string BuildResultsString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== RACE RESULTS ===");
        foreach (var entry in _finishers.OrderBy(e => e.Position))
        {
            int m = (int)(entry.Time / 60f);
            float s = entry.Time % 60f;
            sb.AppendLine($"P{entry.Position}  Client {entry.ClientId}   {m}:{s:00.00}");
        }
        return sb.ToString();
    }

    // ── ClientRpc: runs on every connected client ──────────────────────────

    [ClientRpc]
    private void BroadcastResultsClientRpc(string resultsText)
    {
        ShowEndScreen(resultsText);
    }

    private void ShowEndScreen(string text)
    {
        if (EndScreenPanel != null)
            EndScreenPanel.SetActive(true);

        if (LeaderboardText != null)
            LeaderboardText.text = text;

        Debug.Log("[RaceManager] End screen shown:\n" + text);
    }

    // ── Return to Menu ─────────────────────────────────────────────────────

    public void OnReturnToMenuClicked()
    {
        // Gracefully shut down networking for this client (or host).
        NetworkManager.Singleton.Shutdown();

        // Load your main menu scene after shutdown.
         UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }
}
