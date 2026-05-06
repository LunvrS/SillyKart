// RaceSpawnManager.cs
// Attach to the NetworkManager GameObject (or any persistent server-side object).
// Assign your player prefab and spawn points in the Inspector.

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Listens for client connections and spawns each player at a unique
/// spawn point based on connection order / OwnerClientId.
/// </summary>
public class RaceSpawnManager : NetworkBehaviour
{
    [Header("Spawn Configuration")]
    [Tooltip("The NetworkObject prefab to spawn for each player.")]
    public GameObject PlayerPrefab;

    [Tooltip("Drag your spawn-point Transforms here in grid order (P1, P2, ...).")]
    public Transform[] SpawnPoints;

    // Track which client maps to which spawn index.
    private readonly Dictionary<ulong, int> _clientSpawnIndex = new();
    private int _nextSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        int spawnIdx = _nextSpawnIndex % SpawnPoints.Length;
        _clientSpawnIndex[clientId] = spawnIdx;
        _nextSpawnIndex++;

        Transform spawnPoint = SpawnPoints[spawnIdx];

        GameObject instance = Instantiate(PlayerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[RaceSpawnManager] PlayerPrefab is missing a NetworkObject component!");
            Destroy(instance);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        Debug.Log($"[RaceSpawnManager] Spawned client {clientId} at spawn point {spawnIdx}.");
    }

    /// <summary>Call this to teleport a specific client back to their spawn (e.g. on race restart).</summary>
    public void RespawnClient(ulong clientId)
    {
        if (!IsServer) return;
        if (!_clientSpawnIndex.TryGetValue(clientId, out int idx)) return;

        // Find that client's player object and move it.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Transform sp = SpawnPoints[idx];
                client.PlayerObject.transform.SetPositionAndRotation(sp.position, sp.rotation);
            }
        }
    }
}
