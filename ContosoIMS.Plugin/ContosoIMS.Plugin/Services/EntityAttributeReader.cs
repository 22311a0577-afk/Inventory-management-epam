using System;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>
    /// Safe, null-tolerant readers for Dataverse entity attributes (SRP).
    /// </summary>
    internal static class EntityAttributeReader
    {
        public static string GetString(Entity e, string attr)
        {
            if (e == null || !e.Contains(attr) || e[attr] == null) return string.Empty;
            return e[attr].ToString().Trim();
        }

        public static int GetInteger(Entity e, string attr)
        {
            if (e == null || !e.Contains(attr) || e[attr] == null) return 0;
            return Convert.ToInt32(e[attr]);
        }

        public static int GetOptionSetValue(Entity e, string attr)
        {
            if (e == null || !e.Contains(attr) || e[attr] == null) return 0;
            return ((OptionSetValue)e[attr]).Value;
        }
    }
}
