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
    [SerializeField, Min(1f)] private float networkShutdownTimeout = 5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onConnectionLost;
    [SerializeField] private UnityEvent onReturnToLobby;

    private NetworkManager networkManager;
    private FishySteamworks.FishySteamworks fishySteamworks;
    private LobbyManager lobbyManager;

    private Coroutine returnToLobbyCoroutine;
    private Coroutine networkShutdownCoroutine;

    private bool isReturningToLobby;
    private bool connectionIssueReported;

    private bool networkEventsSubscribed;
    private bool lobbyEventsSubscribed;

    private bool isWaitingForNetworkShutdown;
    private bool clientShutdownComplete;
    private bool serverShutdownComplete;

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
        CancelNetworkShutdown();
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
        BeginNetworkShutdown();
    }

    private void HandleClientConnectionState(
        ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped)
            return;

        if (isWaitingForNetworkShutdown)
        {
            clientShutdownComplete = true;

            Debug.Log(
                "[GameConnection] FishNet client shutdown completed."
            );

            return;
        }

        if (isReturningToLobby)
            return;

        ReportConnectionIssue(
            "FishNet client connection stopped unexpectedly."
        );
    }

    private void HandleServerConnectionState(
        ServerConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Stopped)
            return;

        if (isWaitingForNetworkShutdown)
        {
            serverShutdownComplete = true;

            Debug.Log(
                "[GameConnection] FishNet server shutdown completed."
            );

            return;
        }

        if (isReturningToLobby)
            return;

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

        returnToLobbyCoroutine = StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnToLobbyDelay);

        returnToLobbyCoroutine = null;

        if (isReturningToLobby)
            yield break;

        isReturningToLobby = true;

        LobbyReturnContext.SetConnectionError();

        BeginNetworkShutdown();
    }

    private void BeginNetworkShutdown()
    {
        if (networkShutdownCoroutine != null)
            return;

        networkShutdownCoroutine =
            StartCoroutine(ShutdownNetworkAndReturnToLobby());
    }

    private IEnumerator ShutdownNetworkAndReturnToLobby()
    {
        TryInitialize();

        bool clientWasStarted =
            networkManager?.ClientManager != null &&
            networkManager.ClientManager.Started;

        bool serverWasStarted = networkManager?.ServerManager != null && networkManager.ServerManager.Started;

        clientShutdownComplete = !clientWasStarted;
        serverShutdownComplete = !serverWasStarted;

        isWaitingForNetworkShutdown = true;

        Debug.Log(
            $"[GameConnection] Starting network shutdown. " +
            $"Client active: {clientWasStarted}, " +
            $"Server active: {serverWasStarted}."
        );

        if (fishySteamworks != null)
        {
            if (clientWasStarted)
                fishySteamworks.StopConnection(false);

            if (serverWasStarted)
                fishySteamworks.StopConnection(true);
        }
        else if (clientWasStarted || serverWasStarted)
        {
            Debug.LogError(
                "[GameConnection] FishySteamworks was not found during network shutdown."
            );
        }

        float elapsedTime = 0f;

        while ((!clientShutdownComplete ||
                !serverShutdownComplete) &&
               elapsedTime < networkShutdownTimeout)
        {
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        bool shutdownTimedOut = !clientShutdownComplete || !serverShutdownComplete;

        if (shutdownTimedOut)
        {
            Debug.LogWarning(
                "[GameConnection] Network shutdown timed out. " +
                "Returning to the lobby anyway."
            );

            if (fishySteamworks != null)
            {
                if (!clientShutdownComplete)
                    fishySteamworks.StopConnection(false);

                if (!serverShutdownComplete)
                    fishySteamworks.StopConnection(true);
            }
        }
        else
        {
            Debug.Log(
                "[GameConnection] Network shutdown completed."
            );
        }

        isWaitingForNetworkShutdown = false;

        // Da un frame a FishNet para finalizar su limpieza interna.
        yield return null;

        if (lobbyManager != null && lobbyManager.HasLobby)
        {
            Debug.Log(
                "[GameConnection] Leaving Steam lobby."
            );

            lobbyManager.Leave();
        }

        networkShutdownCoroutine = null;

        Debug.Log(
            "[GameConnection] Returning to lobby scene."
        );

        onReturnToLobby?.Invoke();
    }

    private void TryInitialize()
    {
        networkManager ??= FindFirstObjectByType<NetworkManager>();

        fishySteamworks ??= FindFirstObjectByType<FishySteamworks.FishySteamworks>();

        lobbyManager ??= FindFirstObjectByType<LobbyManager>();

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

        networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
        networkManager.ClientManager.OnClientTimeOut += HandleClientTimeout;
        networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;

        networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!networkEventsSubscribed || networkManager == null)
            return;

        if (networkManager.ClientManager != null)
        {
            networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
            networkManager.ClientManager.OnClientTimeOut -= HandleClientTimeout;
        }

        if (networkManager.ServerManager != null)
        {
            networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
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

    private void CancelNetworkShutdown()
    {
        if (networkShutdownCoroutine != null)
        {
            StopCoroutine(networkShutdownCoroutine);
            networkShutdownCoroutine = null;
        }

        isWaitingForNetworkShutdown = false;
        clientShutdownComplete = false;
        serverShutdownComplete = false;
    }
}