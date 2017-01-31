using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Web;

namespace OctopusVariableViewerExtension.Web
{
    public class OctopusVariablesHomeLinks : IHomeLinksContributor
    {
        public IDictionary<string, string> GetLinksToContribute()
        {
            return new Dictionary<string, string>
            {
                {"VariableDeploymentManifest", "/api/variables/deployment/{id}"},
                {"VariableDeploymentResolution", "/api/variables/deployment/{id}/eval{/variable}"},
                {"VariableTestManifest", "/api/variables/test/{releaseId}/{environmentId}{/tenantId}"},
                {"VariableTestResolution", "/api/variables/test/{releaseId}/{environmentId}{/tenantId}/eval{/variable}"}
            };
        }
    }
}