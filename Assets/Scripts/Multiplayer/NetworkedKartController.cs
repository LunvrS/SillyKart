// NetworkedKartController.cs
// Drop this onto your kart prefab alongside your existing kart script.
// Wraps owner-only input, syncs driftPower + driftMode to all clients.

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Add this component to your kart prefab.
/// Reference your existing KartController here so we can gate its Update
/// and broadcast drift state via NetworkVariables.
/// </summary>
public class NetworkedKartController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Drag your existing KartController script reference here, or merge the logic below.")]
    public MonoBehaviour kartController; // swap for your concrete type if preferred

    // ── Network Variables ──────────────────────────────────────────────────
    // Written by the OWNER, read by everyone (default permission).
    private readonly NetworkVariable<float> _driftPower = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<int> _driftMode = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // ── Cached local copies (owner drives these from its own KartController) ─
    // Expose setters so your KartController can push values each frame.
    public float DriftPower
    {
        get => _driftPower.Value;
        set { if (IsOwner) _driftPower.Value = value; }
    }

    /// <summary>0 = none, 1 = left, 2 = right (match your enum/int)</summary>
    public int DriftMode
    {
        get => _driftMode.Value;
        set { if (IsOwner) _driftMode.Value = value; }
    }

    // ── Spark / VFX references ─────────────────────────────────────────────
    [Header("Drift VFX")]
    public ParticleSystem leftSparks;
    public ParticleSystem rightSparks;

    public override void OnNetworkSpawn()
    {
        // Non-owners subscribe to changes so their sparks mirror the owner.
        if (!IsOwner)
        {
            _driftPower.OnValueChanged += OnDriftPowerChanged;
            _driftMode.OnValueChanged  += OnDriftModeChanged;
        }

        // Disable input-driven components on non-owners so they don't fight
        // the transform sync coming over the network.
        if (!IsOwner && kartController != null)
            kartController.enabled = false;
    }

    public override void OnNetworkDespawn()
    {
        _driftPower.OnValueChanged -= OnDriftPowerChanged;
        _driftMode.OnValueChanged  -= OnDriftModeChanged;
    }

    private void Update()
    {
        // ── OWNER ONLY: your normal input + physics lives here ──────────────
        // Replace the comment below with a call to your existing Update logic,
        // e.g.  base.Update(); or  _kartLogic.Tick();
        // After updating, push the current drift state into the NetworkVariables:
        //
        //   DriftPower = myKart.driftPower;
        //   DriftMode  = (int)myKart.driftMode;
        //
        if (!IsOwner) return;

        // TODO: call your existing kart input / physics here.
        // Then push values:
        // DriftPower = <your_drift_power_field>;
        // DriftMode  = (int)<your_drift_mode_enum>;
    }

    // ── Remote callbacks ───────────────────────────────────────────────────

    private void OnDriftPowerChanged(float prev, float next)
    {
        // Optionally scale spark emission rate by drift power.
        UpdateSparks(_driftMode.Value, next);
    }

    private void OnDriftModeChanged(int prev, int next)
    {
        UpdateSparks(next, _driftPower.Value);
    }

    private void UpdateSparks(int mode, float power)
    {
        // mode: 0=none, 1=left drift, 2=right drift
        SetSparks(leftSparks,  mode == 1, power);
        SetSparks(rightSparks, mode == 2, power);
    }

    private static void SetSparks(ParticleSystem ps, bool active, float power)
    {
        if (ps == null) return;
        if (active)
        {
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Lerp(10f, 80f, power); // tune as needed
            if (!ps.isPlaying) ps.Play();
        }
        else
        {
            if (ps.isPlaying) ps.Stop();
        }
    }

    // ── RPC alternative (uncomment if you prefer RPCs over NetworkVariables) ─
    // [ServerRpc]
    // private void SetDriftStateServerRpc(float power, int mode)
    // {
    //     SetDriftStateClientRpc(power, mode);
    // }
    //
    // [ClientRpc]
    // private void SetDriftStateClientRpc(float power, int mode)
    // {
    //     if (IsOwner) return; // owner already has correct state
    //     UpdateSparks(mode, power);
    // }
}
