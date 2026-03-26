using System.Collections.Generic;
using NUnit.Framework;
using ReelWords;

namespace ReelWords.Tests
{
    /// <summary>
    /// Unit tests for <see cref="TrieDictionary"/>.
    /// AC-1 through AC-10 from the acceptance criteria.
    /// </summary>
    public class TrieDictionaryTests
    {
        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Builds a TrieDictionary pre-seeded with the test vocabulary via the
        /// internal constructor so no Resources load is required for most tests.
        /// Test vocabulary: CAT, CATS, DOG, DOGS, WORD
        /// </summary>
        private static TrieDictionary BuildTestDictionary()
        {
            // Build the trie manually so tests are self-contained and fast.
            string[] words = { "CAT", "CATS", "DOG", "DOGS", "WORD" };
            var root = new TrieNode();
            int count = 0;

            foreach (var word in words)
            {
                var node = root;
                foreach (var ch in word)
                {
                    if (!node.Children.TryGetValue(ch, out var next))
                    {
                        next = new TrieNode();
                        node.Children[ch] = next;
                    }
                    node = next;
                }
                if (!node.IsEndOfWord)
                {
                    node.IsEndOfWord = true;
                    count++;
                }
            }

            return new TrieDictionary(root, count);
        }

        // ---------------------------------------------------------------------------
        // AC-1: Load with valid word list path → IsLoaded==true, WordCount>0
        // ---------------------------------------------------------------------------
        [Test]
        public void AC1_Load_ValidPath_IsLoaded_And_WordCountPositive()
        {
            var dict = new TrieDictionary("Data/TestWordList");
            dict.Load();

            Assert.IsTrue(dict.IsLoaded, "IsLoaded should be true after successful load.");
            Assert.Greater(dict.WordCount, 0, "WordCount should be > 0 after loading a non-empty list.");
        }

        // ---------------------------------------------------------------------------
        // AC-2: Contains known word → true
        // ---------------------------------------------------------------------------
        [Test]
        public void AC2_Contains_KnownWord_ReturnsTrue()
        {
            var dict = BuildTestDictionary();
            Assert.IsTrue(dict.Contains("CAT"));
            Assert.IsTrue(dict.Contains("DOGS"));
            Assert.IsTrue(dict.Contains("WORD"));
        }

        // ---------------------------------------------------------------------------
        // AC-3: Contains unknown word → false
        // ---------------------------------------------------------------------------
        [Test]
        public void AC3_Contains_UnknownWord_ReturnsFalse()
        {
            var dict = BuildTestDictionary();
            Assert.IsFalse(dict.Contains("FISH"));
            Assert.IsFalse(dict.Contains("ZEBRA"));
        }

        // ---------------------------------------------------------------------------
        // AC-4: Contains prefix only → false (list has "CATS" but not "CAT" if we
        //       check that "CA" is not a word, and "CAT" is a word but "CATSS" is not)
        //       The test vocabulary has CATS but not CATSS, so "CATSS" is a non-word prefix test.
        //       Additionally verify "CA" (prefix of CAT) returns false.
        // ---------------------------------------------------------------------------
        [Test]
        public void AC4_Contains_PrefixOnly_ReturnsFalse()
        {
            var dict = BuildTestDictionary();
            // "CA" is a prefix of "CAT" but is not itself a word in the list
            Assert.IsFalse(dict.Contains("CA"), "'CA' is a prefix but not a word.");
            // "DO" is a prefix of "DOG"/"DOGS" but not itself a word
            Assert.IsFalse(dict.Contains("DO"), "'DO' is a prefix but not a word.");
        }

        // ---------------------------------------------------------------------------
        // AC-5: Contains(null) → false, no exception
        // ---------------------------------------------------------------------------
        [Test]
        public void AC5_Contains_Null_ReturnsFalse_NoException()
        {
            var dict = BuildTestDictionary();
            Assert.DoesNotThrow(() =>
            {
                var result = dict.Contains(null);
                Assert.IsFalse(result);
            });
        }

        // ---------------------------------------------------------------------------
        // AC-6: Contains("") → false, no exception
        // ---------------------------------------------------------------------------
        [Test]
        public void AC6_Contains_EmptyString_ReturnsFalse_NoException()
        {
            var dict = BuildTestDictionary();
            Assert.DoesNotThrow(() =>
            {
                var result = dict.Contains(string.Empty);
                Assert.IsFalse(result);
            });
        }

        // ---------------------------------------------------------------------------
        // AC-7: Case-insensitive (lowercase input → true if word is in list)
        // ---------------------------------------------------------------------------
        [Test]
        public void AC7_Contains_LowercaseInput_MatchesUppercaseWord()
        {
            var dict = BuildTestDictionary();
            Assert.IsTrue(dict.Contains("cat"),  "Lowercase 'cat' should match 'CAT'.");
            Assert.IsTrue(dict.Contains("dogs"), "Lowercase 'dogs' should match 'DOGS'.");
            Assert.IsTrue(dict.Contains("Word"), "Mixed-case 'Word' should match 'WORD'.");
        }

        // ---------------------------------------------------------------------------
        // AC-8: Non-alpha "W0RD" → false
        // ---------------------------------------------------------------------------
        [Test]
        public void AC8_Contains_NonAlphaCharacters_ReturnsFalse()
        {
            var dict = BuildTestDictionary();
            Assert.IsFalse(dict.Contains("W0RD"), "String with digit should return false.");
            Assert.IsFalse(dict.Contains("DO-G"), "String with hyphen should return false.");
            Assert.IsFalse(dict.Contains("CAT!"), "String with punctuation should return false.");
        }

        // ---------------------------------------------------------------------------
        // AC-9: Missing file → IsLoaded==false
        // ---------------------------------------------------------------------------
        [Test]
        public void AC9_Load_MissingFile_IsLoadedFalse()
        {
            var dict = new TrieDictionary("Data/ThisFileDoesNotExist_XYZ");
            dict.Load();

            Assert.IsFalse(dict.IsLoaded, "IsLoaded should remain false when the file is missing.");
        }

        // ---------------------------------------------------------------------------
        // AC-10: WordCount reflects inserted words
        // ---------------------------------------------------------------------------
        [Test]
        public void AC10_WordCount_ReflectsInsertedWords()
        {
            // Use the Resources path test list which has exactly 5 words.
            var dict = new TrieDictionary("Data/TestWordList");
            dict.Load();

            Assert.AreEqual(5, dict.WordCount,
                "WordCount should equal the number of words in the test list (5).");
        }
    }
}
