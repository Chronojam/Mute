﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mute.Moe.Discord.Context;
using Mute.Moe.Discord.Services.Responses;

namespace Mute.Moe.Discord
{
    public class HostedDiscordBot
        : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly Configuration _config;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public HostedDiscordBot(DiscordSocketClient client, Configuration config, CommandService commands, IServiceProvider services)
        {
            _client = client;
            _config = config;
            _commands = commands;
            _services = services;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Discover all of the commands in this assembly and load them.
            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
            Console.WriteLine($"Loaded Modules ({_commands.Modules.Count()}):");
            foreach (var module in _commands.Modules)
                Console.WriteLine($" - {module.Name}");

            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleMessage;

            // Log the bot in
            await _client.LogoutAsync();
            await _client.LoginAsync(TokenType.Bot, _config.Auth.Token);
            await _client.StartAsync();

            // Set presence
            if (Debugger.IsAttached)
            {
                await _client.SetActivityAsync(new Game("Debug Mode"));
                await _client.SetStatusAsync(UserStatus.DoNotDisturb);
            }
            else
            {
                await _client.SetActivityAsync(null);
                await _client.SetStatusAsync(UserStatus.Online);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.LogoutAsync();
            await _client.StopAsync();
        }

        [NotNull] private async Task HandleMessage([NotNull] SocketMessage socketMessage)
        {
            // Don't process the command if it was a System Message
            if (!(socketMessage is SocketUserMessage message))
                return;

            //Ignore messages from self
            if (message.Author.Id == _client.CurrentUser.Id && !_config.ProcessMessagesFromSelf)
                return;

            // Check if the message starts with the command prefix character
            var prefixPos = 0;
            var hasPrefix = message.HasCharPrefix(_config.PrefixCharacter, ref prefixPos);

            // Create a context for this message
            var context = new MuteCommandContext(_client, message, _services);

            //Apply generic message preproccessor
            foreach (var pre in _services.GetServices<IMessagePreprocessor>())
                pre.Process(context);

            //Either process as command or try to process conversationally
            if (hasPrefix)
            {
                foreach (var pre in _services.GetServices<ICommandPreprocessor>())
                    pre.Process(context);
                await ProcessAsCommand(prefixPos, context);
            }
            else
            {
                foreach (var pre in _services.GetServices<IConversationPreprocessor>())
                    pre.Process(context);
                await _services.GetService<ConversationalResponseService>().Respond(context);
            }
        }

        private async Task ProcessAsCommand(int offset, [NotNull] MuteCommandContext context)
        {
            // When there's a mention the command may or may not include the prefix. Check if it does include it and skip over it if so
            if (context.Message.Content[offset] == _config.PrefixCharacter)
                offset++;

            // Execute the command
            try
            {
                foreach (var pre in _services.GetServices<ICommandPreprocessor>())
                    pre.Process(context);

                var result = await _commands.ExecuteAsync(context, offset, _services);

                //Don't print error message in response to messages from self
                if (!result.IsSuccess && context.User.Id != _client.CurrentUser.Id)
                    await context.Channel.SendMessageAsync(result.ErrorReason);

                if (result.ErrorReason != null)
                    Console.WriteLine(result.ErrorReason);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new CommandService(new CommandServiceConfig {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
                ThrowOnError = true
            }));

            var client = new DiscordSocketClient(new DiscordSocketConfig {
                AlwaysDownloadUsers = true
            });

            services.AddSingleton(client);
            services.AddSingleton<IDiscordClient>(client);
        }
    }
}