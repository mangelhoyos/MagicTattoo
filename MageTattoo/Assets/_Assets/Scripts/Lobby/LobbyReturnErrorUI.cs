using UnityEngine;

public class LobbyReturnErrorUI : MonoBehaviour
{
    [SerializeField] private GameObject connectionErrorPanel;

    private void Start()
    {
        if (connectionErrorPanel == null)
        {
            Debug.LogError("Connection error panel is not assigned.");
            return;
        }

        connectionErrorPanel.SetActive(
            LobbyReturnContext.ConsumeConnectionError()
        );
    }
}