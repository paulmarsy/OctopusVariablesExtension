using Nancy;
using Octopus.Core.Model.Variables;

namespace OctopusVariableViewerExtension.Variables
{
    public interface IVariableManifestFactory
    {
        VariableCollection GetVariableManifest(string deploymentId);
        VariableCollection GetVariableManifest(NancyContext context, string releaseId, string environmentId, string tenantId);
    }
}