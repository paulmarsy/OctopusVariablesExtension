using Autofac;
using Nancy;
using Octopus.Server.Extensibility.Extensions;
using Octopus.Server.Extensibility.HostServices.Web;
using OctopusVariableViewerExtension.Variables;
using OctopusVariableViewerExtension.Web;

namespace OctopusVariableViewerExtension
{
    [OctopusPlugin("Octopus Variables", "Paul Marston")]
    public class OctopusVariablesExtension : IOctopusExtension
    {
        public void Load(ContainerBuilder builder)
        {
            builder.RegisterType<VariableManifestFactory>()
                .As<IVariableManifestFactory>()
                .InstancePerDependency();

            builder.RegisterType<OctopusVariablesModule>()
                .As<NancyModule>()
                .InstancePerDependency();

            builder.RegisterType<OctopusVariablesHomeLinks>()
                .As<IHomeLinksContributor>()
                .InstancePerDependency();
        }
    }
}