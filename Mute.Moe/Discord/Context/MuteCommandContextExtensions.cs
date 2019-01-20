﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Mute.Moe.Services;
using Mute.Moe.Services.Sentiment;

namespace Mute.Moe.Discord.Context
{
    public static class MuteCommandContextExtensions
    {
        public static async Task<SentimentResult> Sentiment([NotNull] this MuteCommandContext context)
        {
            var r = await context.GetOrAdd(async () => {
                var sentiment = (ISentimentService)context.Services.GetService(typeof(ISentimentService));
                return new SentimentResultContainer(await sentiment.Predict(context.Message.Content));
            });

            return r.Result;
        }

        private class SentimentResultContainer
        {
            public readonly SentimentResult Result;

            public SentimentResultContainer(SentimentResult result)
            {
                Result = result;
            }
        }
    }
}