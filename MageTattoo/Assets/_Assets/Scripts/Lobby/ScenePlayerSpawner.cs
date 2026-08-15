using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScenePlayerSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;

    private NetworkManager networkManager;

    private readonly HashSet<int> spawnedClientIds = new();

    private int nextSpawnIndex;
    private bool sceneEventsSubscribed;

    private void OnEnable()
    {
        TryInitialize();
    }

    private void Start()
    {
        TryInitialize();
        StartCoroutine(TrySpawnExistingConnections());
    }

    private void OnDisable()
    {
        UnsubscribeFromSceneEvents();
    }

    private void TryInitialize()
    {
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (networkManager == null)
        {
            Debug.LogError(
                "[ScenePlayerSpawner] NetworkManager was not found."
            );

            return;
        }

        SubscribeToSceneEvents();
    }

    private void SubscribeToSceneEvents()
    {
        if (sceneEventsSubscribed || networkManager?.SceneManager == null)
            return;

        networkManager.SceneManager.OnClientPresenceChangeEnd += HandleClientPresenceChangeEnd;
        networkManager.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;

        sceneEventsSubscribed = true;
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (!sceneEventsSubscribed || networkManager?.SceneManager == null)
            return;

        networkManager.SceneManager.OnClientPresenceChangeEnd -= HandleClientPresenceChangeEnd;
        networkManager.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;

        sceneEventsSubscribed = false;
    }

    private void HandleClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
    {
        if (!args.Added)
            return;

        if (args.Scene != gameObject.scene)
            return;

        SpawnPlayer(args.Connection);
    }

    private void HandleRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Stopped)
            return;

        spawnedClientIds.Remove(connection.ClientId);

        Debug.Log(
            $"[ScenePlayerSpawner] Cleared spawn tracking for client " +
            $"{connection.ClientId} after disconnect."
        );
    }

    private IEnumerator TrySpawnExistingConnections()
    {
        yield return null;

        if (networkManager == null || networkManager.ServerManager == null || !networkManager.ServerManager.Started)
        {
            yield break;
        }

        if (!networkManager.SceneManager.SceneConnections.TryGetValue(gameObject.scene, out var connections))
        {
            yield break;
        }

        foreach (NetworkConnection connection in connections)
        {
            SpawnPlayer(connection);
        }
    }

    private void SpawnPlayer(NetworkConnection connection)
    {
        if (connection == null || !connection.IsValid || connection.Disconnecting)
            return;

        if (spawnedClientIds.Contains(connection.ClientId))
            return;

        if (connection.FirstObject != null)
        {
            spawnedClientIds.Add(connection.ClientId);
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError(
                "[ScenePlayerSpawner] Player prefab is not assigned."
            );

            return;
        }

        GetSpawnTransform(out Vector3 position, out Quaternion rotation);

        NetworkObject playerObject = networkManager.GetPooledInstantiated(playerPrefab, position, rotation, true);

        if (playerObject == null)
        {
            Debug.LogError(
                "[ScenePlayerSpawner] Player could not be instantiated."
            );

            return;
        }

        networkManager.ServerManager.Spawn(playerObject, connection, gameObject.scene);

        spawnedClientIds.Add(connection.ClientId);

        Debug.Log(
            $"[ScenePlayerSpawner] Player spawned for client " +
            $"{connection.ClientId} in scene {gameObject.scene.name}."
        );
    }

    private void GetSpawnTransform(out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            position = playerPrefab.transform.position;
            rotation = playerPrefab.transform.rotation;
            return;
        }

        Transform spawnPoint = spawnPoints[nextSpawnIndex];

        if (spawnPoint == null)
        {
            position = playerPrefab.transform.position;
            rotation = playerPrefab.transform.rotation;
        }
        else
        {
            position = spawnPoint.position;
            rotation = spawnPoint.rotation;
        }

        nextSpawnIndex++;

        if (nextSpawnIndex >= spawnPoints.Length)
            nextSpawnIndex = 0;
    }
}