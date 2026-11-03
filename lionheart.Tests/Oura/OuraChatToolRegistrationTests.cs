using lionheart.Services.Oura;
using Microsoft.Extensions.DependencyInjection;
using Model.Chat.Tools;

namespace lionheart.Tests.Oura;

/// <summary>
/// That the Oura chat tools are actually reachable by the model.
/// </summary>
/// <remarks>
/// Worth asserting because the registry fails quietly: a tool whose signature the OpenAI mapper
/// rejects is skipped without a word, so the only symptom is a model that never asks about Oura.
/// </remarks>
public class OuraChatToolRegistrationTests
{
    [Fact]
    public async Task Both_Oura_tools_are_registered_under_the_names_the_model_calls()
    {
        var registry = await BuildRegistryAsync();

        Assert.True(registry.TryGet("GetOuraDays", out _));
        Assert.True(registry.TryGet("GetOuraSleepNights", out _));
    }

    [Fact]
    public async Task Each_Oura_tool_resolves_to_a_method_that_needs_the_signed_in_athlete()
    {
        // The registry decides a tool is user-scoped by looking for an IdentityUser parameter. A
        // tool that lost it would be invoked without one and read another athlete's data, or none.
        var registry = await BuildRegistryAsync();

        foreach (var name in new[] { "GetOuraDays", "GetOuraSleepNights" })
        {
            Assert.True(registry.TryGet(name, out var descriptor));
            Assert.NotNull(descriptor);
            Assert.True(descriptor.RequireAuthorization, $"{name} should be scoped to the signed-in athlete.");
            Assert.Equal(typeof(OuraReadService), descriptor.ServiceType);
        }
    }

    [Fact]
    public async Task An_Oura_tools_declared_interface_is_the_one_registered_in_DI()
    {
        // How the executor resolves a provider: it takes the implementation type's first interface
        // and asks the container for it. A second interface on the service would break that.
        var registry = await BuildRegistryAsync();

        Assert.True(registry.TryGet("GetOuraDays", out var descriptor));
        Assert.Equal(typeof(IOuraReadService), Assert.Single(descriptor!.ServiceType.GetInterfaces()));
    }

    private static Task<ToolRegistry> BuildRegistryAsync()
    {
        var services = new ServiceCollection();
        services.AddTransient<IOuraReadService, OuraReadService>();

        return ToolRegistryBuilder.Build(services);
    }
}
