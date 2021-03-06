﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Mute.Moe.Discord.Services.Responses.Eliza.Engine;

namespace Mute.Moe.Discord.Services.Responses.Eliza.Scripts
{
    public class Script
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<Key>> _keys;

        public IReadOnlyList<IReadOnlyCollection<string>> Syns { get; }
        public IReadOnlyDictionary<string, Transform> Pre { get; }
        public IReadOnlyDictionary<string, Transform> Post { get; }
        public IReadOnlyList<string> Final { get; }
        public IReadOnlyList<string> Quit { get; }

        public Script([NotNull] IEnumerable<string> lines, [NotNull] IEnumerable<IKeyProvider> keyProviders)
        {
            List<Decomposition> lastDecomp = null;
            List<IReassembly> lastReasemb = null;
            var keysList = new List<Key>();
            var pre = new List<Transform>();
            var post = new List<Transform>();
            var quit = new List<string>();
            var syns = new List<HashSet<string>>();
            var final = new List<string>();

            //Parse all the lines of the script
            foreach (var line in lines)    
                ParseLine(line, ref lastReasemb, ref lastDecomp, keysList, pre, post, quit, syns, final);

            Syns = syns;
            Pre = new ReadOnlyDictionary<string, Transform>(pre.ToDictionary(a => a.Source, a => a));
            Post = new ReadOnlyDictionary<string, Transform>(post.ToDictionary(a => a.Source, a => a));
            Quit = quit;
            Final = final;

            //Get keys from all external providers
            keysList.AddRange(keyProviders.SelectMany(kp => kp.Keys));

            // Expand `@foo` in the keyword position into N rules, one for each synonym of `foo`
            ExpandSynonymKeys(keysList, syns);

            _keys = new ReadOnlyDictionary<string, IReadOnlyList<Key>>((
                from key in keysList
                group key by key.Keyword
                into kgroup
                select kgroup).ToDictionary(a => a.Key, a => (IReadOnlyList<Key>)a.ToArray()
            ));
        }

        [NotNull] public IEnumerable<Key> GetKeys(string keyword)
        {
            //Get keys from script
            if (!_keys.TryGetValue(keyword, out var results))
                return Array.Empty<Key>();
            else
                return results;

        }

        #region parsing
        private static void ExpandSynonymKeys([NotNull] IList<Key> keysList, IReadOnlyList<IReadOnlyCollection<string>> syns)
        {
            for (var i = keysList.Count - 1; i >= 0; i--)
            {
                //Find out if this key uses a synonym as it's keyword
                var k = keysList[i];
                if (!keysList[i].Keyword.StartsWith('@'))
                    continue;

                //It does, so remove it
                keysList.RemoveAt(i);

                //Find synonyms of keyword
                var kw = k.Keyword.Substring(1);
                var synonyms = syns.FirstOrDefault(a => a.Contains(kw));
                if (synonyms == null)
                    continue;

                //create concrete keys, one for each synonym
                foreach (var synonym in synonyms)
                    keysList.Add(new Key(synonym, k.Rank, k.Decompositions));
            }
        }

        private static bool DecompositionRule([NotNull] string s, ref List<Decomposition> lastDecomp, ref List<IReassembly> lastReasemb)
        {
            var m = Regex.Match(s, "^.*?decomp:(?<modifiers>[~\\$ ]*)(?<value>.*)$");
            if (!m.Success)
                return false;

            if (lastDecomp != null)
            {
                lastReasemb = new List<IReassembly>();

                var val = m.Groups["value"].Value;
                var mod = m.Groups["modifiers"].Value;

                lastDecomp.Add(new Decomposition(val, mod.Contains('$'), mod.Contains('~'), lastReasemb));
            }

            return true;
        }

        private static bool ReassemblyRule([NotNull] string s, ICollection<IReassembly> lr)
        {
            var m = Regex.Match(s, "^.*?reasmb:( )+(?<value>.*)$");
            if (m.Success)
            {
                lr?.Add(new ConstantReassembly(m.Groups["value"].Value));
                return true;
            }

            return false;
        }

        private static bool KeysRule([NotNull] string s, ICollection<Key> keys, [NotNull] ref List<Decomposition> lastDecomp, ref List<IReassembly> lastReasemb)
        {
            var m = Regex.Match(s, "^.*?key:( )+(?<value>.*?)( )*(?<rank>\\d*)$");
            if (!m.Success)
                return false;

            var val = m.Groups["value"].Value;
            var rank = m.Groups["rank"].Value;

            int.TryParse(rank, out var rankValue);

            lastDecomp = new List<Decomposition>();
            lastReasemb = null;

            keys.Add(new Key(val, rankValue, lastDecomp));

            return true;
        }

        private static bool SynonymRule([NotNull] string s, ICollection<HashSet<string>> synonyms)
        {
            var m = Regex.Match(s, "^.*?synon:( )+(?<value>.*)$");
            if (!m.Success)
                return false;

            synonyms.Add(new HashSet<string>(m.Groups["value"].Value.Split(' ')));
            return true;
        }

        private static bool PreRule([NotNull] string s, ICollection<Transform> pre)
        {
            var m = Regex.Match(s, "^.*?pre:( )+(?<a>.*?)( )+(?<b>.*?)$");
            if (!m.Success)
                return false;

            pre.Add(new Transform(m.Groups["a"].Value, m.Groups["b"].Value));
            return true;
        }

        private static bool PostRule([NotNull] string s, ICollection<Transform> post)
        {
            var m = Regex.Match(s, "^.*?post:( )+(?<a>.*?)( )+(?<b>.*?)$");
            if (!m.Success)
                return false;

            post.Add(new Transform(m.Groups["a"].Value, m.Groups["b"].Value));
            return true;
        }

        private static bool FinalRule([NotNull] string s, ICollection<string> final)
        {
            var m = Regex.Match(s, "^.*?final:( )+(?<value>.*)$");
            if (!m.Success)
                return false;

            final.Add(m.Groups["value"].Value);
            return true;
        }

        private static bool QuitRule([NotNull] string s, ICollection<string> quit)
        {
            var m = Regex.Match(s, "^.*?quit:( )+(?<value>.*)$");
            if (!m.Success)
                return false;

            quit.Add(m.Groups["value"].Value);
            return true;
        }

        /// <summary>Process a line of script input.</summary>
		/// <remarks>Process a line of script input.</remarks>
		private static bool ParseLine([CanBeNull] string line, ref List<IReassembly> lastReasemb, ref List<Decomposition> lastDecomp, ICollection<Key> keys, ICollection<Transform> pre, ICollection<Transform> post, ICollection<string> quit, ICollection<HashSet<string>> syns, ICollection<string> final)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            return ReassemblyRule(line, lastReasemb)
                   || DecompositionRule(line, ref lastDecomp, ref lastReasemb)
                   || KeysRule(line, keys, ref lastDecomp, ref lastReasemb)
                   || SynonymRule(line, syns)
                   || PreRule(line, pre)
                   || PostRule(line, post)
                   || FinalRule(line, final)
                   || QuitRule(line, quit);
		}
        #endregion
    }
}
