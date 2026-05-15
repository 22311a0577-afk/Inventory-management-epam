// IUserEmailResolver.cs
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace ContosoIMS.Plugin.Services
{
    public interface IUserEmailResolver
    {
        string GetInternalEmail(Guid systemUserId);
    }
}
