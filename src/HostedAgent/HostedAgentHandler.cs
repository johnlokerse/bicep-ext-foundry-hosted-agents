using FoundryExtension.Client;

namespace FoundryExtension.HostedAgent;

public class HostedAgentHandler(IFoundryClientFactory clientFactory)
    : TypedResourceHandler<HostedAgentResource, HostedAgentIdentifiers, Configuration>
{
    protected override HostedAgentIdentifiers GetIdentifiers(HostedAgentResource properties) => new()
    {
        Name = properties.Name,
    };

    protected override Task<ResourceResponse> Preview(
        ResourceRequest request,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            HostedAgentValidator.Validate(request.Properties);
            using var client = clientFactory.Create(request.Config.ProjectEndpoint);
            var desired = HostedAgentDefinitionMapper.ToPayload(request.Properties);
            var existing = await client.GetAgentAsync(request.Properties.Name, cancellationToken);

            var action = existing is null
                ? "create"
                : GetPlannedAction(desired, existing);

            if (existing is not null)
            {
                EnsureHosted(existing);
                HostedAgentDefinitionMapper.ApplyOutputs(request.Properties, existing, action, client);
            }
            else
            {
                request.Properties.DeploymentAction = action;
                request.Properties.Status = "notFound";
                request.Properties.Endpoints = client.GetProtocolEndpoints(
                    request.Properties.Name,
                    desired.ProtocolVersions);
            }

            return GetResponse(request);
        });

    protected override Task<ResourceResponse> CreateOrUpdate(
        ResourceRequest request,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            HostedAgentValidator.Validate(request.Properties);
            using var client = clientFactory.Create(request.Config.ProjectEndpoint);
            var desired = HostedAgentDefinitionMapper.ToPayload(request.Properties);
            var existing = await client.GetAgentAsync(request.Properties.Name, cancellationToken);

            AgentVersionResponse deployed;
            string action;

            if (existing is null)
            {
                action = "create";
                deployed = await client.CreateAgentAsync(request.Properties.Name, desired, cancellationToken);
            }
            else
            {
                EnsureHosted(existing);
                if (!HostedAgentDefinitionComparer.Equals(desired, existing.Definition))
                {
                    action = "newVersion";
                    deployed = await client.CreateAgentVersionAsync(request.Properties.Name, desired, cancellationToken);
                }
                else if (string.Equals(existing.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    action = "retryVersion";
                    deployed = await client.CreateAgentVersionAsync(request.Properties.Name, desired, cancellationToken);
                }
                else
                {
                    action = "none";
                    deployed = existing;
                }
            }

            deployed = await client.WaitUntilActiveAsync(deployed, cancellationToken);
            HostedAgentDefinitionMapper.ApplyOutputs(request.Properties, deployed, action, client);
            return GetResponse(request);
        });

    protected override Task<ResourceResponse> Get(
        ReferenceRequest request,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            using var client = clientFactory.Create(request.Config.ProjectEndpoint);
            var existing = await client.GetAgentAsync(request.Identifiers.Name, cancellationToken);
            if (existing is null)
            {
                return GetResponse(request, properties: null);
            }

            EnsureHosted(existing);
            var resource = HostedAgentDefinitionMapper.FromResponse(existing);
            HostedAgentDefinitionMapper.ApplyOutputs(resource, existing, "none", client);
            return GetResponse(request, resource);
        });

    protected override Task<ResourceResponse> Delete(
        ReferenceRequest request,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            using var client = clientFactory.Create(request.Config.ProjectEndpoint);
            await client.DeleteAgentAsync(request.Identifiers.Name, cancellationToken);
            return GetResponse(request, properties: null);
        });

    private static string GetPlannedAction(
        HostedAgentDefinitionPayload desired,
        AgentVersionResponse existing)
    {
        EnsureHosted(existing);

        if (!HostedAgentDefinitionComparer.Equals(desired, existing.Definition))
        {
            return "newVersion";
        }

        return string.Equals(existing.Status, "failed", StringComparison.OrdinalIgnoreCase)
            ? "retryVersion"
            : "none";
    }

    private static void EnsureHosted(AgentVersionResponse response)
    {
        if (!string.Equals(response.Definition.Kind, "hosted", StringComparison.OrdinalIgnoreCase))
        {
            throw new HostedAgentValidationException(
                [$"An agent named '{response.Name}' already exists but is not a hosted agent."]);
        }
    }

    private static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (HostedAgentValidationException exception)
        {
            throw new ResourceErrorException("InvalidHostedAgent", exception.Message);
        }
        catch (FoundryApiException exception)
        {
            throw new ResourceErrorException(
                exception.ErrorCode ?? "FoundryApiError",
                exception.Message);
        }
        catch (AgentProvisioningException exception)
        {
            throw new ResourceErrorException(
                exception.ErrorCode ?? "AgentProvisioningFailed",
                exception.Message);
        }
        catch (TimeoutException exception)
        {
            throw new ResourceErrorException("AgentProvisioningTimeout", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw new ResourceErrorException("InvalidConfiguration", exception.Message);
        }
    }
}
