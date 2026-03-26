using System;

namespace ReelWords
{
    /// <summary>
    /// Tracks session score and word count, and fires events when the score changes.
    /// </summary>
    public class ScoringSystem
    {
        private readonly LetterValueTable _letterValues;

        /// <summary>Cumulative score for the current session.</summary>
        public int SessionScore { get; private set; }

        /// <summary>Total number of words scored this session.</summary>
        public int WordsFoundCount { get; private set; }

        /// <summary>
        /// Fired whenever the score changes.
        /// Parameters: (sessionScore, wordScore).
        /// On <see cref="Reset"/> fires with (0, 0).
        /// </summary>
        public event Action<int, int> OnScoreChanged;

        /// <param name="letterValues">Table used to look up per-letter point values.</param>
        public ScoringSystem(LetterValueTable letterValues)
        {
            _letterValues = letterValues;
        }

        /// <summary>
        /// Scores <paramref name="word"/>, adds to <see cref="SessionScore"/>,
        /// increments <see cref="WordsFoundCount"/>, and fires <see cref="OnScoreChanged"/>.
        /// Handles null and empty strings gracefully (scores 0, still fires event).
        /// </summary>
        public void ScoreWord(string word)
        {
            var wordScore = 0;

            if (!string.IsNullOrEmpty(word))
            {
                foreach (var ch in word)
                    wordScore += _letterValues.GetValue(ch);
            }

            SessionScore += wordScore;
            WordsFoundCount++;
            OnScoreChanged?.Invoke(SessionScore, wordScore);
        }

        /// <summary>Resets session score and word count to zero and fires <see cref="OnScoreChanged"/>(0, 0).</summary>
        public void Reset()
        {
            SessionScore = 0;
            WordsFoundCount = 0;
            OnScoreChanged?.Invoke(0, 0);
        }
    }
}
