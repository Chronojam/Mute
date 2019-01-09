﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Mute.Context;
using Mute.Extensions;
using IEnumerableExtensions = Mute.Extensions.IEnumerableExtensions;

namespace Mute.Services.Responses
{
    public class ConversationalResponseService
    {
        private readonly DiscordSocketClient _client;
        private readonly Random _random;
        private readonly List<IResponse> _responses = new List<IResponse>();

        private readonly ConcurrentDictionary<IUser, IConversation> _conversations = new ConcurrentDictionary<IUser, IConversation>();

        public ConversationalResponseService(DiscordSocketClient client, IServiceProvider services, Random random)
        {
            _client = client;
            _random = random;

            //Create response generators
            _responses.AddRange(from t in Assembly.GetExecutingAssembly().GetTypes()
                                where t.IsClass
                                where typeof(IResponse).IsAssignableFrom(t)
                                let i = ActivatorUtilities.CreateInstance(services, t) as IResponse
                                where i != null
                                select i);

            Console.WriteLine($"Loaded Response Generators ({_responses.Count}):");
            foreach (var response in _responses)
                Console.WriteLine($" - {response.GetType().Name}");
        }

        public async Task Respond([NotNull] MuteCommandContext context)
        {
            // Check if the bot is directly mentioned
            var mentionsBot = ((IMessage)context.Message).MentionedUserIds.Contains(_client.CurrentUser.Id);

            //Try to get a conversation with this user (either continued from before, or starting with a new one)
            var c = await GetOrCreateConversation(context, mentionsBot);

            //If we have a conversation, try to reply to this message
            if (c != null)
            {
                var r = await c.Respond(context, mentionsBot, CancellationToken.None);
                if (r != null)
                    await context.Channel.TypingReplyAsync(r);
            }
        }

        private async Task<IConversation> GetOrCreateConversation([NotNull] MuteCommandContext context, bool mentionsBot)
        {
            //Create a new conversation starting with this message
            var newConv = await TryCreateConversation(context, mentionsBot);

            //Use the existing conversation if it is not over, or else replace it with the new conversation
            return _conversations.AddOrUpdate(
                context.User,
                _ => newConv,
                (_, c) => (c?.IsComplete ?? true) ? newConv : c
            );
        }

        [ItemCanBeNull] private async Task<IConversation> TryCreateConversation([NotNull] MuteCommandContext context, bool mentionsBot)
        {
            //Find generators which can respond to this message
            var random = new Random(context.Message.Id.GetHashCode());
            var candidates = new List<IConversation>();
            foreach (var generator in _responses.AsParallel())
            {
                var conversation = await generator.TryRespond(context, mentionsBot);
                if (conversation == null)
                    continue;

                var rand = random.NextDouble();
                if ((mentionsBot && rand < generator.MentionedChance) || (!mentionsBot && rand < generator.BaseChance))
                    candidates.Add(conversation);
            }

            //If there are several pick a random one
            return IEnumerableExtensions.Random(candidates, _random);
        }

        public IConversation GetConversation(IGuildUser user)
        {
            if (_conversations.TryGetValue(user, out var conversation))
                return conversation;
            else
                return null;
        }
    }
}
