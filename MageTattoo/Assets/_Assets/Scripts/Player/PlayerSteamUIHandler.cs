using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSteamUIHandler : NetworkBehaviour
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
        playerSteamDataHolder.OnSteamDataRetrieved += HandleSteamDataRetrieved;

        //Check initial values for new players
        SteamUserData data = playerSteamDataHolder.GetPlayerInfo();

        if (data.userId != 0 && data.avatarTexture != null)
            HandleSteamDataRetrieved(data);
    }

    private void OnDisable()
    {
        playerSteamDataHolder.OnSteamDataRetrieved -= HandleSteamDataRetrieved;
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
