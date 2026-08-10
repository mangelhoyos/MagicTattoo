using FishNet;
using UnityEngine;
using FishNet.Managing.Scened;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public class NetworkSceneChanger : MonoBehaviour
{
    [SerializeField] private string sceneToChangeName;

    public void ChangeScene()
    {
        if (string.IsNullOrEmpty(sceneToChangeName))
            return;

        SceneLoadData sld = new SceneLoadData(sceneToChangeName);
        sld.ReplaceScenes = ReplaceOption.All;

        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }

    public void ChangeSceneLocal()
    {
        if (string.IsNullOrWhiteSpace(sceneToChangeName))
            return;

        UnitySceneManager.LoadScene(sceneToChangeName);
    }
}