using Octopus.Core.Model.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;

namespace OctopusVariableViewerExtension.Variables
{
   public interface IVariableManifestFactory
    {
        VariableCollection GetVariableManifest(string deploymentId);
        VariableCollection GetVariableManifest(NancyContext context, string releaseId, string environmentId, string tenantId);
    }
}
