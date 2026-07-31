using Steamworks;
using UnityEngine;
using UnityEngine.Events;

public class FishNetLobbyConnectionHandler : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private FishySteamworks.FishySteamworks fishySteamworks;

    [Header("Events")]
    [SerializeField] private UnityEvent onHostStarted;
    [SerializeField] private UnityEvent<EResult> onHostFailed;

    public bool TryStartHost()
    {
        if (fishySteamworks == null)
        {
            Debug.LogError("FishySteamworks is null.");
            return false;
        }

        // Host = servidor + cliente local.
        fishySteamworks.StartConnection(true);
        fishySteamworks.StartConnection(false);

        onHostStarted?.Invoke();

        return true;
    }

    public bool TryStartClient(string hostSteamId)
    {
        if (fishySteamworks == null)
        {
            Debug.LogError("FishySteamworks is null.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(hostSteamId))
        {
            Debug.LogError("The host's SteamID is not valid.");
            return false;
        }

        fishySteamworks.SetClientAddress(hostSteamId);
        fishySteamworks.StartConnection(false);

        return true;
    }

    public void HandleLobbyCreationFailed(EResult result)
    {
        StopConnections();
        onHostFailed?.Invoke(result);
    }

    public void StopConnections()
    {
        if (fishySteamworks == null)
            return;

        fishySteamworks.StopConnection(false);
        fishySteamworks.StopConnection(true);
    }
}