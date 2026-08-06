using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSteamUIHandler : MonoBehaviour
{
    [SerializeField] PlayerSteamDataHolder playerSteamDataHolder;
    [SerializeField] TMP_Text playerNameTextField;
    [SerializeField] RawImage playerAvatarImage;

    private void Awake()
    {
        SetUIToDefault();
    }

    private void OnEnable()
    {
        playerSteamDataHolder.OnOwnerSteamDataRetrieved += HandleSteamDataRetrieved;
    }

    private void OnDisable()
    {
        playerSteamDataHolder.OnOwnerSteamDataRetrieved -= HandleSteamDataRetrieved;
    }

    void SetUIToDefault()
    {
        playerNameTextField.text = string.Empty;
        playerAvatarImage.enabled = false;
    }

    private void HandleSteamDataRetrieved(SteamUserData steamUserData)
    {
        playerNameTextField.text = steamUserData.userName;
        playerAvatarImage.texture = steamUserData.avatarTexture;
        playerAvatarImage.enabled = true;
    }
}
