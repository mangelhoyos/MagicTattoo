using System.Collections;
using FishNet.Managing;
using FishNet.Transporting;
using HeathenEngineering.SteamworksIntegration;
using UnityEngine;
using UnityEngine.Events;

public class GameConnectionHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Min(0f)] private float returnToLobbyDelay = 5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onConnectionLost;
    [SerializeField] private UnityEvent onReturnToLobby;

    private NetworkManager networkManager;
    private FishySteamworks.FishySteamworks fishySteamworks;
    private LobbyManager lobbyManager;

    private Coroutine returnToLobbyCoroutine;

    private bool isReturningToLobby;
    private bool connectionIssueReported;
    private bool networkEventsSubscribed;
    private bool lobbyEventsSubscribed;

    private void OnEnable()
    {
        TryInitialize();
    }

    private void Start()
    {
        TryInitialize();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
        UnsubscribeFromLobbyEvents();
        CancelReturnToLobby();
    }

    // Permite abandonar voluntariamente la partida desde la UI.
    public void LeaveGame()
    {
        if (isReturningToLobby)
            return;

        isReturningToLobby = true;

        Debug.Log(
            "[GameConnection] Player requested to leave the game."
        );

        CancelReturnToLobby();
        CleanupConnection();

        onReturnToLobby?.Invoke();
    }

    private void HandleClientConnectionState(
        ClientConnectionStateArgs args)
    {
        if (isReturningToLobby ||
            args.ConnectionState != LocalConnectionState.Stopped)
        {
            return;
        }

        ReportConnectionIssue(
            "FishNet client connection stopped unexpectedly."
        );
    }

    private void HandleServerConnectionState(
        ServerConnectionStateArgs args)
    {
        if (isReturningToLobby ||
            args.ConnectionState != LocalConnectionState.Stopped)
        {
            return;
        }

        ReportConnectionIssue(
            "FishNet server stopped unexpectedly."
        );
    }

    private void HandleClientTimeout()
    {
        if (isReturningToLobby)
            return;

        ReportConnectionIssue(
            "FishNet client connection timed out."
        );
    }

    private void HandleLobbyLeft()
    {
        if (isReturningToLobby)
            return;

        ReportConnectionIssue(
            "Steam lobby was left unexpectedly."
        );
    }

    private void HandleAskedToLeave()
    {
        if (isReturningToLobby)
            return;

        ReportConnectionIssue(
            "Steam requested the local player to leave the lobby."
        );
    }

    private void ReportConnectionIssue(string errorMessage)
    {
        if (connectionIssueReported || isReturningToLobby)
            return;

        connectionIssueReported = true;

        Debug.LogWarning(
            $"[GameConnection] {errorMessage}"
        );

        BeginConnectionLostSequence();
    }

    private void BeginConnectionLostSequence()
    {
        if (returnToLobbyCoroutine != null)
            return;

        onConnectionLost?.Invoke();

        returnToLobbyCoroutine =
            StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnToLobbyDelay);

        returnToLobbyCoroutine = null;
        isReturningToLobby = true;

        Debug.Log(
            "[GameConnection] Returning to lobby after connection loss."
        );

        CleanupConnection();

        onReturnToLobby?.Invoke();
    }

    private void CleanupConnection()
    {
        if (fishySteamworks != null)
        {
            bool clientStarted =
                networkManager?.ClientManager != null &&
                networkManager.ClientManager.Started;

            bool serverStarted =
                networkManager?.ServerManager != null &&
                networkManager.ServerManager.Started;

            if (clientStarted)
                fishySteamworks.StopConnection(false);

            if (serverStarted)
                fishySteamworks.StopConnection(true);
        }

        if (lobbyManager != null && lobbyManager.HasLobby)
            lobbyManager.Leave();
    }

    private void TryInitialize()
    {
        networkManager ??=
            FindFirstObjectByType<NetworkManager>();

        fishySteamworks ??=
            FindFirstObjectByType<FishySteamworks.FishySteamworks>();

        lobbyManager ??=
            FindFirstObjectByType<LobbyManager>();

        SubscribeToNetworkEvents();
        SubscribeToLobbyEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        if (networkEventsSubscribed ||
            networkManager == null ||
            networkManager.ClientManager == null ||
            networkManager.ServerManager == null)
        {
            return;
        }

        networkManager.ClientManager.OnClientConnectionState +=
            HandleClientConnectionState;

        networkManager.ClientManager.OnClientTimeOut +=
            HandleClientTimeout;

        networkManager.ServerManager.OnServerConnectionState +=
            HandleServerConnectionState;

        networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!networkEventsSubscribed || networkManager == null)
            return;

        if (networkManager.ClientManager != null)
        {
            networkManager.ClientManager.OnClientConnectionState -=
                HandleClientConnectionState;

            networkManager.ClientManager.OnClientTimeOut -=
                HandleClientTimeout;
        }

        if (networkManager.ServerManager != null)
        {
            networkManager.ServerManager.OnServerConnectionState -=
                HandleServerConnectionState;
        }

        networkEventsSubscribed = false;
    }

    private void SubscribeToLobbyEvents()
    {
        if (lobbyEventsSubscribed || lobbyManager == null)
            return;

        lobbyManager.evtLeave.AddListener(HandleLobbyLeft);
        lobbyManager.evtAskedToLeave.AddListener(HandleAskedToLeave);

        lobbyEventsSubscribed = true;
    }

    private void UnsubscribeFromLobbyEvents()
    {
        if (!lobbyEventsSubscribed || lobbyManager == null)
            return;

        lobbyManager.evtLeave.RemoveListener(HandleLobbyLeft);
        lobbyManager.evtAskedToLeave.RemoveListener(HandleAskedToLeave);

        lobbyEventsSubscribed = false;
    }

    private void CancelReturnToLobby()
    {
        if (returnToLobbyCoroutine == null)
            return;

        StopCoroutine(returnToLobbyCoroutine);
        returnToLobbyCoroutine = null;
    }
}