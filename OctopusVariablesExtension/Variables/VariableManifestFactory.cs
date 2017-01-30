using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nevermore;
using Octopus.Core.Model.Environments;
using Octopus.Core.Model.Environments.Accounts;
using Octopus.Core.Model.Policies;
using Octopus.Core.Model.Projects;
using Octopus.Core.Model.ServerTasks;
using Octopus.Core.Model.TagSets;
using Octopus.Core.Model.Tenants;
using Octopus.Core.Model.Users;
using Octopus.Core.Model.Variables;
using Octopus.Core.Resources;
using Octopus.Server.Extensibility.Authentication.HostServices;
using Octopus.Server.Extensibility.HostServices.Web;
using Octopus.Server.Orchestration.Deploy;
using Octopus.Server.Orchestration.Deploy.Manifest;
using Octopus.Server.Orchestration.Tags;
using Octopus.Server.Web;
using Octopus.Diagnostics;

namespace OctopusVariableViewerExtension.Variables
{
    public class VariableManifestFactory : IVariableManifestFactory
    {
        private readonly IDeploymentManifestFactory _deploymentManifestFactory;
        private readonly IRelationalStore _store;
        private readonly IWebPortalConfigurationStore _webPortalConfigurationStore;
        private readonly ILog _log;

        public VariableManifestFactory(ILog log, IDeploymentManifestFactory deploymentManifestFactory, IWebPortalConfigurationStore webPortalConfigurationStore, IRelationalStore store)
        {
            _log = log;
            _deploymentManifestFactory = deploymentManifestFactory;
            _webPortalConfigurationStore = webPortalConfigurationStore;
            _store = store;
        }

        public VariableCollection GetVariableManifest(string deploymentId)
        {
            using (var transaction = _store.BeginTransaction())
            {
                var deployment = transaction.LoadRequired<Deployment>(deploymentId);
                return transaction.LoadRequired<VariableSet>(deployment.ManifestVariableSetId).Variables;
            }
        }
        public VariableCollection GetVariableManifest(NancyContext context, string releaseId, string environmentId, string tenantId)
        {
            using (var transaction = _store.BeginTransaction())
            {
                var release = transaction.LoadRequired<Release>(releaseId);
                var environment = transaction.LoadRequired<DeploymentEnvironment>(environmentId);
                var tenant = string.IsNullOrEmpty(tenantId) ? null : transaction.LoadRequired<Tenant>(tenantId);

                var project = transaction.LoadRequired<Project>(release.ProjectId);
                var deploymentProcess = transaction.LoadRequired<DeploymentProcess>(release.ProjectDeploymentProcessSnapshotId);
                GetPreviousDeployment(transaction, project.Id, environmentId, tenantId, out var previousSuccessfulEnvironmentDeployment, out var currentReleaseForEnvironment);

                return _deploymentManifestFactory.CreateManifestVariables(
                    _webPortalConfigurationStore.GetListenPrefixes(),
                    new Deployment(),
                    project,
                    deploymentProcess,
                    environment,
                    release,
                    GetChannel(transaction, release.ChannelId),
                    GetServerTask(project, release, environment, tenant),
                    GetEnvironmentMachines(transaction, environmentId, deploymentProcess, release.ChannelId, tenant),
                    GetEnvironmentAccounts(transaction, environmentId),
                    GetMachinePolicies(transaction),
                    RetentionPeriod.KeepForever(),
                    GetProjectGroup(transaction, project),
                    GetUser(context, transaction),
                    GetProjectVariables(transaction, release, project),
                    GetLibraryVariables(transaction, release, project),
                    GetPreviousRelease(transaction, releaseId, release.ProjectId),
                    GetPreviousReleaseForEnvironment(transaction, release.ProjectId, environmentId, releaseId),
                    previousSuccessfulEnvironmentDeployment,
                    currentReleaseForEnvironment,
                    tenant,
                    GetTagNameMapper(transaction),
                    GetTenantVariables(transaction, tenantId, release.ProjectId, environmentId));
            }
        }

        private static Channel GetChannel(IRelationalTransaction transaction, string channelId)
        {
            return transaction.LoadRequired<Channel>(channelId);
        }

        private static List<Account> GetEnvironmentAccounts(IRelationalTransaction transaction, string environmentId)
        {
            return transaction.Query<Account>()
                .Where("[EnvironmentIds] IS NULL OR [EnvironmentIds] = '' OR [EnvironmentIds] LIKE @environmentId")
                .LikePipedParameter(nameof(environmentId), environmentId)
                .ToList();
        }

        private static Release GetPreviousReleaseForEnvironment(IRelationalTransaction transaction, string projectId, string environmentId, string releaseId)
        {
            return transaction.Query<Release>()
                .Where("[Id] = (SELECT TOP 1 [ReleaseId] FROM Deployment WHERE [ProjectId] = @projectId AND [EnvironmentId] = @environmentId AND [ReleaseId] <> @releaseId ORDER BY [Created] DESC)")
                .Parameter(nameof(projectId), projectId)
                .Parameter(nameof(environmentId), environmentId)
                .Parameter(nameof(releaseId), releaseId)
                .First();
        }

        private static Release GetPreviousRelease(IRelationalTransaction transaction, string releaseId, string projectId)
        {
            return transaction.Query<Release>()
                .Where("[Id] <> @releaseId AND [ProjectId] = @projectId")
                .Parameter(nameof(releaseId), releaseId)
                .Parameter(nameof(projectId), projectId)
                .OrderByDescending("Assembled")
                .First();
        }

        private static List<MachinePolicy> GetMachinePolicies(IRelationalTransaction transaction)
        {
            return transaction.Query<MachinePolicy>()
                .ToList();
        }

        private static ServerTask GetServerTask(Project project, Release release, DeploymentEnvironment environment, Tenant tenant)
        {
            return new ServerTask(BuiltInTasks.Deploy.Arguments.DeploymentId, $"Deploy {project.Name} release {release.Version} to {environment.Name}{(tenant == null ? null : $" for {tenant.Name}")}");
        }

        private static ProjectGroup GetProjectGroup(IRelationalTransaction transaction, Project project)
        {
            return transaction.LoadRequired<ProjectGroup>(project.ProjectGroupId);
        }

        private static User GetUser(NancyContext context, IRelationalTransaction transaction)
        {
            return transaction.LoadRequired<User>(((IOctopusPrincipal)context.CurrentUser).Id);
        }

        private static VariableCollection GetProjectVariables(IRelationalTransaction transaction, Release release, Project project)
        {
            var projectVariables = transaction.Load<VariableSet>(release.ProjectVariableSetSnapshotId).Variables;
            AddTemplates(projectVariables, project);
            return projectVariables;
        }

        private static VariableCollection GetTenantVariables(IRelationalTransaction transaction, string tenantId, string projectId, string environmentId)
        {
            var tenantVariables = new VariableCollection();
            if (tenantId == null)
                return tenantVariables;

            foreach (var tenantVariable in new TenantVariableLoader(transaction)
                .Get(tenantId, projectId, environmentId)
                .Select(v =>
                {
                    var variableDeclaration = new VariableDeclaration
                    {
                        Id = v.MissingVariableTemplate.Id,
                        Name = v.MissingVariableTemplate.VariableName,
                        Scope = new ScopeSpecification { { ScopeField.Tenant, tenantId } },
                        Value = v.Value.Value,
                        IsSensitive = v.MissingVariableTemplate.IsSensitive
                    };
                    if (v.IsProject)
                    {
                        variableDeclaration.Scope.Add(ScopeField.Project, projectId);
                        variableDeclaration.Scope.Add(ScopeField.Environment, environmentId);
                    }
                    return variableDeclaration;
                }))
                tenantVariables.Add(tenantVariable);

            return tenantVariables;
        }

        private static ICanonicalTagNameMapper GetTagNameMapper(IRelationalTransaction transaction)
        {
            return new CanonicalTagNameMapper(new LazyDocumentList<TagSet>(transaction));
        }

        private static ReferenceCollection GetTargetRoles(DeploymentProcess deploymentProcess, string environmentId, string channelId, ITenantTagTester tenantTagTester)
        {
            return new ReferenceCollection(deploymentProcess.AllActions
                .Where(deploymentAction => deploymentAction.AppliesToScenario(environmentId, channelId, tenantTagTester))
                .SelectMany(x => deploymentProcess.GetTargetRoles(x.Id)));
        }

        private static List<Machine> GetEnvironmentMachines(IRelationalTransaction transaction, string environmentId, DeploymentProcess deploymentProcess, string channelId, Tenant tenant)
        {
            var tenantTagTester = new TenantTagTester(tenant);
            return transaction.Query<Machine>()
                .Where("[IsDisabled] = 0 AND [EnvironmentIds] LIKE @environmentId")
                .LikePipedParameter(nameof(environmentId), environmentId)
                .ToList()
                .Where(m => m.AppliesToRoles(GetTargetRoles(deploymentProcess, environmentId, channelId, tenantTagTester)))
                .Where(m => m.AppliesToTenant(tenantTagTester))
                .ToList();
        }

        private static void GetPreviousDeployment(IRelationalTransaction transaction, string projectId, string environmentId, string tenantId, out Deployment previousSuccessfulEnvironmentDeployment, out Release currentReleaseForEnvironment)
        {
            var orderedQueryBuilder = transaction.Query<ServerTask>()
                .Where("ProjectId", SqlOperand.Equal, projectId)
                .Where("EnvironmentId", SqlOperand.Equal, environmentId)
                .Where("State", SqlOperand.Equal, "Success")
                .OrderByDescending("CompletedTime");

            if (tenantId != null)
                orderedQueryBuilder
                    .Where("[TenantId] = @tenantId")
                    .Parameter(nameof(tenantId), tenantId);
            else
                orderedQueryBuilder
                    .Where("[TenantId] IS NULL");
            var serverTask = orderedQueryBuilder.First();
            var id = serverTask?.Arguments.GetValue(BuiltInTasks.Deploy.Arguments.DeploymentId).ToString();

            previousSuccessfulEnvironmentDeployment = string.IsNullOrEmpty(id) ? null : transaction.Load<Deployment>(id);
            currentReleaseForEnvironment = string.IsNullOrEmpty(id) ? null : transaction.Load<Release>(previousSuccessfulEnvironmentDeployment.ReleaseId);
        }


        private static VariableCollection GetLibraryVariables(IRelationalTransaction transaction, Release release, Project project)
        {
            var source = new VariableCollection();

            foreach (var variableSetSnapshot in release.LibraryVariableSetSnapshots)
                source.Extend(transaction.LoadRequired<VariableSet>(variableSetSnapshot.VariableSetSnapshotId).Variables);

            if (project.IncludedLibraryVariableSetIds.Any())
                foreach (var libraryVariableSet in transaction.Load<LibraryVariableSet>(project.IncludedLibraryVariableSetIds))
                    AddTemplates(source, libraryVariableSet);

            return source;
        }
        private static void AddTemplates(VariableCollection variableCollection, IHaveTemplates owner)
        {
            foreach (var variableDeclaration in owner.Templates
                .Where(template => !string.IsNullOrEmpty(template.DefaultValue))
                .Select(template => new VariableDeclaration(template.Name, template.DefaultValue, template.IsSensitive)))
                variableCollection.Add(variableDeclaration);
        }
    }
}