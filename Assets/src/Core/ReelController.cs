using System;

namespace ReelWords
{
    /// <summary>
    /// Manages the state of a single reel: which character is currently showing
    /// and advancing to the next character in the sequence.
    /// </summary>
    public class ReelController
    {
        private readonly string _sequence;

        /// <summary>Zero-based index of the currently displayed character.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>The character currently showing on this reel.</summary>
        public char CurrentChar => _sequence[CurrentIndex];

        /// <param name="sequence">Non-empty string of characters for this reel.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sequence"/> is null or empty.</exception>
        public ReelController(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                throw new ArgumentException("Reel sequence must not be null or empty.", nameof(sequence));

            _sequence = sequence.ToUpperInvariant();
            CurrentIndex = 0;
        }

        /// <summary>Advances to the next character, wrapping around to the start of the sequence.</summary>
        public void Advance()
        {
            CurrentIndex = (CurrentIndex + 1) % _sequence.Length;
        }
    }
}
