namespace Model.Chat.Tools;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolProviderAttribute : Attribute
{
}