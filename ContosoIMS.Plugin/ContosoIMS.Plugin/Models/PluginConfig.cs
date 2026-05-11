namespace ContosoIMS.Plugin.Models
{
    /// <summary>
    /// Plugin secure/unsecure configuration deserialized from the registration JSON.
    /// </summary>
    public class PluginConfig
    {
        public string FunctionUrl { get; set; }
        public string FunctionKey { get; set; }
    }
}
