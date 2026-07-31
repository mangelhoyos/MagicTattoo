using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using HeathenEngineering.SteamworksIntegration;

public enum LobbyConnectionState
{
    Idle,
    CreatingLobby,
    JoiningLobby,
    StartingHost,
    StartingClient,
    Connected,
    Disconnecting
}

public class LobbyConnectionCoordinator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private SteamworksLobbyConnectionHandler steamworksHandler;

    [SerializeField]
    private FishNetLobbyConnectionHandler fishNetHandler;

    [Header("UI Events")]
    [SerializeField] private UnityEvent onInviteAvailable;
    [SerializeField] private UnityEvent onOperationStarted;
    [SerializeField] private UnityEvent onConnectionReady;
    [SerializeField] private UnityEvent<string> onConnectionFailed;
    [SerializeField] private UnityEvent onReturnedToIdle;

    private LobbyConnectionState state = LobbyConnectionState.Idle;

    public LobbyConnectionState State => state;

    // Se llama desde el evento de invitación de Heathens.
    public void ReceiveLobbyInvite(LobbyInvite lobbyInvite)
    {
        if (state != LobbyConnectionState.Idle)
        {
            Debug.LogWarning(
                $"Lobby invite ignored because the current state is {state}."
            );

            return;
        }

        if (steamworksHandler == null)
        {
            Debug.LogError("SteamworksLobbyConnectionHandler is null.");
            return;
        }

        steamworksHandler.CacheLobbyInvite(lobbyInvite);
        onInviteAvailable?.Invoke();
    }

    // Se llama desde el botón para crear el lobby.
    public void StartHost()
    {
        if (!CanStartOperation())
            return;

        if (!ValidateDependencies())
            return;

        SetState(LobbyConnectionState.CreatingLobby);
        onOperationStarted?.Invoke();

        if (!steamworksHandler.TryCreateLobby())
        {
            FailAndReset("Lobby creation request could not be started.");
        }
    }

    // Se llama desde el botón para entrar al lobby almacenado.
    public void JoinLobby()
    {
        if (!CanStartOperation())
            return;

        if (!ValidateDependencies())
            return;

        SetState(LobbyConnectionState.JoiningLobby);
        onOperationStarted?.Invoke();

        if (!steamworksHandler.TryJoinCachedLobby())
        {
            FailAndReset("Lobby join request could not be started.");
        }
    }

    // Se llama cuando Steam confirma que el lobby fue creado.
    public void HandleHostLobbyCreated()
    {
        if (state != LobbyConnectionState.CreatingLobby)
        {
            Debug.LogWarning(
                $"Lobby creation callback ignored because the current state is {state}."
            );

            return;
        }

        SetState(LobbyConnectionState.StartingHost);

        if (!fishNetHandler.TryStartHost())
        {
            FailAndReset("FishNet host could not be started.");
        }
    }

    // Se llama cuando Steam confirma que entramos al lobby.
    public void HandleClientLobbyEntered(string hostSteamId)
    {
        if (state != LobbyConnectionState.JoiningLobby)
        {
            Debug.LogWarning(
                $"Lobby entered callback ignored because the current state is {state}."
            );

            return;
        }

        SetState(LobbyConnectionState.StartingClient);

        if (!fishNetHandler.TryStartClient(hostSteamId))
        {
            FailAndReset("FishNet client could not be started.");
        }

        // Todavía no cambiamos a Connected.
        // Más adelante escucharemos la confirmación real de FishNet.
    }

    // Se llama cuando Steam no puede crear el lobby.
    public void HandleLobbyCreationFailed(EResult result)
    {
        if (state != LobbyConnectionState.CreatingLobby)
        {
            Debug.LogWarning(
                $"Lobby creation failure ignored because the current state is {state}."
            );

            return;
        }

        FailAndReset($"Lobby creation failed with result: {result}.");
    }

    // Por ahora conserva el comportamiento actual de FishNetLobbyConnectionHandler.
    public void HandleHostStarted()
    {
        if (state != LobbyConnectionState.StartingHost)
        {
            Debug.LogWarning(
                $"Host started callback ignored because the current state is {state}."
            );

            return;
        }

        SetState(LobbyConnectionState.Connected);
        onConnectionReady?.Invoke();
    }

    private bool CanStartOperation()
    {
        if (state == LobbyConnectionState.Idle)
            return true;

        Debug.LogWarning(
            $"Connection operation rejected because the current state is {state}."
        );

        return false;
    }

    private bool ValidateDependencies()
    {
        if (steamworksHandler == null)
        {
            Debug.LogError("SteamworksLobbyConnectionHandler is null.");
            return false;
        }

        if (fishNetHandler == null)
        {
            Debug.LogError("FishNetLobbyConnectionHandler is null.");
            return false;
        }

        return true;
    }

    private void FailAndReset(string errorMessage)
    {
        fishNetHandler?.StopConnections();

        Debug.LogError(errorMessage);

        SetState(LobbyConnectionState.Idle);

        onConnectionFailed?.Invoke(errorMessage);
        onReturnedToIdle?.Invoke();
    }

    private void SetState(LobbyConnectionState newState)
    {
        if (state == newState)
            return;

        Debug.Log($"Lobby connection state changed: {state} -> {newState}.");

        state = newState;
    }
}