using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
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
    [SerializeField] private SteamworksLobbyConnectionHandler steamworksHandler;
    [SerializeField] private FishNetLobbyConnectionHandler fishNetHandler;

    [Header("UI Events")]
    [SerializeField] private UnityEvent onInviteAvailable;
    [SerializeField] private UnityEvent onOperationStarted;

    [FormerlySerializedAs("onConnectionReady")]
    [SerializeField] private UnityEvent onHostConnected;

    [SerializeField] private UnityEvent onClientConnected;
    [SerializeField] private UnityEvent<string> onConnectionFailed;
    [SerializeField] private UnityEvent onReturnedToIdle;

    private LobbyConnectionState state = LobbyConnectionState.Idle;
    private string pendingDisconnectError;

    public LobbyConnectionState State => state;

    private void OnEnable()
    {
        SubscribeToSteamworksEvents();
        SubscribeToFishNetEvents();

        HandleExternalLobbyJoinRequested();
    }

    private void OnDisable()
    {
        UnsubscribeFromSteamworksEvents();
        UnsubscribeFromFishNetEvents();
    }

    // Recibe y almacena una invitación emitida por el LobbyManager.
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

        if (!steamworksHandler.TryCacheLobbyInvite(lobbyInvite))
            return;

        onInviteAvailable?.Invoke();
    }

    // Inicia desde la UI el flujo para crear el lobby y levantar el host.
    public void StartHost()
    {
        if (!TryBeginOperation(LobbyConnectionState.CreatingLobby))
            return;

        if (!steamworksHandler.TryCreateLobby())
        {
            BeginDisconnect(
                "Lobby creation request could not be started."
            );
        }
    }

    // Inicia desde la UI la conexión al lobby almacenado.
    public void JoinLobby()
    {
        if (!TryBeginOperation(LobbyConnectionState.JoiningLobby))
            return;

        if (!steamworksHandler.TryJoinCachedLobby())
        {
            BeginDisconnect(
                "Lobby join request could not be started."
            );
        }
    }

    private void HandleExternalLobbyJoinRequested()
    {
        if (steamworksHandler == null)
        {
            Debug.LogError("SteamworksLobbyConnectionHandler is null.");
            return;
        }

        if (!steamworksHandler.TryConsumeExternalLobbyJoinRequest(out LobbyData lobby))
        {
            return;
        }

        if (!TryBeginOperation(LobbyConnectionState.JoiningLobby))
            return;

        if (!steamworksHandler.TryJoinLobby(lobby))
        {
            BeginDisconnect(
                "External lobby join request could not be started."
            );
        }
    }

    // Permite que otros sistemas soliciten una desconexión segura.
    public void Disconnect()
    {
        if (state == LobbyConnectionState.Idle)
            return;

        if (state == LobbyConnectionState.Disconnecting)
        {
            Debug.LogWarning("A disconnection operation is already active.");
            return;
        }

        BeginDisconnect(null);
    }

    private void HandleHostLobbyCreated()
    {
        if (!IsExpectedState(LobbyConnectionState.CreatingLobby,"Lobby creation callback"))
        {
            return;
        }

        SetState(LobbyConnectionState.StartingHost);

        if (!fishNetHandler.TryStartHost())
        {
            BeginDisconnect("FishNet host could not be started.");
        }
    }

    private void HandleLobbyCreationFailed(EResult result)
    {
        if (!IsExpectedState(LobbyConnectionState.CreatingLobby,"Lobby creation failure"))
        {
            return;
        }

        BeginDisconnect(
            $"Lobby creation failed with result: {result}."
        );
    }

    private void HandleClientLobbyEntered(string hostSteamId)
    {
        if (!IsExpectedState(LobbyConnectionState.JoiningLobby,"Lobby entered callback"))
        {
            return;
        }

        SetState(LobbyConnectionState.StartingClient);

        if (!fishNetHandler.TryStartClient(hostSteamId))
        {
            BeginDisconnect("FishNet client could not be started.");
        }
    }

    private void HandleLobbyJoinFailed(EChatRoomEnterResponse response)
    {
        if (!IsExpectedState(LobbyConnectionState.JoiningLobby,"Lobby join failure"))
        {
            return;
        }

        BeginDisconnect(
            $"Lobby join failed with response: {response}."
        );
    }

    private void HandleLobbyValidationFailed(string errorMessage)
    {
        if (!IsExpectedState(LobbyConnectionState.JoiningLobby,"Lobby validation failure"))
        {
            return;
        }

        BeginDisconnect(errorMessage);
    }

    private void HandleSteamLobbyLeft()
    {
        if (state == LobbyConnectionState.Idle || state == LobbyConnectionState.Disconnecting)
        {
            return;
        }

        BeginDisconnect("The Steam lobby was left unexpectedly.");
    }

    private void HandleAskedToLeave()
    {
        if (state == LobbyConnectionState.Idle || state == LobbyConnectionState.Disconnecting)
        {
            return;
        }

        BeginDisconnect(
            "Steam requested that the local player leave the lobby."
        );
    }

    private void HandleHostStarted()
    {
        if (!IsExpectedState(LobbyConnectionState.StartingHost,"Host started callback"))
        {
            return;
        }

        SetState(LobbyConnectionState.Connected);
        onHostConnected?.Invoke();
    }

    private void HandleClientStarted()
    {
        if (!IsExpectedState(LobbyConnectionState.StartingClient,"Client started callback"))
        {
            return;
        }

        SetState(LobbyConnectionState.Connected);
        onClientConnected?.Invoke();
    }

    private void HandleFishNetConnectionFailed(string errorMessage)
    {
        if (!IsFishNetSessionActive())
        {
            Debug.LogWarning(
                $"FishNet failure ignored because the current state is {state}."
            );

            return;
        }

        BeginDisconnect(errorMessage);
    }

    private void HandleFishNetConnectionsStopped()
    {
        if (!IsExpectedState(LobbyConnectionState.Disconnecting,"FishNet stop callback"))
        {
            return;
        }

        CompleteDisconnect();
    }

    private bool TryBeginOperation(LobbyConnectionState operationState)
    {
        if (!CanStartOperation() || !ValidateDependencies())
            return false;

        SetState(operationState);
        onOperationStarted?.Invoke();

        return true;
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

    private bool IsExpectedState(LobbyConnectionState expectedState, string callbackName)
    {
        if (state == expectedState)
            return true;

        Debug.LogWarning(
            $"{callbackName} ignored because the current state is {state}. " +
            $"Expected state: {expectedState}."
        );

        return false;
    }

    private bool IsFishNetSessionActive()
    {
        return state == LobbyConnectionState.StartingHost ||
               state == LobbyConnectionState.StartingClient ||
               state == LobbyConnectionState.Connected;
    }

    private void BeginDisconnect(string errorMessage)
    {
        if (state == LobbyConnectionState.Disconnecting)
            return;

        pendingDisconnectError = errorMessage;

        SetState(LobbyConnectionState.Disconnecting);

        if (fishNetHandler == null)
        {
            CompleteDisconnect();
            return;
        }

        fishNetHandler.StopConnections();
    }

    private void CompleteDisconnect()
    {
        steamworksHandler?.LeaveLobby();

        string errorMessage = pendingDisconnectError;
        pendingDisconnectError = null;

        SetState(LobbyConnectionState.Idle);

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            Debug.LogError(errorMessage);
            onConnectionFailed?.Invoke(errorMessage);
        }

        onReturnedToIdle?.Invoke();
    }

    private void SetState(LobbyConnectionState newState)
    {
        if (state == newState)
            return;

        Debug.Log(
            $"Lobby connection state changed: {state} -> {newState}."
        );

        state = newState;
    }

    private void SubscribeToSteamworksEvents()
    {
        if (steamworksHandler == null)
            return;

        steamworksHandler.HostLobbyCreated += HandleHostLobbyCreated;
        steamworksHandler.LobbyCreationFailed += HandleLobbyCreationFailed;
        steamworksHandler.ClientLobbyEntered += HandleClientLobbyEntered;
        steamworksHandler.LobbyJoinFailed += HandleLobbyJoinFailed;
        steamworksHandler.LobbyValidationFailed += HandleLobbyValidationFailed;
        steamworksHandler.LobbyLeft += HandleSteamLobbyLeft;
        steamworksHandler.AskedToLeave += HandleAskedToLeave;
        steamworksHandler.ExternalLobbyJoinRequested += HandleExternalLobbyJoinRequested;
        steamworksHandler.LobbyInviteReceived += ReceiveLobbyInvite;
    }

    private void UnsubscribeFromSteamworksEvents()
    {
        if (steamworksHandler == null)
            return;

        steamworksHandler.HostLobbyCreated -= HandleHostLobbyCreated;
        steamworksHandler.LobbyCreationFailed -= HandleLobbyCreationFailed;
        steamworksHandler.ClientLobbyEntered -= HandleClientLobbyEntered;
        steamworksHandler.LobbyJoinFailed -= HandleLobbyJoinFailed;
        steamworksHandler.LobbyValidationFailed -= HandleLobbyValidationFailed;
        steamworksHandler.LobbyLeft -= HandleSteamLobbyLeft;
        steamworksHandler.AskedToLeave -= HandleAskedToLeave;
        steamworksHandler.ExternalLobbyJoinRequested -= HandleExternalLobbyJoinRequested;
        steamworksHandler.LobbyInviteReceived -= ReceiveLobbyInvite;
    }

    private void SubscribeToFishNetEvents()
    {
        if (fishNetHandler == null)
            return;

        fishNetHandler.HostStarted += HandleHostStarted;
        fishNetHandler.ClientStarted += HandleClientStarted;
        fishNetHandler.ConnectionFailed += HandleFishNetConnectionFailed;
        fishNetHandler.ConnectionsStopped += HandleFishNetConnectionsStopped;
    }

    private void UnsubscribeFromFishNetEvents()
    {
        if (fishNetHandler == null)
            return;

        fishNetHandler.HostStarted -= HandleHostStarted;
        fishNetHandler.ClientStarted -= HandleClientStarted;
        fishNetHandler.ConnectionFailed -= HandleFishNetConnectionFailed;
        fishNetHandler.ConnectionsStopped -= HandleFishNetConnectionsStopped;
    }
}