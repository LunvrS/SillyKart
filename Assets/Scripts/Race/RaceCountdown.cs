// RaceCountdown.cs
// Attach to a server-side manager object.
// Drives the "3-2-1-GO" sequence and broadcasts the start to all clients.

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Server-authoritative countdown.  Exposes a static RaceElapsed so
/// RaceTracker can read elapsed time without a direct reference.
/// </summary>
public class RaceCountdown : NetworkBehaviour
{
    [Header("UI")]
    [Tooltip("TextMeshPro label shown to every player during countdown.")]
    public TMP_Text CountdownLabel; // assign in Inspector (Screen Space – Overlay Canvas)

    [Header("Timing")]
    public float DelayBeforeCountdown = 1f;
    public float StepDuration = 1f; // seconds per number

    // ── Public state ───────────────────────────────────────────────────────
    public static float RaceElapsed { get; private set; } = 0f;
    public static bool  RaceStarted { get; private set; } = false;

    // Networked so late-joiners get the right countdown digit.
    private readonly NetworkVariable<int> _countdownValue = new NetworkVariable<int>(
        -1, // -1 = race not started
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _raceActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        _countdownValue.OnValueChanged += OnCountdownChanged;
        _raceActive.OnValueChanged      += OnRaceActiveChanged;

        if (IsServer)
            StartCoroutine(CountdownSequence());
    }

    public override void OnNetworkDespawn()
    {
        _countdownValue.OnValueChanged -= OnCountdownChanged;
        _raceActive.OnValueChanged      -= OnRaceActiveChanged;
    }

    private void Update()
    {
        if (RaceStarted)
            RaceElapsed += Time.deltaTime;
    }

    // ── Server coroutine ───────────────────────────────────────────────────

    private IEnumerator CountdownSequence()
    {
        yield return new WaitForSeconds(DelayBeforeCountdown);

        for (int i = 3; i >= 1; i--)
        {
            _countdownValue.Value = i;
            yield return new WaitForSeconds(StepDuration);
        }

        // GO!
        _countdownValue.Value = 0;
        _raceActive.Value = true;
        RaceElapsed = 0f;

        yield return new WaitForSeconds(StepDuration);
        _countdownValue.Value = -1; // hide label
    }

    // ── Client callbacks ───────────────────────────────────────────────────

    private void OnCountdownChanged(int prev, int next)
    {
        if (CountdownLabel == null) return;

        switch (next)
        {
            case -1: CountdownLabel.text = "";    CountdownLabel.gameObject.SetActive(false); break;
            case  0: CountdownLabel.text = "GO!"; CountdownLabel.gameObject.SetActive(true);  break;
            default: CountdownLabel.text = next.ToString(); CountdownLabel.gameObject.SetActive(true); break;
        }
    }

    private void OnRaceActiveChanged(bool prev, bool next)
    {
        RaceStarted = next;
        RaceElapsed = 0f;
        // Disable kart input until race starts — hook this event in your KartController:
        // RaceCountdown instance.OnRaceActiveChanged -> enable/disable input.
        Debug.Log(next ? "[RaceCountdown] Race STARTED." : "[RaceCountdown] Race inactive.");
    }
}
