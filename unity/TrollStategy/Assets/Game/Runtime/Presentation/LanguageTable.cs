using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Presentation
{
    /// <summary>
    /// The translation files of the game, one per language code (tools/localization/build.py writes them into
    /// UI/Localization; LocalizationSetup lists them here). The bootstrap hands it to <see cref="Localization"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Languages", menuName = "TrollStrategy/Presentation/Language Table")]
    public sealed class LanguageTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Code;
            public TextAsset File;
        }

        [SerializeField] private List<Entry> _languages = new();

        public IReadOnlyList<Entry> Languages => _languages;

        public TextAsset Get(string code)
        {
            foreach (var entry in _languages)
                if (entry.Code == code) return entry.File;
            return null;
        }

        public void Set(IEnumerable<Entry> languages) => _languages = new List<Entry>(languages);
    }
}
