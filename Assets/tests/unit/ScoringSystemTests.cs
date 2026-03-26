using NUnit.Framework;
using UnityEngine;
using ReelWords;

namespace ReelWords.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ScoringSystem"/>.
    /// Uses <see cref="ScriptableObject.CreateInstance{T}"/> to build a
    /// <see cref="LetterValueTable"/> with default Scrabble values.
    /// </summary>
    public class ScoringSystemTests
    {
        private LetterValueTable _table;
        private ScoringSystem _scoring;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<LetterValueTable>();
            _table.InitializeDefaults();
            _scoring = new ScoringSystem(_table);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_table);
        }

        // ---------------------------------------------------------------------------
        // Word score calculations
        // ---------------------------------------------------------------------------

        [Test]
        public void ScoreWord_PUZZLE_Returns26()
        {
            // P=3 U=1 Z=10 Z=10 L=1 E=1 → 26
            _scoring.ScoreWord("PUZZLE");
            Assert.AreEqual(26, _scoring.SessionScore);
        }

        [Test]
        public void ScoreWord_REEL_Returns4()
        {
            // R=1 E=1 E=1 L=1 → 4
            _scoring.ScoreWord("REEL");
            Assert.AreEqual(4, _scoring.SessionScore);
        }

        [Test]
        public void ScoreWord_QUARTZ_Returns24()
        {
            // Q=10 U=1 A=1 R=1 T=1 Z=10 → 24
            _scoring.ScoreWord("QUARTZ");
            Assert.AreEqual(24, _scoring.SessionScore);
        }

        // ---------------------------------------------------------------------------
        // Reset behaviour
        // ---------------------------------------------------------------------------

        [Test]
        public void Reset_SetsSessionScoreToZero()
        {
            _scoring.ScoreWord("REEL");
            _scoring.Reset();
            Assert.AreEqual(0, _scoring.SessionScore);
        }

        [Test]
        public void Reset_ThenScoreWord_SessionScoreEqualsWordScore()
        {
            _scoring.ScoreWord("PUZZLE"); // 26
            _scoring.Reset();
            _scoring.ScoreWord("REEL");  // 4
            Assert.AreEqual(4, _scoring.SessionScore);
        }

        // ---------------------------------------------------------------------------
        // Cumulative score
        // ---------------------------------------------------------------------------

        [Test]
        public void ScoreWord_TwoWords_SessionScoreIsCumulative()
        {
            _scoring.ScoreWord("REEL");   // 4
            _scoring.ScoreWord("PUZZLE"); // 26
            Assert.AreEqual(30, _scoring.SessionScore);
        }

        // ---------------------------------------------------------------------------
        // Event firing
        // ---------------------------------------------------------------------------

        [Test]
        public void ScoreWord_FiresOnScoreChangedExactlyOnce()
        {
            int fireCount = 0;
            _scoring.OnScoreChanged += (_, __) => fireCount++;
            _scoring.ScoreWord("REEL");
            Assert.AreEqual(1, fireCount, "OnScoreChanged should fire exactly once per ScoreWord call.");
        }

        [Test]
        public void Reset_FiresOnScoreChanged_WithZeroZero()
        {
            int receivedSession = -1;
            int receivedWord = -1;
            _scoring.OnScoreChanged += (s, w) => { receivedSession = s; receivedWord = w; };

            _scoring.ScoreWord("REEL");
            _scoring.Reset();

            Assert.AreEqual(0, receivedSession, "OnScoreChanged after Reset should pass sessionScore=0.");
            Assert.AreEqual(0, receivedWord,    "OnScoreChanged after Reset should pass wordScore=0.");
        }

        [Test]
        public void ScoreWord_EventSessionScoreParam_MatchesProperty()
        {
            int eventSession = -1;
            _scoring.OnScoreChanged += (s, _) => eventSession = s;

            _scoring.ScoreWord("REEL");   // 4
            _scoring.ScoreWord("PUZZLE"); // 26 → total 30

            Assert.AreEqual(_scoring.SessionScore, eventSession,
                "Last OnScoreChanged sessionScore param should match SessionScore property.");
        }

        // ---------------------------------------------------------------------------
        // Edge cases
        // ---------------------------------------------------------------------------

        [Test]
        public void ScoreWord_EmptyString_ScoresZero_NoException()
        {
            Assert.DoesNotThrow(() => _scoring.ScoreWord(string.Empty));
            Assert.AreEqual(0, _scoring.SessionScore);
        }

        [Test]
        public void ScoreWord_EmptyString_StillFiresOnScoreChanged()
        {
            int fireCount = 0;
            _scoring.OnScoreChanged += (_, __) => fireCount++;
            _scoring.ScoreWord(string.Empty);
            Assert.AreEqual(1, fireCount, "OnScoreChanged should fire even for empty-string input.");
        }
    }
}
