﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using Mute.Moe.AsyncEnumerable.Extensions;
using Mute.Moe.Discord.Attributes;
using Mute.Moe.Extensions;
using Mute.Moe.Services.Audio.Sources.Youtube;
using Mute.Moe.Services.Music;
using Mute.Moe.Services.Music.Extensions;
using NAudio.Wave;

namespace Mute.Moe.Discord.Modules.Audio
{
    [HelpGroup("music")]
    [Group("music")]
    [RequireContext(ContextType.Guild)]
    public class Music
        : BaseModule
    {
        private readonly IYoutubeDownloader _youtube;
        private readonly IMusicLibrary _library;
        private readonly Random _rng;
        private readonly IGuildMusicQueueCollection _queueCollection;

        public Music(IGuildMusicQueueCollection queueCollection, IYoutubeDownloader youtube, IMusicLibrary library, Random rng)
        {
            _queueCollection = queueCollection;
            _youtube = youtube;
            _library = library;
            _rng = rng;
        }

        [NotNull, Command("playing"), Summary("Get information about the currently playing track")]
        public async Task NowPlaying()
        {
            var q = await _queueCollection.Get(Context.Guild.Id);

            if (!q.IsPlaying)
            {
                await ReplyAsync("Nothing is currently playing");
                return;
            }

            var (metadata, completion) = q.Playing;
            var embed = (await metadata.DiscordEmbed());

            //Show embed with green border (indicates it is playing)
            var message = await ReplyAsync(embed.WithColor(Color.Green));

            //Wait for track to finish
            await completion;

            //Change embed colour
            await message.ModifyAsync(a => a.Embed = embed.WithColor(Color.DarkPurple).Build());
        }

        [NotNull, Command("stop"), Summary("Clear the music queue")]
        [RequireVoiceChannel]
        public async Task ClearQueue()
        {
            var q = await _queueCollection.Get(Context.Guild.Id);
            q.Stop();
        }

        [NotNull, Command("skip"), Summary("Skip the currently playing track")]
        [RequireVoiceChannel]
        public async Task SkipTrack()
        {
            var q = await _queueCollection.Get(Context.Guild.Id);
            q.Skip();
        }

        [NotNull, Command("playlist"), Summary("Show the current playlist")]
        [RequireVoiceChannel]
        public async Task PlayList()
        {
            // Get music queue
            var channel = await _queueCollection.Get(Context.Guild.Id);

            // Early out for empty queue
            var q = channel.Queue.ToArray();
            if (q.Length == 0)
            {
                await ReplyAsync("There is nothing in the play queue");
                return;
            }

            // Display entire queue
            await DisplayItemList(
                q,
                () => "There is nothing in the play queue",
                xs => $"There are {xs.Count} tracks in the playlist:",
                TrackString
            );
        }

        [NotNull, Command("play-random"), Summary("Play a random track from the library")]
        [RequireVoiceChannel]
        [ThinkingReply]
        public async Task PlayRandom()
        {
            // Pick a track
            var track = await (await _library.Get(Context.Guild.Id, order: TrackOrder.Random, limit: 1)).SingleOrDefault();
            if (track == null)
            {
                await ReplyAsync("Failed to find a single random track in the library!");
                return;
            }

            await Enqueue(track);
        }

        [NotNull, Command("play-url"), Summary("Play a track from a given URL")]
        [RequireVoiceChannel]
        [ThinkingReply]
        public async Task PlayUrl([NotNull] string url)
        {
            // Tolerate misusing the play command to invoke `play-random`
            if (url.ToLowerInvariant() == "random")
            {
                await PlayRandom();
                return;
            }

            // Try to find this item in the library
            var match = await (await _library.Get(Context.Guild.Id, url: url.ToLowerInvariant())).SingleOrDefault();
            if (match != null)
            {
                await Enqueue(match);
                return;
            }

            // Try to download this URL using one of the downloaders we know of
            if (await _youtube.IsValidUrl(url))
            {
                var ytd = await _youtube.DownloadAudio(url);
                if (ytd.Status != YoutubeDownloadStatus.Success || ytd.File == null)
                {
                    await ReplyAsync("I couldn't download that track");
                    return;
                }

                using (ytd.File)
                using (var stream = ytd.File.File.OpenRead())
                {
                    // Add to library
                    var track = await _library.Add(Context.Guild.Id, Context.User.Id, stream, ytd.File.Title, ytd.File.Duration, ytd.File.Url, ytd.File.ThumbnailUrl);

                    // Play it
                    await Enqueue(track);
                }
            }
            else
            {
                await ReplyAsync("Sorry, that isn't a URL I know how to play");
            }
            
        }

        [NotNull, Command("play"), Summary("Play a track which best matches the given search parameter")]
        [RequireVoiceChannel]
        [ThinkingReply]
        public async Task Play([NotNull, Remainder] string search)
        {
            // Tolerate misusing the play command to invoke `play-random`
            if (search.ToLowerInvariant() == "random")
            {
                await PlayRandom();
                return;
            }

            // Get all potential tracks from those searches
            var tracks = await Search(search).ToArray();

            // if we failed to find a track in the database, try to treat the search string as a URL
            if (tracks.Length == 0)
            {
                if (Uri.TryCreate(search, UriKind.Absolute, out _))
                    await PlayUrl(search);
                else
                    await ReplyAsync("I can't find a track matching that search in the library");
                return;
            }

            // We found exactly one track, play it
            if (tracks.Length == 1)
            {
                await Enqueue(tracks[0]);
                return;
            }

            // Found multiple matches, display them and play a random one
            await DisplayItemList(
                tracks,
                () => "Found nothing",
                xs => $"Found {xs.Count} matching tracks in the library:",
                TrackString
            );

            await Enqueue(tracks.Random(_rng));

        }

        [NotNull, Command("find"), Summary("Find tracks by a search string")]
        [ThinkingReply]
        public async Task Find([NotNull, Remainder] string search)
        {
            // Tolerate misusing the play command to invoke `play-random`
            if (search.ToLowerInvariant() == "random")
            {
                await PlayRandom();
                return;
            }

            // Get all potential tracks from those searches
            var tracks = await Search(search).ToArray();

            // if we failed to find a track in the database, try to treat the search string as a URL
            if (tracks.Length == 0)
            {
                await ReplyAsync("I can't find a track matching that search in the library");
            }
            else if (tracks.Length == 1)
            {
                await ReplyAsync(await tracks[0].DiscordEmbed());
            }
            else
            {
                await DisplayItemList(
                    tracks,
                    () => "Found nothing",
                    xs => $"Found {xs.Count} matching tracks in the library:",
                    TrackString
                );
            }

        }

        [NotNull] private async Task Enqueue([NotNull] ITrack track)
        {
            // Get music queue
            var channel = await _queueCollection.Get(Context.Guild.Id);

            // Move into channel with user
            if (!(Context.User is IVoiceState vs) || vs.VoiceChannel == null)
            {
                await ReplyAsync("You must be in a voice channel!");
                return;
            }
            await channel.VoicePlayer.Move(vs.VoiceChannel);

            // Enqueue track
            var completion = await channel.Enqueue(track, new AudioFileReader(track.Path));

            // Create embed for this track (with no colour)
            var embed = await track.DiscordEmbed();

            //Show embed with green border (indicates it is playing)
            var message = await ReplyAsync(embed.WithColor(Color.Green));

            //Wait for track to finish
            await completion;

            //Change embed colour
            await message.ModifyAsync(a => a.Embed = embed.WithColor(Color.DarkPurple).Build());
        }

        [NotNull] private static string TrackString([NotNull] ITrack track, int index)
        {
            return $"{index}. **{track.Title}** (`{track.ID.MeaninglessString()}`)";
        }

        [NotNull, ItemNotNull]
        private async Task<IAsyncEnumerable<ITrack>> Search(string search)
        {
            var results = new List<IAsyncEnumerable<ITrack>>();

            // Try to treat search as a direct ID
            if (ulong.TryParse(search, out var ulongParse))
                results.Add(await _library.Get(Context.Guild.Id, id: ulongParse, order: TrackOrder.Id));
            else
            {
                var p = FriendlyId64.Parse(search);
                if (p.HasValue)
                    results.Add(await _library.Get(Context.Guild.Id, id: p.Value.Value, order: TrackOrder.Id));
            }

            // Treat search as a search on song title
            results.Add(await _library.Get(Context.Guild.Id, titleSearch: search, order: TrackOrder.Id));

            // Treat it as a durect URL search
            results.Add(await _library.Get(Context.Guild.Id, url: search, order: TrackOrder.Id));

            // Get all potential tracks from those searches
            return results.ToAsyncEnumerable().SelectMany(a => a).OrderBy(a => a.ID);
        }
    }
}
