namespace ReelWords
{
    public enum ValidationReason { Valid, TooShort, NotInDictionary, DictionaryNotLoaded }

    public readonly struct ValidationResult
    {
        public bool IsValid { get; }
        public ValidationReason Reason { get; }

        public ValidationResult(bool isValid, ValidationReason reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        public static ValidationResult Valid() => new(true, ValidationReason.Valid);
        public static ValidationResult Invalid(ValidationReason reason) => new(false, reason);
    }
}
