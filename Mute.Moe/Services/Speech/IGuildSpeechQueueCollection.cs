﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Mute.Moe.Services.Audio;
using Mute.Moe.Services.Audio.Mixing.Channels;

namespace Mute.Moe.Services.Speech
{
    public interface IGuildSpeechQueueCollection
    {
        Task<IGuildSpeechQueue> Get(ulong guild);
    }

    public interface IGuildSpeechQueue
        : ISimpleQueueChannel<string>
    {
        IGuildVoice VoicePlayer { get; }
    }

    public class InMemoryGuildSpeechQueueCollection
        : BaseInMemoryAudioPlayerQueueCollection<InMemoryGuildSpeechQueue, IGuildSpeechQueue>, IGuildSpeechQueueCollection
    {
        public InMemoryGuildSpeechQueueCollection(IGuildVoiceCollection voice)
            : base(voice)
        {
        }

        [NotNull] protected override InMemoryGuildSpeechQueue Create(IGuildVoice voice)
        {
            return new InMemoryGuildSpeechQueue(voice);
        }
    }

    public class InMemoryGuildSpeechQueue
        : SimpleQueueChannel<string>, IGuildSpeechQueue
    {
        public IGuildVoice VoicePlayer { get; }

        public InMemoryGuildSpeechQueue(IGuildVoice voice)
        {
            VoicePlayer = voice;
        }
    }
}
