using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// ScriptableObject data asset holding the character sequences for each reel.
    /// Each sequence string defines the ordered characters on that reel.
    /// </summary>
    [CreateAssetMenu(menuName = "ReelWords/Reel Sequence Data")]
    public class ReelSequenceData : ScriptableObject
    {
        [SerializeField] private string[] _sequences;

        /// <summary>Number of reels defined in this data asset.</summary>
        public int ReelCount => _sequences?.Length ?? 0;

        /// <summary>
        /// Returns the character sequence for the reel at <paramref name="reelIndex"/> (uppercased).
        /// Logs an error and returns an empty string if the index is out of range.
        /// </summary>
        public string GetSequence(int reelIndex)
        {
            if (_sequences == null || reelIndex < 0 || reelIndex >= _sequences.Length)
            {
                Debug.LogError($"[ReelSequenceData] Reel index {reelIndex} is out of range (ReelCount={ReelCount}).");
                return string.Empty;
            }

            return _sequences[reelIndex]?.ToUpperInvariant() ?? string.Empty;
        }
    }
}
