﻿using System;
using System.Globalization;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using Mute.Moe.AsyncEnumerable.Extensions;
using Mute.Moe.Discord.Attributes;
using Mute.Moe.Extensions;
using Mute.Moe.Services.Information.Cryptocurrency;
using Mute.Moe.Services.Information.Forex;
using Mute.Moe.Services.Information.Stocks;
using Mute.Moe.Utilities;

namespace Mute.Moe.Discord.Modules.Search
{
    [ThinkingReply]
    public class Finance
        : BaseModule
    {
        private readonly ICryptocurrencyInfo _crypto;
        private readonly IStockQuotes _stocks;
        private readonly IForexInfo _forex;
        private readonly IStockSearch _search;
        private readonly Random _random;

        public Finance(ICryptocurrencyInfo crypto, IStockQuotes stocks, IForexInfo forex, IStockSearch search, Random random)
        {
            _crypto = crypto;
            _stocks = stocks;
            _forex = forex;
            _search = search;
            _random = random;
        }

        [Command("ticker"), Summary("I will find out information about a stock or currency")]
        public async Task Ticker([NotNull] string symbolOrName, string quote = "USD")
        {
            if (await TickerAsCrypto(symbolOrName, quote))
                return;

            if (await TickerAsForex(symbolOrName, quote))
                return;

            if (await TickerAsStock(symbolOrName))
                return;

            var reply = $"I can't find a crypto, currency or stock with the symbol `{symbolOrName}`. ";

            var suggestions = await _search
                                    .Search(symbolOrName)
                                    .Select(a => $" • {a.Name} (`{a.Symbol}`)")
                                    .Take(10)
                                    .ToArray();
            if (suggestions.Length > 0)
                reply += "Did you mean one of these stocks:\n" + string.Join("\n", suggestions);
            else if (_random.NextDouble() < 0.25f)
                reply += new[] { EmojiLookup.Confused, EmojiLookup.Crying, EmojiLookup.Pensive, EmojiLookup.SlightlyFrowning, EmojiLookup.Thinking, EmojiLookup.Unamused, EmojiLookup.Worried }.Random(_random);

            await TypingReplyAsync(reply);
        }

        private async Task<bool> TickerAsStock(string symbolOrName)
        {
            var result = await _stocks.GetQuote(symbolOrName);

            if (result != null)
            {
                var change = "";
                var delta = result.Price - result.Open;
                if (delta > 0)
                    change += "up ";
                else
                    change += "down";
                change += $"{(delta / result.Price):P}";

                await TypingReplyAsync($"{result.Symbol} is trading at {result.Price:0.00}, {change} since opening today");
                return true;
            }

            return false;
        }

        private async Task<bool> TickerAsForex(string symbolOrName, string quote)
        {
            var result = await _forex.GetExchangeRate(symbolOrName, quote);

            if (result != null)
            {
                await TypingReplyAsync($"{result.FromName} ({result.FromCode}) is worth {result.ToCode.TryGetCurrencySymbol()}{result.ExchangeRate.ToString("0.00", CultureInfo.InvariantCulture)}");
                return true;
            }

            return false;
        }

        private async Task<bool> TickerAsCrypto([NotNull] string symbolOrName, string quote)
        {
            //Try to parse the sym/name as a cryptocurrency
            var currency = await _crypto.FindBySymbolOrName(symbolOrName);
            if (currency == null)
                return false;

            var ticker = await _crypto.GetTicker(currency, quote);

            string reply;
            if (ticker == null)
            {
                reply = $"{currency.Name} ({currency.Symbol}) doesn't seem to have any price information associated with it";
            }
            else
            {
                reply = $"{ticker.Currency.Name} ({ticker.Currency.Symbol})";

                //Try to find quote in selected currency, if not then default to USD
                if (!ticker.Quotes.TryGetValue(quote.ToUpperInvariant(), out var val) && quote != "USD")
                {
                    await TypingReplyAsync($"I'm not sure what the value is in '{quote.ToUpperInvariant()}', I'll try 'USD' instead");

                    quote = "USD";
                    ticker.Quotes.TryGetValue(quote, out val);
                }

                //Format the value part of the quote
                if (val != null)
                {
                    var price = val.Price.ToString("0.00", CultureInfo.InvariantCulture);
                    reply += $" is worth {quote.TryGetCurrencySymbol().ToUpperInvariant()}{price}";

                    if (val.PctChange24H.HasValue)
                    {
                        if (val.PctChange24H > 0)
                            reply += " (up";
                        else
                            reply += " (down";

                        reply += $" {val.PctChange24H}% in 24H)";
                    }
                }
            }

            await TypingReplyAsync(reply);
            return true;
        }
    }
}
