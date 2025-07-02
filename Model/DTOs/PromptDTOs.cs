using System.Collections.Generic;

public class GeneratePromptRequest
{
    public string PromptType { get; set; } = string.Empty;

    public Dictionary<string, object>? Inputs { get; set; }
}
public class FunctionToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required object Parameters { get; init; }
}
