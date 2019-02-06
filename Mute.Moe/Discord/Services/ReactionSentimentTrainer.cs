﻿using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using JetBrains.Annotations;
using Mute.Moe.Discord.Services.Responses;
using Mute.Moe.Services.Sentiment;
using Mute.Moe.Services.Sentiment.Training;

namespace Mute.Moe.Discord.Services
{
    public class ReactionSentimentTrainer
    {
        private readonly ISentimentTrainer _sentiment;

        public ReactionSentimentTrainer([NotNull] DiscordSocketClient client, ISentimentTrainer sentiment)
        {
            _sentiment = sentiment;

            client.ReactionAdded += OnReactionAdded;
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, [NotNull] ISocketMessageChannel channel, [NotNull] SocketReaction reaction)
        {
            if (SentimentResponse.Happy.Contains(reaction.Emote.Name))
                await TryLearn(await message.DownloadAsync(), reaction, Sentiment.Positive);
            else if (SentimentResponse.Sad.Contains(reaction.Emote.Name))
                await TryLearn(await message.DownloadAsync(), reaction, Sentiment.Negative);
            else if (SentimentResponse.Neutral.Contains(reaction.Emote.Name))
                await TryLearn(await message.DownloadAsync(), reaction, Sentiment.Neutral);
        }

        private async Task TryLearn([NotNull] IUserMessage message, [NotNull] IReaction reaction, Sentiment sentiment)
        {
            var gc = (SocketGuildChannel)message.Channel;
            var g = gc.Guild;

            var users = (await (message.GetReactionUsersAsync(reaction.Emote, 128).Flatten().ToArray()))   //Get users who reacted
                        .Select(u => u as IGuildUser ?? g.GetUser(u.Id))        //Convert them to guild users
                        .Where(u => u != null)
                        .GroupBy(a => a.Id)                                     //GroupBy ID to deduplicate users who reacted multiple times
                        .Select(a => a.First())
                        .ToArray();

            if (users.Length >= 3 || users.Any(IsTeacher))
                await _sentiment.Teach(message.Content, sentiment);
        }

        private bool IsTeacher([NotNull] IGuildUser user)
        {
            //Check if the user has the `*MuteTeacher` role (hardcoded ID for now)
            return user.RoleIds.Contains<ulong>(506127510740795393);

            ////For now hardcode it to Nyarlathothep only, in the future make this role based
            //return user.Id == 103509816437149696;
        }
    }
}
