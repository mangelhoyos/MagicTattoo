using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using HeathenEngineering.SteamworksIntegration;

public class LobbyConnectionManager : MonoBehaviour
{
    [SerializeField] private FishySteamworks.FishySteamworks fishySteamworks;
    [SerializeField] private LobbyManager lobbyManager;

    [SerializeField] private UnityEvent onHostStarted;
    [SerializeField] private UnityEvent<EResult> onHostFailed;

    private void OnEnable()
    {
        if (lobbyManager != null)
        {
            lobbyManager.evtCreated.AddListener(OnLobbyCreatedSuccessfully);
            lobbyManager.evtCreateFailed.AddListener(OnLobbyCreationFailed);

            lobbyManager.evtEnterSuccess.AddListener(OnEnteredLobby);
        }
    }

    private void OnDisable()
    {
        if (lobbyManager != null)
        {
            lobbyManager.evtCreated.RemoveListener(OnLobbyCreatedSuccessfully);
            lobbyManager.evtCreateFailed.RemoveListener(OnLobbyCreationFailed);
            lobbyManager.evtEnterSuccess.RemoveListener(OnEnteredLobby);
        }
    }

    //HOST

    public void StartHost()
    {
        lobbyManager.Create();
    }

    private void OnLobbyCreatedSuccessfully(LobbyData lobby)
    {
        fishySteamworks.StartConnection(true);
        fishySteamworks.StartConnection(false);

        lobby["HostSteamID"] = SteamUser.GetSteamID().ToString();

        onHostStarted?.Invoke();
    }

    private void OnLobbyCreationFailed(EResult result)
    {
        fishySteamworks.StopConnection(true);
        fishySteamworks.StopConnection(false);

        onHostFailed?.Invoke(result);
    }

    // LOGICA DE UNIRSE A LOBBY

    public void JoinRequestedLobby(LobbyData targetLobby)
    {
        lobbyManager.Join(targetLobby);
    }

    private void OnEnteredLobby(LobbyData lobby)
    {
        if (!lobby.IsOwner)
        {
            string hostSteamID = lobby["HostSteamID"];

            fishySteamworks.SetClientAddress(hostSteamID);

            fishySteamworks.StartConnection(false);
        }
    }
}