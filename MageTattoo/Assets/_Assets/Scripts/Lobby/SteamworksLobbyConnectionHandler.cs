using System;
using HeathenEngineering.SteamworksIntegration;
using Steamworks;
using UnityEngine;
using SteamOverlay =
    HeathenEngineering.SteamworksIntegration.API.Overlay;

public class SteamworksLobbyConnectionHandler : MonoBehaviour
{
    private const string HOST_STEAM_ID_KEY = "HostSteamID";
    private const string CONNECT_LOBBY_ARGUMENT = "+connect_lobby";

    private static bool launchArgumentsProcessed;

    [Header("Dependencies")]
    [SerializeField] private LobbyManager lobbyManager;

    public event Action HostLobbyCreated;
    public event Action<EResult> LobbyCreationFailed;

    public event Action<string> ClientLobbyEntered;
    public event Action<EChatRoomEnterResponse> LobbyJoinFailed;
    public event Action<string> LobbyValidationFailed;

    public event Action LobbyLeft;
    public event Action AskedToLeave;

    public event Action ExternalLobbyJoinRequested;

    private LobbyData cachedLobby;
    private ulong? pendingExternalLobbyId;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        launchArgumentsProcessed = false;
    }

    private void OnEnable()
    {
        SubscribeToLobbyEvents();
        SubscribeToOverlayEvents();
    }

    private void Start()
    {
        ProcessLaunchArguments();
    }

    private void OnDisable()
    {
        UnsubscribeFromLobbyEvents();
        UnsubscribeFromOverlayEvents();
    }

    // Solicita al LobbyManager crear un lobby para el coordinador.
    public bool TryCreateLobby()
    {
        if (!ValidateLobbyManager())
            return false;

        lobbyManager.Create();
        return true;
    }

    // Almacena la invitación recibida para aceptarla desde la UI del juego.
    public bool TryCacheLobbyInvite(LobbyInvite lobbyInvite)
    {
        LobbyData targetLobby = lobbyInvite.ToLobby;

        if (targetLobby == null)
        {
            Debug.LogWarning("The received lobby invite is not valid.");
            return false;
        }

        cachedLobby = targetLobby;
        return true;
    }

    // Solicita entrar al último lobby almacenado desde la UI del juego.
    public bool TryJoinCachedLobby()
    {
        if (!ValidateLobbyManager())
            return false;

        if (cachedLobby == null)
        {
            Debug.LogWarning("No existing lobby to join.");
            return false;
        }

        lobbyManager.Join(cachedLobby);
        return true;
    }

    // Solicita entrar directamente a un lobby recibido desde Steam.
    public bool TryJoinLobby(ulong lobbyId)
    {
        if (!ValidateLobbyManager())
            return false;

        if (lobbyId == 0)
        {
            Debug.LogWarning("The external lobby ID is not valid.");
            return false;
        }

        ClearCachedLobby();
        lobbyManager.Join(lobbyId);

        return true;
    }

    // Entrega al coordinador una solicitud externa pendiente una sola vez.
    public bool TryConsumeExternalLobbyJoinRequest(
        out ulong lobbyId)
    {
        if (!pendingExternalLobbyId.HasValue)
        {
            lobbyId = 0;
            return false;
        }

        lobbyId = pendingExternalLobbyId.Value;
        pendingExternalLobbyId = null;

        return true;
    }

    // Abandona el lobby de Steam durante la limpieza del coordinador.
    public void LeaveLobby()
    {
        ClearCachedLobby();
        pendingExternalLobbyId = null;

        if (!ValidateLobbyManager())
            return;

        if (lobbyManager.HasLobby)
            lobbyManager.Leave();
    }

    private void HandleLobbyCreated(LobbyData lobby)
    {
        lobby[HOST_STEAM_ID_KEY] = SteamUser.GetSteamID().ToString();
        HostLobbyCreated?.Invoke();
    }

    private void HandleLobbyCreationFailed(EResult result)
    {
        LobbyCreationFailed?.Invoke(result);
    }

    private void HandleLobbyEntered(LobbyData lobby)
    {
        ClearCachedLobby();

        if (lobby.IsOwner)
        {
            LobbyValidationFailed?.Invoke(
                "The local player entered the lobby as its owner " +
                "during a client join."
            );

            return;
        }

        string hostSteamId = lobby[HOST_STEAM_ID_KEY];

        if (string.IsNullOrWhiteSpace(hostSteamId))
        {
            LobbyValidationFailed?.Invoke(
                $"The lobby does not contain the field " +
                $"{HOST_STEAM_ID_KEY}."
            );

            return;
        }

        ClientLobbyEntered?.Invoke(hostSteamId);
    }

    private void HandleLobbyJoinFailed(
        EChatRoomEnterResponse response)
    {
        ClearCachedLobby();
        LobbyJoinFailed?.Invoke(response);
    }

    private void HandleLobbyLeft()
    {
        ClearCachedLobby();
        LobbyLeft?.Invoke();
    }

    private void HandleAskedToLeave()
    {
        AskedToLeave?.Invoke();
    }

    private void HandleGameLobbyJoinRequested(LobbyData lobby, UserData _)
    {
        if (lobby == null ||
            !ulong.TryParse(lobby.ToString(), out ulong lobbyId))
        {
            Debug.LogWarning(
                "Steam requested an invalid lobby ID."
            );

            return;
        }

        QueueExternalLobbyJoinRequest(lobbyId);
    }

    private void QueueExternalLobbyJoinRequest(ulong lobbyId)
    {
        if (lobbyId == 0)
        {
            Debug.LogWarning(
                "Steam requested an invalid lobby ID."
            );

            return;
        }

        if (pendingExternalLobbyId.HasValue)
        {
            if (pendingExternalLobbyId.Value != lobbyId)
            {
                Debug.LogWarning(
                    "Another external lobby join request is already pending."
                );
            }

            return;
        }

        pendingExternalLobbyId = lobbyId;
        ExternalLobbyJoinRequested?.Invoke();
    }

    private void ProcessLaunchArguments()
    {
        if (launchArgumentsProcessed)
            return;

        launchArgumentsProcessed = true;

        string[] arguments = Environment.GetCommandLineArgs();

        for (int i = 0; i < arguments.Length; i++)
        {
            if (!string.Equals(
                    arguments[i],
                    CONNECT_LOBBY_ARGUMENT,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool hasLobbyArgument = i + 1 < arguments.Length;

            if (!hasLobbyArgument ||
                !ulong.TryParse(arguments[i + 1], out ulong lobbyId))
            {
                Debug.LogWarning(
                    "Steam provided an invalid +connect_lobby argument."
                );

                return;
            }

            QueueExternalLobbyJoinRequest(lobbyId);
            return;
        }
    }

    private bool ValidateLobbyManager()
    {
        if (lobbyManager != null)
            return true;

        Debug.LogError("LobbyManager is null.");
        return false;
    }

    private void ClearCachedLobby()
    {
        cachedLobby = null;
    }

    private void SubscribeToLobbyEvents()
    {
        if (lobbyManager == null)
            return;

        lobbyManager.evtCreated.AddListener(HandleLobbyCreated);
        lobbyManager.evtCreateFailed.AddListener(HandleLobbyCreationFailed);
        lobbyManager.evtEnterSuccess.AddListener(HandleLobbyEntered);
        lobbyManager.evtEnterFailed.AddListener(HandleLobbyJoinFailed);
        lobbyManager.evtLeave.AddListener(HandleLobbyLeft);
        lobbyManager.evtAskedToLeave.AddListener(HandleAskedToLeave);
    }

    private void UnsubscribeFromLobbyEvents()
    {
        if (lobbyManager == null)
            return;

        lobbyManager.evtCreated.RemoveListener(HandleLobbyCreated);
        lobbyManager.evtCreateFailed.RemoveListener(HandleLobbyCreationFailed);
        lobbyManager.evtEnterSuccess.RemoveListener(HandleLobbyEntered);
        lobbyManager.evtEnterFailed.RemoveListener(HandleLobbyJoinFailed);
        lobbyManager.evtLeave.RemoveListener(HandleLobbyLeft);
        lobbyManager.evtAskedToLeave.RemoveListener(HandleAskedToLeave);
    }

    private void SubscribeToOverlayEvents()
    {
        SteamOverlay.Client.EventGameLobbyJoinRequested.AddListener(HandleGameLobbyJoinRequested);
    }

    private void UnsubscribeFromOverlayEvents()
    {
        SteamOverlay.Client.EventGameLobbyJoinRequested.RemoveListener(HandleGameLobbyJoinRequested);
    }
}