using lionheart.Functional;
using lionheart.Services.Llm;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Model.Tools;

namespace Model.Chat.Tools
{
    /// <summary>
    /// Registry for chat tools, allowing retrieval of tool descriptors by name.
    /// </summary>
    public sealed class ToolRegistry(IReadOnlyDictionary<string, ToolDescriptor> tools)
    {
        private readonly IReadOnlyDictionary<string, ToolDescriptor> _tools = tools;

        public bool TryGet(string toolName, out ToolDescriptor? descriptor)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                descriptor = null;
                return false;
            }

            return _tools.TryGetValue(toolName, out descriptor);
        }

        public IEnumerable<ToolDescriptor> GetAllTools() => _tools.Values;

        /// <summary>Every tool, as the model is offered them, in name order.</summary>
        /// <remarks>
        /// Ordered because tools are the first thing a prompt cache sees: a list whose order moved
        /// between two requests would miss the cache for everything after it.
        /// </remarks>
        public IReadOnlyList<ToolDefinition> GetAllToolDefinitions() =>
            [.. _tools.Values.Select(td => td.Definition).OrderBy(definition => definition.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Builder for creating a ToolRegistry from registered tool providers in the service collection.
    /// </summary>
    public static class ToolRegistryBuilder
    {
        public async static Task<ToolRegistry> Build(IServiceCollection services)
        {
            var toolProviders = services
                .Select(sd => sd.ImplementationType ?? sd.ServiceType)
                .Distinct()
                .Where(st => st is not null && st.GetCustomAttribute<ToolProviderAttribute>() is not null);

            var toolDescriptors = new List<ToolDescriptor>();
            foreach (var toolProvider in toolProviders)
            {
                if (toolProvider.IsInterface || toolProvider.IsAbstract || toolProvider is null)
                    continue;
                var methods = toolProvider.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<ToolAttribute>() is not null);

                foreach (var method in methods)
                {
                    var toolAttr = method.GetCustomAttribute<ToolAttribute>()!;

                    // A method whose signature the mapper cannot describe is left out of the
                    // registry, so the model is never offered a tool it could not call.
                    await ToolSchemaMapper.MapTool(method).TapAsync(definition =>
                        toolDescriptors.Add(new ToolDescriptor(
                            toolAttr.Name,
                            toolProvider,
                            method,
                            definition,
                            RequiresAuthorization(method))));
                }
            }
            var tools = toolDescriptors.ToDictionary(t => t.ToolName, StringComparer.Ordinal);
            return new ToolRegistry(tools);
        }
        /// <summary>
        /// Determines if a method requires authorization(an identity user) based on its parameters.
        private static bool RequiresAuthorization(MethodInfo method)
        {
            var methodParams = method.GetParameters();
            for (var i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                if (param.ParameterType == typeof(IdentityUser))
                    return true;
            }
            return false;
        }
    }


    /// <summary>
    /// Descriptor for a chat tool: what the model is told about it, and what the executor needs to
    /// invoke it.
    /// </summary>
    public sealed record ToolDescriptor(
        string ToolName,
        Type ServiceType,
        MethodInfo MethodInfo,
        ToolDefinition Definition,
        bool RequireAuthorization = false

    );
}