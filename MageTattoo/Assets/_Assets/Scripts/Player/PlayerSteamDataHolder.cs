using FishNet.Object;
using FishNet.Object.Synchronizing;
using HeathenEngineering.SteamworksIntegration;
using Steamworks;
using System;
using UnityEngine;

public class PlayerSteamDataHolder : NetworkBehaviour
{
    public event Action<SteamUserData> OnOwnerSteamDataRetrieved;

    private readonly SyncVar<SteamUserNetworkData> _steamUserData = new();

    public SteamUserData PlayerData { get; private set; }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _steamUserData.OnChange += OnSteamDataChanged;

        if (IsOwner)
            SendLocalSteamData();
    }

    public override void OnStopClient()
    {
        _steamUserData.OnChange -= OnSteamDataChanged;

        base.OnStopClient();
    }

    private void SendLocalSteamData()
    {
        try
        {
            SteamUserNetworkData data = new SteamUserNetworkData
            {
                userName = UserData.Me.Name,
                userId = UserData.Me.id.m_SteamID
            };

            SetSteamDataServerRpc(data);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to retrieve Steam user data: {exception}");
        }
    }

    [ServerRpc]
    private void SetSteamDataServerRpc(SteamUserNetworkData data)
    {
        _steamUserData.Value = data;
    }

    private void OnSteamDataChanged(
        SteamUserNetworkData previous,
        SteamUserNetworkData current,
        bool asServer)
    {
        if (asServer)
            return;

        PlayerData = new SteamUserData
        {
            userName = current.userName,
            userId = current.userId,
            avatarTexture = null
        };

        CSteamID steamId = new CSteamID(current.userId);

        UserData.Get(steamId).LoadAvatar(texture =>
        {
            PlayerData = new SteamUserData
            {
                userName = current.userName,
                userId = current.userId,
                avatarTexture = texture
            };
            
            if(IsOwner)
                OnOwnerSteamDataRetrieved?.Invoke(PlayerData);
        });
    }

    public SteamUserData GetPlayerInfo()
    {
        return PlayerData;
    }
}

public struct SteamUserNetworkData
{
    public string userName;
    public ulong userId;
}

public struct SteamUserData
{
    public string userName;
    public ulong userId;
    public Texture2D avatarTexture;
}