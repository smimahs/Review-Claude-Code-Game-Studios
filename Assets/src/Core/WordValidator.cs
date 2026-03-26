namespace ReelWords
{
    /// <summary>
    /// Validates submitted words against a <see cref="TrieDictionary"/>.
    /// </summary>
    public class WordValidator
    {
        private const int MINIMUM_WORD_LENGTH = 2;

        private readonly TrieDictionary _dictionary;

        /// <summary>True when the backing dictionary has been loaded and is ready for lookups.</summary>
        public bool IsReady => _dictionary.IsLoaded;

        /// <param name="dictionary">Loaded (or pre-seeded) trie dictionary.</param>
        public WordValidator(TrieDictionary dictionary)
        {
            _dictionary = dictionary;
        }

        /// <summary>
        /// Validates <paramref name="word"/> and returns a <see cref="ValidationResult"/>
        /// describing whether it is acceptable and why.
        /// </summary>
        public ValidationResult Validate(string word)
        {
            if (string.IsNullOrEmpty(word))
                return ValidationResult.Invalid(ValidationReason.NotInDictionary);

            if (!_dictionary.IsLoaded)
                return ValidationResult.Invalid(ValidationReason.DictionaryNotLoaded);

            if (word.Length < MINIMUM_WORD_LENGTH)
                return ValidationResult.Invalid(ValidationReason.TooShort);

            return _dictionary.Contains(word)
                ? ValidationResult.Valid()
                : ValidationResult.Invalid(ValidationReason.NotInDictionary);
        }
    }
}
