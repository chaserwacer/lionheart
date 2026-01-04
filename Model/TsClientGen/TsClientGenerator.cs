using NSwag.CodeGeneration.TypeScript;
using NSwag;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NSwag.CodeGeneration.OperationNameGenerators;
/// <summary>
/// Generates TypeScript client code from an OpenAPI document.
/// This class provides methods to generate TypeScript client code based on the provided OpenAPI JSON.
/// </summary>
public class TsClientGenerator
{

    public async Task<string> SimpleGenerate(string swaggerJson)
    {
        var document = await OpenApiDocument.FromUrlAsync(swaggerJson);

        var settings = new TypeScriptClientGeneratorSettings
        {
            ClassName = "{controller}Client",
            GenerateClientClasses = true,
            //UseGetBaseUrlMethod = true,
            OperationNameGenerator = new SafeOperationNameGenerator()
            
        };

        var generator = new TypeScriptClientGenerator(document, settings);
        var code = generator.GenerateFile();
        await File.WriteAllTextAsync(SourceGen.GetSourceRoot( "ClientApp","src", "lib", "api", "ApiClient.ts"), code);
        return code;
    }


    public async Task<string> Generate(bool includeHttpHeaderParameters, string swaggerJson)
    {
        var document = await OpenApiDocument.FromJsonAsync(swaggerJson);
        if (!includeHttpHeaderParameters)
        {
            foreach (var path in document.Paths.Values)
            {
                path.Parameters = path.Parameters.Where(p => p.Kind != OpenApiParameterKind.Header).ToList();
            }
        }

        var settings = new TypeScriptClientGeneratorSettings
        {
            ClassName = "{controller}ClientBase",
            OperationNameGenerator = new SafeOperationNameGenerator()
        };

        // generate the final client class for each controller, including the parameterless constructor
        var controllerClasses = typeof(TsClientGenerator).Assembly.GetTypes().Where(IsController).ToList();
        var extensionCode = new System.Text.StringBuilder();
        var extendedClasses = new List<string>();
        extensionCode.AppendLine("import * as generated from './apiClient';");
        foreach (var type in controllerClasses)
        {
            var name = type.Name.Replace("Controller", "");
            extensionCode.AppendLine(ExtensionCodeForClient(name));
            extendedClasses.Add(name);
        }

        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                  throw new ApplicationException("Unable to get content folder location");
        var scriptPath = Path.Combine(dir, @"Content/Scripts/apiClientExtensions.ts");
        using var reader = new StreamReader(scriptPath);
        var extraExtensionCode = await reader.ReadToEndAsync();
        extensionCode.AppendLine(extraExtensionCode);

        settings.TypeScriptGeneratorSettings.ExtensionCode = extensionCode.ToString();
        settings.TypeScriptGeneratorSettings.ExtendedClasses = extendedClasses.ToArray();
        settings.TypeScriptGeneratorSettings.ConvertDateToLocalTimezone = true;


        var generator = new TypeScriptClientGenerator(document, settings);
        return generator.GenerateFile();
    }

    private static bool IsController(Type type)
    {
        if (!type.IsAbstract && !type.IsInterface)
        {
            return type.GetCustomAttribute<ApiControllerAttribute>() != null;
        }

        return false;
    }

    private static string ExtensionCodeForClient(string name)
    {
        return $@"
export class {name}Client extends generated.{name}ClientBase {{
    public constructor(baseUrl?: string, http?: {{ fetch(url: RequestInfo, init?: RequestInit): Promise<Response> }}) {{
        if (!baseUrl) {{
            baseUrl = Api.basePath;
        }}
        super(baseUrl, http ?? Api.http);
    }}
}}
";

    }
}