using FishNet;
using UnityEngine;
using FishNet.Managing.Scened;

public class NetworkSceneChanger : MonoBehaviour
{
    [SerializeField] private string sceneToChangeName;

    public void ChangeScene()
    {
        if(string.IsNullOrEmpty(sceneToChangeName))
            return;

        SceneLoadData sld = new SceneLoadData(sceneToChangeName);
        sld.ReplaceScenes = ReplaceOption.All;
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }
}
