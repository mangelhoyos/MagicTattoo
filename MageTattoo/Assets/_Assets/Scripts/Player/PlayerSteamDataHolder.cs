using FishNet.Object;
using FishNet.Object.Synchronizing;
using HeathenEngineering.SteamworksIntegration;
using Steamworks;
using System;
using UnityEngine;

public class PlayerSteamDataHolder : NetworkBehaviour
{
    public event Action<SteamUserData> OnOwnerSteamDataRetrieved;

    private const int MAXUSERNAMELENGTH = 64;

    private readonly SyncVar<SteamUserNetworkData> _steamUserData = new();

    private bool _clientStarted;

    public SteamUserData PlayerData { get; private set; }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _clientStarted = true;
        _steamUserData.OnChange += OnSteamDataChanged;

        if (IsOwner)
            SendLocalSteamData();
    }

    public override void OnStopClient()
    {
        _clientStarted = false;

        _steamUserData.OnChange -= OnSteamDataChanged;

        base.OnStopClient();
    }

    private void SendLocalSteamData()
    {
        try
        {
            SteamUserNetworkData data = new()
            {
                userName = UserData.Me.Name,
                userId = UserData.Me.id.m_SteamID
            };

            if (data.userId == 0 || string.IsNullOrWhiteSpace(data.userName))
                return;

            SetSteamDataServerRpc(data);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to retrieve Steam user data: {exception}");
        }
    }

    [ServerRpc]
    private void SetSteamDataServerRpc(SteamUserNetworkData data)
    {
        if (_steamUserData.Value.userId != 0)
            return;

        if (data.userId == 0)
            return;

        if (string.IsNullOrWhiteSpace(data.userName))
            return;

        data.userName = data.userName.Trim();

        if (data.userName.Length > MAXUSERNAMELENGTH)
            data.userName = data.userName[..MAXUSERNAMELENGTH];

        _steamUserData.Value = data;
    }

    private void OnSteamDataChanged(SteamUserNetworkData previous, SteamUserNetworkData current, bool asServer)
    {
        if (asServer)
            return;

        if (current.userId == 0 || string.IsNullOrWhiteSpace(current.userName))
            return;

        ulong requestedUserId = current.userId;

        UserData.Get(new CSteamID(requestedUserId)).LoadAvatar(texture =>
        {
            if (!_clientStarted || this == null)
                return;

            if (_steamUserData.Value.userId != requestedUserId)
                return;

            PlayerData = new SteamUserData
            {
                userName = current.userName,
                userId = requestedUserId,
                avatarTexture = texture
            };

            if (IsOwner)
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