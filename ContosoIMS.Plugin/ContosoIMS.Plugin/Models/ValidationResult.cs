namespace ContosoIMS.Plugin.Models
{
    /// <summary>
    /// Outcome of input validation. Immutable value object.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }

        private ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static ValidationResult Success() { return new ValidationResult(true, null); }
        public static ValidationResult Failure(string message) { return new ValidationResult(false, message); }
    }
}
