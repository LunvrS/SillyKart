// RaceSpawnManager.cs
// Attach to the NetworkManager GameObject (or any persistent server-side object).
// Assign your player prefab and spawn points in the Inspector.

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private readonly HashSet<ulong> _spawnedClients = new();
    private int _nextSpawnIndex = 0;
    private bool _sceneLoadComplete = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (PlayerPrefab == null)
        {
            Debug.LogError("[RaceSpawnManager] PlayerPrefab is not assigned.");
            return;
        }

        if (SpawnPoints == null || SpawnPoints.Length == 0)
        {
            Debug.LogError("[RaceSpawnManager] No spawn points assigned.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

        Debug.Log($"[RaceSpawnManager] Ready. Waiting for scene load completion. Connected clients: {NetworkManager.Singleton.ConnectedClientsList.Count}.");
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (!_sceneLoadComplete)
        {
            Debug.Log($"[RaceSpawnManager] Client {clientId} connected. Waiting for GameScene load before spawning.");
            return;
        }

        SpawnClientIfNeeded(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _clientSpawnIndex.Remove(clientId);
        _spawnedClients.Remove(clientId);
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        if (sceneName != gameObject.scene.name) return;

        _sceneLoadComplete = true;
        Debug.Log($"[RaceSpawnManager] Scene load completed for {clientsCompleted.Count} client(s). Spawning players.");

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            SpawnClientIfNeeded(client.ClientId);
        }

        foreach (ulong clientId in clientsTimedOut)
        {
            Debug.LogWarning($"[RaceSpawnManager] Client {clientId} timed out loading {sceneName}; not spawning yet.");
        }
    }

    private void SpawnClientIfNeeded(ulong clientId)
    {
        if (_spawnedClients.Contains(clientId))
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return;

        if (client.PlayerObject != null)
        {
            _spawnedClients.Add(clientId);
            Debug.Log($"[RaceSpawnManager] Client {clientId} already has player object '{client.PlayerObject.name}'.");
            return;
        }

        int spawnIdx = GetSpawnIndex(clientId);
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
        _spawnedClients.Add(clientId);
        Debug.Log($"[RaceSpawnManager] Spawned client {clientId} at spawn point {spawnIdx}.");
    }

    private int GetSpawnIndex(ulong clientId)
    {
        if (_clientSpawnIndex.TryGetValue(clientId, out int existingIndex))
            return existingIndex;

        int spawnIdx = _nextSpawnIndex % SpawnPoints.Length;
        _clientSpawnIndex[clientId] = spawnIdx;
        _nextSpawnIndex++;
        return spawnIdx;
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
