﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using JetBrains.Annotations;
using Mute.Moe.Extensions;

namespace Mute.Moe.Services.Reminders
{
    public class AsyncReminderSender
        : IReminderSender
    {
        private readonly IReminders _reminders;
        private readonly DiscordSocketClient _client;

        // ReSharper disable once NotAccessedField.Local
        private Task _thread;

        public AsyncReminderSender(IReminders reminders, DiscordSocketClient client)
        {
            _reminders = reminders;
            _client = client;

            _thread = Task.Run(ThreadEntry);
        }

        private async Task ThreadEntry()
        {
            try
            {
                while (true)
                {
                    var next = await _reminders.Get(after: DateTime.UtcNow, count: 1).FirstOrDefault();

                    //Wait for one of these events to happen
                    var cts = new CancellationTokenSource();
                    var evt = await await Task.WhenAny(
                        WaitForCreation(cts.Token),
                        WaitForDeletion(cts.Token),
                        WaitForTimeout(cts.Token, next)
                    );

                    //cancel all the others
                    cts.Cancel();

                    //Run whichever one completed
                    await evt.Run(ref next);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Notification service killed! Exception: {0}", e);
            }
        }

        [ItemNotNull] private async Task<BaseEventAction> WaitForCreation(CancellationToken ct)
        {
            //Create a task which will complete when a new reminder is created
            var tcs = new TaskCompletionSource<IReminder>();
            _reminders.ReminderCreated += tcs.SetResult;

            //If the wait task is cancelled cancel the inner task and unregister the event handler
            ct.Register(() => tcs.TrySetCanceled());
            ct.Register(() => _reminders.ReminderCreated -= tcs.SetResult);

            //Now wait for something to happen...
            var reminder = await tcs.Task;

            //If something happened, return the reminder
            return new EventCreatedAction(reminder);
        }

        [ItemNotNull] private async Task<BaseEventAction> WaitForDeletion(CancellationToken ct)
        {
            //Create a task which will complete when a reminder is deleted
            var tcs = new TaskCompletionSource<uint>();
            _reminders.ReminderDeleted += tcs.SetResult;

            //If the wait task is cancelled cancel the inner task and unregister the event handler
            ct.Register(() => tcs.TrySetCanceled());
            ct.Register(() => _reminders.ReminderDeleted -= tcs.SetResult);

            //Now wait for something to happen...
            var id = await tcs.Task;

            //If something happened, return the reminder
            return new EventDeletedAction(id);
        }

        [ItemNotNull] private async Task<BaseEventAction> WaitForTimeout(CancellationToken ct, [CanBeNull] IReminder next)
        {
            //If there is no next event then just hang forever
            if (next == null)
            {
                while (!ct.IsCancellationRequested)
                    await Task.Yield();
                return await Task.FromCanceled<BaseEventAction>(ct);
            }
            else
            {
                //Wait for this reminder to time out
                await Task.Delay(next.TriggerTime - DateTime.UtcNow, ct);

                return new EventTimeoutAction(next, _reminders, _client);
            }
        }

        private abstract class BaseEventAction
        {
            [NotNull] public abstract Task Run([CanBeNull] ref IReminder next);
        }

        private class EventCreatedAction
            : BaseEventAction
        {
            private readonly IReminder _reminder;

            public EventCreatedAction([NotNull] IReminder reminder)
            {
                _reminder = reminder;
            }

            public override Task Run(ref IReminder next)
            {
                if (next == null || _reminder.TriggerTime < next.TriggerTime)
                    next = _reminder;

                return Task.CompletedTask;
            }
        }

        private class EventDeletedAction
            : BaseEventAction
        {
            private readonly uint _id;

            public EventDeletedAction(uint id)
            {
                _id = id;
            }

            public override Task Run(ref IReminder next)
            {
                if (_id == next?.ID)
                    next = null;
                return Task.CompletedTask;
            }
        }

        private class EventTimeoutAction
            : BaseEventAction
        {
            private readonly IReminder _reminder;
            private readonly IReminders _reminders;
            private readonly DiscordSocketClient _client;

            public EventTimeoutAction(IReminder reminder, IReminders reminders, DiscordSocketClient client)
            {
                _reminder = reminder;
                _reminders = reminders;
                _client = client;
            }

            public override Task Run(ref IReminder _)
            {
                return Task.Run(async () =>
                {
                    if (_client.GetChannel(_reminder.ChannelId) is ITextChannel channel)
                    {
                        if (!string.IsNullOrWhiteSpace(_reminder.Prelude))
                            await channel.SendMessageAsync(_reminder.Prelude);

                        string name = null;
                        if (channel.Guild != null)
                        {
                            var user = (await channel.Guild.GetUserAsync(_reminder.UserId));
                            name = user.Nickname ?? user.Username;
                        }
                        else
                        {
                            var user = _client.GetUser(_reminder.UserId);
                            name = user.Username;
                        }

                        var embed = new EmbedBuilder()
                            .WithDescription(_reminder.Message)
                            .WithAuthor(name)
                            .WithFooter(new FriendlyId32(_reminder.ID).ToString());

                        await channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                    {
                        Console.WriteLine($"Cannot send reminder: Channel `{_reminder.ChannelId}` is null");
                    }

                    await _reminders.Delete(_reminder.ID);
                });
            }
        }
    }
}
