using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Model.Tools;
using OpenAI.Chat;

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
        public IEnumerable<ChatTool> GetAllChatTools() => _tools.Values.Select(td => td.ChatToolDefinition);
    }

    /// <summary>
    /// Builder for creating a ToolRegistry from registered tool providers in the service collection.
    /// </summary>
    public static class ToolRegistryBuilder
    {
        public async static Task<ToolRegistry> Build(IServiceCollection services)
        {
            var toolProviders = services
                .Select(sd => sd.ServiceType)
                .Where(st => st.GetCustomAttribute<ToolProviderAttribute>() is not null);

            var toolDescriptors = new List<ToolDescriptor>();
            foreach (var toolProvider in toolProviders)
            {
                var methods = toolProvider.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<ToolAttribute>() is not null);

                foreach (var method in methods)
                {
                    var toolAttr = method.GetCustomAttribute<ToolAttribute>()!;
                    var chatToolResult = await OpenAIToolMapper.MapTool(method);
                    
                    if (chatToolResult.IsSuccess)
                    {
                        var descriptor = new ToolDescriptor(
                            toolAttr.Name,
                            toolProvider,
                            method,
                            chatToolResult.Value,
                            RequiresAuthorization(method));
                        toolDescriptors.Add(descriptor);
                    }
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
    /// Descriptor for a chat tool. Produces ChatTool definitions for OpenAI and holds metadata for execution.
    /// </summary>
    public sealed record ToolDescriptor(
        string ToolName,
        Type ServiceType,
        MethodInfo MethodInfo,
        ChatTool ChatToolDefinition,
        bool RequireAuthorization = false

    );
}