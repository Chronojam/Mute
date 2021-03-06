﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Mute.Moe
{
    public class Configuration
    {
        [UsedImplicitly] public AuthConfig Auth;
        [UsedImplicitly] public AlphaAdvantageConfig AlphaAdvantage;
        [UsedImplicitly] public CoinMarketCapConfig CoinMarketCap;
        [UsedImplicitly] public DatabaseConfig Database;
        [UsedImplicitly] public YoutubeDlConfig YoutubeDl;
        [UsedImplicitly] public SentimentConfig Sentiment;
        [UsedImplicitly] public ElizaConfig ElizaConfig;
        [UsedImplicitly] public SteamConfig Steam;
        [UsedImplicitly] public SoundEffectConfig SoundEffects;
        [UsedImplicitly] public DictionaryConfig Dictionary;
        [UsedImplicitly] public SentimentReactionConfig SentimentReactions;
        [UsedImplicitly] public WordVectorsConfig WordVectors;
        [UsedImplicitly] public UrbanDictionaryConfig UrbanDictionary;
        [UsedImplicitly] public TTSConfig TTS;
        [UsedImplicitly] public MusicLibraryConfig MusicLibrary;

        [UsedImplicitly] public bool ProcessMessagesFromSelf = false;
        [UsedImplicitly] public char PrefixCharacter = '!';
    }

    public class AuthConfig
    {
        [UsedImplicitly] public string Token;
        [UsedImplicitly] public string ClientId;
        [UsedImplicitly] public string ClientSecret;
    }

    public class AlphaAdvantageConfig
    {
        [UsedImplicitly] public string Key;

        [UsedImplicitly] public int CacheSize = 128;
        [UsedImplicitly] public int CacheMinAgeSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;
        [UsedImplicitly] public int CacheMaxAgeSeconds = (int)TimeSpan.FromHours(6).TotalSeconds;
    }

    public class CoinMarketCapConfig
    {
        [UsedImplicitly] public string Key;

        [UsedImplicitly] public int CacheSize = 4096;
        [UsedImplicitly] public int CacheMinAgeSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;
        [UsedImplicitly] public int CacheMaxAgeSeconds = (int)TimeSpan.FromHours(6).TotalSeconds;
    }

    public class DatabaseConfig
    {
        [UsedImplicitly] public string ConnectionString;
    }

    public class YoutubeDlConfig
    {
        [UsedImplicitly] public string RateLimit;
        [UsedImplicitly] public string InProgressDownloadFolder;

        [UsedImplicitly] public string YoutubeDlBinaryPath;
        [UsedImplicitly] public string FfmpegBinaryPath;
    }

    public class SentimentConfig
    {
        [UsedImplicitly] public string SentimentModelPath;
        [UsedImplicitly] public string SentimentModelInputLayer;
        [UsedImplicitly] public string SentimentModelOutputLayer;
    }

    public class ElizaConfig
    {
        [UsedImplicitly] public List<string> Scripts;
    }

    public class SteamConfig
    {
        [UsedImplicitly] public string WebApiKey;
    }

    public class SoundEffectConfig
    {
        [UsedImplicitly] public string SfxFolder;
    }

    public class DictionaryConfig
    {
        [UsedImplicitly] public string WordListPath;
    }

    public class SentimentReactionConfig
    {
        [UsedImplicitly] public double CertaintyThreshold = 0.8;
        [UsedImplicitly] public double ReactionChance = 0.05;
        [UsedImplicitly] public double MentionReactionChance = 0.25;
    }

    public class WordVectorsConfig
    {
        [UsedImplicitly] public string WordVectorsBaseUrl;
        [UsedImplicitly] public uint CacheSize;
        [UsedImplicitly] public uint CacheMinTimeSeconds;
        [UsedImplicitly] public uint CacheMaxTimeSeconds;
    }

    public class UrbanDictionaryConfig
    {
        [UsedImplicitly] public uint CacheSize;
        [UsedImplicitly] public uint CacheMinTimeSeconds;
        [UsedImplicitly] public uint CacheMaxTimeSeconds;
    }

    public class TTSConfig
    {
        public class MsCongnitiveConfig
        {
            [UsedImplicitly] public string Region;
            [UsedImplicitly] public string Key;
            [UsedImplicitly] public string Language;
            [UsedImplicitly] public string Voice;
        }

        public MsCongnitiveConfig MsCognitive; 
    }

    public class MusicLibraryConfig
    {
        [UsedImplicitly] public string MusicFolder;
    }
}
