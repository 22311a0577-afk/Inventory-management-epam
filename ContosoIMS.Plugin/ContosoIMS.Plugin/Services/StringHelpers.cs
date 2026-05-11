namespace ContosoIMS.Plugin.Services
{
    /// <summary>
    /// Small string utilities. Extracted so logic is reusable and unit-testable (SRP).
    /// </summary>
    internal static class StringHelpers
    {
        public static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
