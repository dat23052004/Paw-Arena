using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawArena.Infrastructure.Audio
{
    /// <summary>
    /// Maps stable cue identifiers to designer-authored audio definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Paw Arena/Audio/Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private List<AudioCue> _cues = new();

        private readonly Dictionary<string, AudioCue> _cueById = new(StringComparer.Ordinal);

        /// <summary>
        /// Attempts to resolve a cue by stable identifier.
        /// </summary>
        public bool TryGet(string cueId, out AudioCue cue)
        {
            cue = null;
            if (_cueById.Count == 0 && _cues.Count > 0)
            {
                RebuildCache(false);
            }

            return !string.IsNullOrWhiteSpace(cueId) && _cueById.TryGetValue(cueId, out cue);
        }

        private void OnEnable() => RebuildCache(false);

        private void OnValidate() => RebuildCache(true);

        private void RebuildCache(bool logErrors)
        {
            _cueById.Clear();
            for (var i = 0; i < _cues.Count; i++)
            {
                var cue = _cues[i];
                if (cue == null || string.IsNullOrWhiteSpace(cue.CueId))
                {
                    continue;
                }

                if (!_cueById.TryAdd(cue.CueId, cue) && logErrors)
                {
                    Debug.LogError($"Duplicate audio cue ID '{cue.CueId}'.", this);
                }
            }
        }
    }
}
