using System;
using UnityEngine;
using FishNet.Managing;
using FishNet.Transporting;

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

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    // Inicia el servidor y el cliente local solicitados por el coordinador.
    public bool TryStartHost()
    {
        if (!CanStartConnection())
            return false;

        PrepareConnection(ConnectionMode.Host);

        serverState = LocalConnectionState.Starting;
        clientState = LocalConnectionState.Starting;

        fishySteamworks.StartConnection(true);
        fishySteamworks.StartConnection(false);

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
        fishySteamworks.StartConnection(false);

        return true;
    }

    // Detiene las conexiones activas y notifica al coordinador al finalizar.
    public void StopConnections()
    {
        isStopRequested = true;

        if (fishySteamworks == null)
        {
            serverState = LocalConnectionState.Stopped;
            clientState = LocalConnectionState.Stopped;

            TryCompleteStop();
            return;
        }

        if (clientState != LocalConnectionState.Stopped)
            fishySteamworks.StopConnection(false);

        if (serverState != LocalConnectionState.Stopped)
            fishySteamworks.StopConnection(true);

        TryCompleteStop();
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

    private void HandleClientConnectionState(
        ClientConnectionStateArgs args)
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
            return;

        ReportFailure("FishNet client connection timed out.");
    }

    private void TryCompleteConnection()
    {
        if (isConnectionConfirmed ||
            hasReportedFailure ||
            isStopRequested)
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
        return clientState == LocalConnectionState.Started &&
               isClientAuthenticated;
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
               clientState != LocalConnectionState.Stopped;
    }

    private void PrepareConnection(ConnectionMode newConnectionMode)
    {
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

    private void TryCompleteStop()
    {
        bool connectionsAreStopped =
            serverState == LocalConnectionState.Stopped &&
            clientState == LocalConnectionState.Stopped;

        if (!isStopRequested || !connectionsAreStopped)
            return;

        ResetConnectionTracking();

        ConnectionsStopped?.Invoke();
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

    private void SubscribeToNetworkEvents()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null.");
            return;
        }

        networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
        networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
        networkManager.ClientManager.OnAuthenticated += HandleClientAuthenticated;
        networkManager.ClientManager.OnClientTimeOut += HandleClientTimeout;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (networkManager == null)
            return;

        networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
        networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
        networkManager.ClientManager.OnAuthenticated -= HandleClientAuthenticated;
        networkManager.ClientManager.OnClientTimeOut -= HandleClientTimeout;
    }
}