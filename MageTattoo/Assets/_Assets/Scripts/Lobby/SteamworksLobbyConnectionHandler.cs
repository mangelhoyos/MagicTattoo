using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using HeathenEngineering.SteamworksIntegration;

public class SteamworksLobbyConnectionHandler : MonoBehaviour
{
    private const string HostSteamIdKey = "HostSteamID";

    [Header("Dependencies")]
    [SerializeField] private LobbyManager lobbyManager;

    [Header("Events")]
    [SerializeField] private UnityEvent onHostLobbyCreated;
    [SerializeField] private UnityEvent<string> onClientLobbyEntered;
    [SerializeField] private UnityEvent<EResult> onLobbyCreationFailed;

    private LobbyData lobbyToJoin;

    private void OnEnable()
    {
        if (lobbyManager == null)
            return;

        lobbyManager.evtCreated.AddListener(HandleLobbyCreated);
        lobbyManager.evtCreateFailed.AddListener(HandleLobbyCreationFailed);
        lobbyManager.evtEnterSuccess.AddListener(HandleLobbyEntered);
    }

    private void OnDisable()
    {
        if (lobbyManager == null)
            return;

        lobbyManager.evtCreated.RemoveListener(HandleLobbyCreated);
        lobbyManager.evtCreateFailed.RemoveListener(HandleLobbyCreationFailed);
        lobbyManager.evtEnterSuccess.RemoveListener(HandleLobbyEntered);
    }

    public bool TryCreateLobby()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("LobbyManager is null.");
            return false;
        }

        lobbyManager.Create();
        return true;
    }

    // Se llama al recibir una invitación de Steam
    public void CacheLobbyInvite(LobbyInvite lobbyInvite)
    {
        lobbyToJoin = lobbyInvite.ToLobby;
    }

    public bool TryJoinCachedLobby()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("LobbyManager is null.");
            return false;
        }

        if (lobbyToJoin == null)
        {
            Debug.LogWarning("No existing lobby to join.");
            return false;
        }

        lobbyManager.Join(lobbyToJoin);
        return true;
    }

    private void HandleLobbyCreated(LobbyData lobby)
    {
        // Steamworks se encarga únicamente de publicar la información necesaria para encontrar al host.
        lobby[HostSteamIdKey] = SteamUser.GetSteamID().ToString();

        onHostLobbyCreated?.Invoke();
    }

    private void HandleLobbyCreationFailed(EResult result)
    {
        onLobbyCreationFailed?.Invoke(result);
    }

    private void HandleLobbyEntered(LobbyData lobby)
    {
        // El propietario iniciará FishNet mediante HandleLobbyCreated, así que aquí solo procesamos clientes.
        if (lobby.IsOwner)
            return;

        string hostSteamId = lobby[HostSteamIdKey];

        if (string.IsNullOrWhiteSpace(hostSteamId))
        {
            Debug.LogError($"The lobby does not contain the field {HostSteamIdKey}.");
            return;
        }

        onClientLobbyEntered?.Invoke(hostSteamId);
    }
}