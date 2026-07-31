using System.Collections;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;

public class LocalNetworkLauncher : MonoBehaviour
{
    [SerializeField]
    private string gameScene = "GameScene";

    [SerializeField]
    private GameObject networkHud;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (networkHud != null)
            DontDestroyOnLoad(networkHud);
    }

    private void Start()
    {
        StartCoroutine(WaitForHostReady());
    }

    private void OnEnable()
    {
        InstanceFinder.ServerManager.OnServerConnectionState += OnServerState;
    }

    private void OnDisable()
    {
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerState;
    }

    private void OnServerState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Started)
            return;

        SceneLoadData loadData = new SceneLoadData(gameScene)
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(loadData);
    }

    private IEnumerator WaitForHostReady()
    {
        while (!InstanceFinder.IsServerStarted || !InstanceFinder.IsClientStarted)
            yield return null;

        yield return null;

        if (networkHud != null)
            networkHud.SetActive(false);

        Destroy(gameObject);
    }
}