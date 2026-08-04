using System;
using System.Collections;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

public class FishNetLobbyConnectionHandler : MonoBehaviour
{
    private enum ConnectionMode
    {
        None,
        Host,
        Client
    }

    [Header("Dependencies")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private FishySteamworks.FishySteamworks fishySteamworks;

    [Header("Settings")]
    [SerializeField, Min(1f)] private float stopTimeoutSeconds = 5f;

    public event Action HostStarted;
    public event Action ClientStarted;
    public event Action<string> ConnectionFailed;
    public event Action ConnectionsStopped;

    private ConnectionMode connectionMode = ConnectionMode.None;

    private LocalConnectionState serverState = LocalConnectionState.Stopped;

    private LocalConnectionState clientState = LocalConnectionState.Stopped;

    private bool isClientAuthenticated;
    private bool isConnectionConfirmed;
    private bool hasReportedFailure;
    private bool isStopRequested;

    private ServerManager subscribedServerManager;
    private ClientManager subscribedClientManager;
    private bool networkEventsSubscribed;

    private Coroutine stopTimeoutCoroutine;

    private void OnEnable()
    {
        TrySubscribeToNetworkEvents();
    }

    private void Start()
    {
        TrySubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        CancelStopTimeout();
        UnsubscribeFromNetworkEvents();
    }

    // Inicia el servidor y el cliente local solicitados por el coordinador.
    public bool TryStartHost()
    {
        if (!CanStartConnection())
            return false;

        PrepareConnection(ConnectionMode.Host);

        serverState = LocalConnectionState.Starting;

        bool serverStartRequested = fishySteamworks.StartConnection(true);

        if (!serverStartRequested)
        {
            serverState = LocalConnectionState.Stopped;
            return false;
        }

        clientState = LocalConnectionState.Starting;

        bool clientStartRequested = fishySteamworks.StartConnection(false);

        if (!clientStartRequested)
        {
            clientState = LocalConnectionState.Stopped;
            return false;
        }

        return true;
    }

    // Configura la dirección del host e inicia el cliente solicitado por el coordinador.
    public bool TryStartClient(string hostSteamId)
    {
        if (!CanStartConnection())
            return false;

        if (string.IsNullOrWhiteSpace(hostSteamId))
        {
            Debug.LogError("The host's SteamID is not valid.");
            return false;
        }

        PrepareConnection(ConnectionMode.Client);

        clientState = LocalConnectionState.Starting;

        fishySteamworks.SetClientAddress(hostSteamId);

        bool clientStartRequested = fishySteamworks.StartConnection(false);

        if (!clientStartRequested)
        {
            clientState = LocalConnectionState.Stopped;
            return false;
        }

        return true;
    }

    // Detiene las conexiones activas y notifica al coordinador al finalizar.
    public void StopConnections()
    {
        if (isStopRequested)
            return;

        isStopRequested = true;

        if (fishySteamworks == null)
        {
            ForceCompleteStopTracking();
            return;
        }

        RequestTransportStop();
        TryCompleteStop();

        if (isStopRequested)
            StartStopTimeout();
    }

    private void HandleServerConnectionState(
        ServerConnectionStateArgs args)
    {
        serverState = args.ConnectionState;

        if (serverState == LocalConnectionState.Started)
        {
            TryCompleteConnection();
            return;
        }

        if (serverState != LocalConnectionState.Stopped)
            return;

        if (isStopRequested)
        {
            TryCompleteStop();
            return;
        }

        if (connectionMode != ConnectionMode.Host)
            return;

        string errorMessage = isConnectionConfirmed
            ? "FishNet server stopped unexpectedly."
            : "FishNet server stopped before host startup completed.";

        ReportFailure(errorMessage);
    }

    private void HandleClientConnectionState(ClientConnectionStateArgs args)
    {
        clientState = args.ConnectionState;

        if (clientState == LocalConnectionState.Started)
        {
            TryCompleteConnection();
            return;
        }

        if (clientState != LocalConnectionState.Stopped)
            return;

        isClientAuthenticated = false;

        if (isStopRequested)
        {
            TryCompleteStop();
            return;
        }

        if (connectionMode == ConnectionMode.None)
            return;

        string errorMessage = isConnectionConfirmed
            ? "FishNet client connection was lost."
            : "FishNet client stopped before authentication completed.";

        ReportFailure(errorMessage);
    }

    private void HandleClientAuthenticated()
    {
        if (connectionMode == ConnectionMode.None)
            return;

        isClientAuthenticated = true;
        TryCompleteConnection();
    }

    private void HandleClientTimeout()
    {
        if (connectionMode == ConnectionMode.None || isStopRequested)
        {
            return;
        }

        ReportFailure("FishNet client connection timed out.");
    }

    private void TryCompleteConnection()
    {
        if (isConnectionConfirmed || hasReportedFailure || isStopRequested)
        {
            return;
        }

        switch (connectionMode)
        {
            case ConnectionMode.Host:
                if (!IsHostReady())
                    return;

                isConnectionConfirmed = true;
                HostStarted?.Invoke();
                break;

            case ConnectionMode.Client:
                if (!IsClientReady())
                    return;

                isConnectionConfirmed = true;
                ClientStarted?.Invoke();
                break;
        }
    }

    private bool IsHostReady()
    {
        return serverState == LocalConnectionState.Started &&
               clientState == LocalConnectionState.Started &&
               isClientAuthenticated;
    }

    private bool IsClientReady()
    {
        return clientState == LocalConnectionState.Started && isClientAuthenticated;
    }

    private bool CanStartConnection()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null.");
            return false;
        }

        if (fishySteamworks == null)
        {
            Debug.LogError("FishySteamworks is null.");
            return false;
        }

        if (!TrySubscribeToNetworkEvents())
        {
            Debug.LogError("FishNet managers are not initialized.");
            return false;
        }

        if (!IsConnectionActive())
            return true;

        Debug.LogWarning(
            "A FishNet connection operation is already active."
        );

        return false;
    }

    private bool IsConnectionActive()
    {
        return connectionMode != ConnectionMode.None ||
               serverState != LocalConnectionState.Stopped ||
               clientState != LocalConnectionState.Stopped ||
               IsServerManagerStarted() ||
               IsClientManagerStarted();
    }

    private bool IsServerManagerStarted()
    {
        ServerManager serverManager = subscribedServerManager;

        if (serverManager == null && networkManager != null)
            serverManager = networkManager.ServerManager;

        return serverManager != null && serverManager.Started;
    }

    private bool IsClientManagerStarted()
    {
        ClientManager clientManager = subscribedClientManager;

        if (clientManager == null && networkManager != null)
            clientManager = networkManager.ClientManager;

        return clientManager != null && clientManager.Started;
    }

    private void PrepareConnection(ConnectionMode newConnectionMode)
    {
        CancelStopTimeout();

        connectionMode = newConnectionMode;

        serverState = LocalConnectionState.Stopped;
        clientState = LocalConnectionState.Stopped;

        isClientAuthenticated = false;
        isConnectionConfirmed = false;
        hasReportedFailure = false;
        isStopRequested = false;
    }

    private void ReportFailure(string errorMessage)
    {
        if (hasReportedFailure || isStopRequested)
            return;

        hasReportedFailure = true;
        ConnectionFailed?.Invoke(errorMessage);
    }

    private void RequestTransportStop()
    {
        bool shouldStopClient = clientState != LocalConnectionState.Stopped || IsClientManagerStarted();

        bool shouldStopServer = serverState != LocalConnectionState.Stopped || IsServerManagerStarted();

        if (shouldStopClient)
            fishySteamworks.StopConnection(false);

        if (shouldStopServer)
            fishySteamworks.StopConnection(true);
    }

    private void TryCompleteStop()
    {
        bool connectionsAreStopped = serverState == LocalConnectionState.Stopped && clientState == LocalConnectionState.Stopped;

        if (!isStopRequested || !connectionsAreStopped)
            return;

        CancelStopTimeout();
        ResetConnectionTracking();

        ConnectionsStopped?.Invoke();
    }

    private void ForceCompleteStopTracking()
    {
        serverState = LocalConnectionState.Stopped;
        clientState = LocalConnectionState.Stopped;

        TryCompleteStop();
    }

    private void ResetConnectionTracking()
    {
        connectionMode = ConnectionMode.None;

        serverState = LocalConnectionState.Stopped;
        clientState = LocalConnectionState.Stopped;

        isClientAuthenticated = false;
        isConnectionConfirmed = false;
        hasReportedFailure = false;
        isStopRequested = false;
    }

    private void StartStopTimeout()
    {
        CancelStopTimeout();

        stopTimeoutCoroutine = StartCoroutine(HandleStopTimeout());
    }

    private IEnumerator HandleStopTimeout()
    {
        float timeout = Mathf.Max(1f, stopTimeoutSeconds);

        yield return new WaitForSecondsRealtime(timeout);

        stopTimeoutCoroutine = null;

        if (!isStopRequested)
            yield break;

        Debug.LogError(
            "FishNet connection stop timed out. " +
            "Connection tracking will be reset."
        );

        if (fishySteamworks != null)
        {
            fishySteamworks.StopConnection(false);
            fishySteamworks.StopConnection(true);
        }

        ForceCompleteStopTracking();
    }

    private void CancelStopTimeout()
    {
        if (stopTimeoutCoroutine == null)
            return;

        StopCoroutine(stopTimeoutCoroutine);
        stopTimeoutCoroutine = null;
    }

    private bool TrySubscribeToNetworkEvents()
    {
        if (networkEventsSubscribed)
            return true;

        if (networkManager == null)
            return false;

        ServerManager serverManager = networkManager.ServerManager;
        ClientManager clientManager = networkManager.ClientManager;

        if (serverManager == null || clientManager == null)
            return false;

        serverManager.OnServerConnectionState += HandleServerConnectionState;
        clientManager.OnClientConnectionState += HandleClientConnectionState;
        clientManager.OnAuthenticated += HandleClientAuthenticated;
        clientManager.OnClientTimeOut += HandleClientTimeout;

        subscribedServerManager = serverManager;
        subscribedClientManager = clientManager;
        networkEventsSubscribed = true;

        return true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!networkEventsSubscribed)
            return;

        if (subscribedServerManager != null)
        {
            subscribedServerManager.OnServerConnectionState -= HandleServerConnectionState;
        }

        if (subscribedClientManager != null)
        {
            subscribedClientManager.OnClientConnectionState -= HandleClientConnectionState;
            subscribedClientManager.OnAuthenticated -= HandleClientAuthenticated;
            subscribedClientManager.OnClientTimeOut -= HandleClientTimeout;
        }

        subscribedServerManager = null;
        subscribedClientManager = null;
        networkEventsSubscribed = false;
    }
}