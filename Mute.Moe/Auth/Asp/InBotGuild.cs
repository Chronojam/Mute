﻿using System.Threading.Tasks;
using Discord.WebSocket;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Mute.Moe.Extensions;

namespace Mute.Moe.Auth.Asp
{
    public class InBotGuildRequirement
        : IAuthorizationRequirement
    {
    }

    public class InBotGuildRequirementHandler
        : AuthorizationHandler<InBotGuildRequirement>
    {
        private readonly DiscordSocketClient _client;

        public InBotGuildRequirementHandler(DiscordSocketClient client)
        {
            _client = client;
        }

        [NotNull] protected override async Task HandleRequirementAsync([NotNull] AuthorizationHandlerContext context, InBotGuildRequirement requirement)
        {
            if (await context.User.IsInBotGuild(_client))
                context.Succeed(requirement);
        }
    }
}
