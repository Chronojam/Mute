﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Steam.Models.SteamCommunity;
using Steam.Models.SteamPlayer;
using Steam.Models.SteamStore;
using SteamWebAPI2.Interfaces;

namespace Mute.Moe.Services.Information.Steam
{
    public class SteamApi
        : ISteamInfo
    {
        private readonly SteamUser _user;
        private readonly SteamStore _store;
        private readonly SteamUserStats _stats;

        public SteamApi([NotNull] Configuration config)
        {
            _user = new SteamUser(config.Steam.WebApiKey);
            _store = new SteamStore();
            _stats = new SteamUserStats(config.Steam.WebApiKey);
        }

        public async Task<PlayerSummaryModel> GetUserSummary(ulong userSteamId)
        {
            var response = await _user.GetPlayerSummaryAsync(userSteamId);
            return response.Data;
        }

        public async Task<SteamCommunityProfileModel> GetUserCommunityProfile(ulong userSteamId)
        {
            var response = await _user.GetCommunityProfileAsync(userSteamId);
            return response;
        }

        public async Task<UserStatsForGameResultModel> GetUserStatsForGame(ulong userSteamId, uint appId)
        {
            var result = await _stats.GetUserStatsForGameAsync(userSteamId, appId);
            return result.Data;
        }

        public async Task<StoreAppDetailsDataModel> GetStoreAppDetail(uint appId)
        {
            var response = await _store.GetStoreAppDetailsAsync(appId);
            return response;
        }

        public async Task<uint> GetCurrentPlayerCount(uint appId)
        {
            var response = await _stats.GetNumberOfCurrentPlayersForGameAsync(appId);
            return response.Data;
        }
    }
}
