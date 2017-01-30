using System.Collections.Generic;
using System.Linq;
using Nancy;
using Octopus.Core.Model.Variables;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using OctopusVariableViewerExtension.Variables;
using System;
using Nancy.Extensions;

namespace OctopusVariableViewerExtension.Web
{
    public partial class OctopusVariablesModule : NancyModule
    {
        private readonly IApiActionResponseCreator _apiResponseCreator;
        private readonly IVariableManifestFactory _variableManifestFactory;
        public OctopusVariablesModule(IApiActionResponseCreator apiResponseCreator, IVariableManifestFactory variableManifestFactory)
        {
            _apiResponseCreator = apiResponseCreator;
            _variableManifestFactory = variableManifestFactory;
            Get["/api/variables/deployment/{id}"] = parameters => _apiResponseCreator.AsOctopusJson(Response, _variableManifestFactory.GetVariableManifest(parameters.id), HttpStatusCode.OK); ;
            Get["/api/variables/test/{releaseId}/{environmentId}"] = parameters => apiResponseCreator.AsOctopusJson(Response, variableManifestFactory.GetVariableManifest(Context, parameters.releaseId, parameters.environmentId, null), HttpStatusCode.OK);
            Get["/api/variables/test/{releaseId}/{environmentId}/{tenantId}"] = parameters => apiResponseCreator.AsOctopusJson(Response, variableManifestFactory.GetVariableManifest(Context, parameters.releaseId, parameters.environmentId, parameters.tenantId), HttpStatusCode.OK);

            Get["/api/variables/deployment/{id}/eval/{variable}"] = parameters => Response.AsText(variableManifestFactory.GetVariableManifest((string)parameters.id).Get((string)parameters.variable));
            Post["/api/variables/deployment/{id}/eval"] = parameters => Response.AsText(variableManifestFactory.GetVariableManifest((string)parameters.id).ToDictionary().Evaluate(Request.Body.AsString()));
            Get["/api/variables/test/{releaseId}/{environmentId}/eval/{variable}"] = parameters => Response.AsText(variableManifestFactory.GetVariableManifest(Context, (string)parameters.releaseId, (string)parameters.environmentId, null).Get((string)parameters.variable));
            Post["/api/variables/test/{releaseId}/{environmentId}/eval"] = parameters => Response.AsText(variableManifestFactory.GetVariableManifest(Context, (string)parameters.releaseId, (string)parameters.environmentId, null).ToDictionary().Evaluate(Request.Body.AsString()));
            Get["/api/variables/test/{releaseId}/{environmentId}/{tenantId}/eval/{variable}"] = parameters => Response.AsText(variableManifestFactory.GetVariableManifest(Context, (string)parameters.releaseId, (string)parameters.environmentId, (string)parameters.tenantId).Get((string)parameters.variable));
            Post["/api/variables/test/{releaseId}/{environmentId}/{tenantId}/eval"] = parameters => Response.AsText(variableManifestFactory.GetVariableManifest(Context, (string)parameters.releaseId, (string)parameters.environmentId, (string)parameters.tenantId).ToDictionary().Evaluate(Request.Body.AsString()));
        }
    }
}
