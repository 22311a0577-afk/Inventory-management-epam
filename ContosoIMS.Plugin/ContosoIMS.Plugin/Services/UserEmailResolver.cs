using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace ContosoIMS.Plugin.Services
{
    public class UserEmailResolver : IUserEmailResolver
    {
        private readonly IOrganizationService _service;

        public UserEmailResolver(IOrganizationService service)
        {
            _service = service;
        }

        public string GetInternalEmail(Guid systemUserId)
        {
            if (systemUserId == Guid.Empty) return null;

            var user = _service.Retrieve(
                "systemuser",
                systemUserId,
                new ColumnSet("internalemailaddress"));

            return user == null ? null : user.GetAttributeValue<string>("internalemailaddress");
        }
    }
}