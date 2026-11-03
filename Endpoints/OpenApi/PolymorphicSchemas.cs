using System.Reflection;
using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace lionheart.Endpoints.OpenApi;

/// <summary>
/// Teaches Swagger the polymorphism that System.Text.Json already knows about, by reading the same
/// <see cref="JsonPolymorphicAttribute"/> and <see cref="JsonDerivedTypeAttribute"/> declarations
/// that drive serialization.
/// </summary>
/// <remarks>
/// Without this the document describes every sealed hierarchy as an empty object: a response typed
/// as a base record emits <c>{}</c>, and the generated TypeScript client silently drops the "$type"
/// discriminator. That breaks the interpretation resume path in particular, which round-trips a
/// parsed draft back to the server and needs its discriminators intact.
///
/// Deriving the schema from the serialization attributes rather than a second hand-maintained list
/// is the point — the two cannot drift apart into a document that lies about the wire format.
/// </remarks>
public static class PolymorphicSchemas
{
    public static void DescribePolymorphicTypes(this SwaggerGenOptions options)
    {
        options.UseAllOfForInheritance();
        options.UseOneOfForPolymorphism();

        options.SelectSubTypesUsing(baseType =>
            baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                    .Select(derived => derived.DerivedType)
                    .ToList());

        options.SelectDiscriminatorNameUsing(baseType =>
            baseType.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false)
                   ?.TypeDiscriminatorPropertyName);

        options.SelectDiscriminatorValueUsing(subType =>
            DeclaredDiscriminator(subType)?.ToString());
    }

    /// <summary>
    /// The discriminator is declared on the base, not on the subtype, so this walks up until it
    /// finds the ancestor that names this type.
    /// </summary>
    private static object? DeclaredDiscriminator(Type subType)
    {
        for (var ancestor = subType.BaseType; ancestor is not null; ancestor = ancestor.BaseType)
        {
            var match = ancestor.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                                .FirstOrDefault(derived => derived.DerivedType == subType);
            if (match is not null) return match.TypeDiscriminator;
        }

        return null;
    }
}
