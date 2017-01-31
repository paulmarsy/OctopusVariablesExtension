using Nancy;
using Nancy.Extensions;
using Octopus.Core.Model.Variables;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using OctopusVariableViewerExtension.Variables;

namespace OctopusVariableViewerExtension.Web
{
    public class OctopusVariablesModule : NancyModule
    {
        private readonly IApiActionResponseCreator _apiResponseCreator;
        private readonly IVariableManifestFactory _variableManifestFactory;

        public OctopusVariablesModule(IApiActionResponseCreator apiResponseCreator, IVariableManifestFactory variableManifestFactory)
        {
            _apiResponseCreator = apiResponseCreator;
            _variableManifestFactory = variableManifestFactory;
            Get["/api/variables/deployment/{id}"] = parameters => _apiResponseCreator.AsOctopusJson(Response, GetByDeploymentId(parameters), HttpStatusCode.OK);
            ;
            Get["/api/variables/test/{releaseId}/{environmentId}"] = parameters => apiResponseCreator.AsOctopusJson(Response, GetFromTestDeployment(parameters), HttpStatusCode.OK);
            Get["/api/variables/test/{releaseId}/{environmentId}/{tenantId}"] = parameters => apiResponseCreator.AsOctopusJson(Response, GetFromTestDeployment(parameters), HttpStatusCode.OK);

            Get["/api/variables/deployment/{id}/eval/{variable}"] = parameters => ReturnResolvedVariable(GetByDeploymentId(parameters), parameters);
            Get["/api/variables/test/{releaseId}/{environmentId}/eval/{variable}"] = parameters => ReturnResolvedVariable(GetFromTestDeployment(parameters), parameters);
            Get["/api/variables/test/{releaseId}/{environmentId}/{tenantId}/eval/{variable}"] = parameters => ReturnResolvedVariable(GetFromTestDeployment(parameters), parameters);

            Post["/api/variables/deployment/{id}/eval"] = parameters => ReturnEvaluatedRequest(GetByDeploymentId(parameters));
            Post["/api/variables/test/{releaseId}/{environmentId}/eval"] = parameters => ReturnEvaluatedRequest(GetFromTestDeployment(parameters));
            Post["/api/variables/test/{releaseId}/{environmentId}/{tenantId}/eval"] = parameters => ReturnEvaluatedRequest(GetFromTestDeployment(parameters));
        }

        private VariableCollection GetByDeploymentId(dynamic parameters) => _variableManifestFactory.GetVariableManifest(parameters.id);

        private VariableCollection GetFromTestDeployment(dynamic parameters) => _variableManifestFactory.GetVariableManifest(Context, (string) parameters.releaseId, (string) parameters.environmentId, (string) parameters.tenantId);

        private Response ReturnResolvedVariable(VariableCollection variableCollection, dynamic parameters) => FormatterExtensions.AsText(Response, variableCollection.Get(parameters.variable));

        private Response ReturnEvaluatedRequest(VariableCollection variableCollection) => Response.AsText(variableCollection.ToDictionary().Evaluate(Request.Body.AsString()));
    }
}