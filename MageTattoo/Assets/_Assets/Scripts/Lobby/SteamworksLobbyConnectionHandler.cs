using System;
using Steamworks;
using UnityEngine;
using HeathenEngineering.SteamworksIntegration;
using SteamOverlay = HeathenEngineering.SteamworksIntegration.API.Overlay;

public class SteamworksLobbyConnectionHandler : MonoBehaviour
{
    private const string HOST_STEAM_ID_KEY = "HostSteamID";
    private const string CONNECT_LOBBY_ARGUMENT = "+connect_lobby";

    private static bool launchArgumentsProcessed;

    [Header("Dependencies")]
    [SerializeField] private LobbyManager lobbyManager;
    private LobbyManager subscribedLobbyManager;

    public event Action HostLobbyCreated;
    public event Action<EResult> LobbyCreationFailed;

    public event Action<string> ClientLobbyEntered;
    public event Action<EChatRoomEnterResponse> LobbyJoinFailed;
    public event Action<string> LobbyValidationFailed;

    public event Action<LobbyInvite> LobbyInviteReceived;
    public event Action LobbyLeft;
    public event Action AskedToLeave;

    public event Action ExternalLobbyJoinRequested;

    private LobbyData cachedLobby;
    private LobbyData pendingExternalLobby;

#if UNITY_EDITOR
    [SerializeField] private bool simulateLobbyCreationFailure;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        launchArgumentsProcessed = false;
    }

    private void Awake()
    { 
        if (lobbyManager == null)
        lobbyManager = FindFirstObjectByType<LobbyManager>();
    }

    private void OnEnable()
    {
        ResolveLobbyManager();
        SubscribeToLobbyEvents();
        SubscribeToOverlayEvents();
    }

    private void Start()
    {
        if (lobbyManager == null)
        {
            ResolveLobbyManager();
            SubscribeToLobbyEvents();
        }

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

#if UNITY_EDITOR
        if (simulateLobbyCreationFailure)
        {
            LobbyCreationFailed?.Invoke(EResult.k_EResultFail);
            return true;
        }
#endif

        lobbyManager.Create();
        return true;
    }

    // Almacena la invitación recibida para aceptarla desde la UI del juego.
    public bool TryCacheLobbyInvite(LobbyInvite lobbyInvite)
    {
        LobbyData targetLobby = lobbyInvite.ToLobby;

        if (!targetLobby.IsValid)
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

        if (!cachedLobby.IsValid)
        {
            Debug.LogWarning("No existing lobby to join.");
            return false;
        }

        lobbyManager.Join(cachedLobby);
        return true;
    }

    // Solicita entrar directamente a un lobby recibido desde Steam.
    public bool TryJoinLobby(LobbyData lobby)
    {
        if (!ValidateLobbyManager())
            return false;

        if (!lobby.IsValid)
        {
            Debug.LogWarning("The external lobby is not valid.");
            return false;
        }

        ClearCachedLobby();
        lobbyManager.Join(lobby);

        return true;
    }

    // Entrega al coordinador una solicitud externa pendiente una sola vez.
    public bool TryConsumeExternalLobbyJoinRequest(out LobbyData lobby)
    {
        if (!pendingExternalLobby.IsValid)
        {
            lobby = default;
            return false;
        }

        lobby = pendingExternalLobby;
        pendingExternalLobby = default;

        return true;
    }

    // Abandona el lobby de Steam durante la limpieza del coordinador.
    public void LeaveLobby()
    {
        ClearCachedLobby();
        pendingExternalLobby = default;

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

    private void HandleLobbyInvite(LobbyInvite lobbyInvite)
    {
        LobbyInviteReceived?.Invoke(lobbyInvite);
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
        QueueExternalLobbyJoinRequest(lobby);
    }

    private void QueueExternalLobbyJoinRequest(LobbyData lobby)
    {
        if (!lobby.IsValid)
        {
            Debug.LogWarning("Steam requested an invalid lobby.");
            return;
        }

        if (pendingExternalLobby.IsValid)
        {
            if (pendingExternalLobby != lobby)
            {
                Debug.LogWarning(
                    "Another external lobby join request is already pending."
                );
            }

            return;
        }

        pendingExternalLobby = lobby;
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
            if (!string.Equals(arguments[i], CONNECT_LOBBY_ARGUMENT, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool hasLobbyArgument = i + 1 < arguments.Length;

            if (!hasLobbyArgument)
            {
                Debug.LogWarning(
                    "Steam did not provide a lobby after +connect_lobby."
                );

                return;
            }

            LobbyData lobby = LobbyData.Get(arguments[i + 1]);

            if (!lobby.IsValid)
            {
                Debug.LogWarning(
                    "Steam provided an invalid +connect_lobby argument."
                );

                return;
            }

            QueueExternalLobbyJoinRequest(lobby);
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
        cachedLobby = default;
    }

    private void ResolveLobbyManager()
    {
        if (lobbyManager == null)
            lobbyManager = FindFirstObjectByType<LobbyManager>();
    }

    private void SubscribeToLobbyEvents()
    {
        if (lobbyManager == null)
            return;

        if (subscribedLobbyManager == lobbyManager)
            return;

        UnsubscribeFromLobbyEvents();

        lobbyManager.evtCreated.AddListener(HandleLobbyCreated);
        lobbyManager.evtCreateFailed.AddListener(HandleLobbyCreationFailed);
        lobbyManager.evtEnterSuccess.AddListener(HandleLobbyEntered);
        lobbyManager.evtEnterFailed.AddListener(HandleLobbyJoinFailed);
        lobbyManager.evtLeave.AddListener(HandleLobbyLeft);
        lobbyManager.evtAskedToLeave.AddListener(HandleAskedToLeave);
        lobbyManager.evtLobbyInvite.AddListener(HandleLobbyInvite);

        subscribedLobbyManager = lobbyManager;
    }

    private void UnsubscribeFromLobbyEvents()
    {
        if (subscribedLobbyManager == null)
            return;

        lobbyManager.evtCreated.RemoveListener(HandleLobbyCreated);
        lobbyManager.evtCreateFailed.RemoveListener(HandleLobbyCreationFailed);
        lobbyManager.evtEnterSuccess.RemoveListener(HandleLobbyEntered);
        lobbyManager.evtEnterFailed.RemoveListener(HandleLobbyJoinFailed);
        lobbyManager.evtLeave.RemoveListener(HandleLobbyLeft);
        lobbyManager.evtAskedToLeave.RemoveListener(HandleAskedToLeave);
        lobbyManager.evtLobbyInvite.RemoveListener(HandleLobbyInvite);

        subscribedLobbyManager = null;
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