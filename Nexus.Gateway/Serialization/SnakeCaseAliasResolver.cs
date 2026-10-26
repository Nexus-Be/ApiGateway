namespace Nexus.Gateway.Serialization;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Keeps camelCase as the Gateway's single JSON convention while still accepting legacy
/// <c>snake_case</c> request payloads (Tolerant Reader).
/// </summary>
/// <remarks>
/// For every writable property, a read-side-only alias named in <c>snake_case</c> is registered on
/// the contract. The alias carries only a setter plus a write suppressor, so it participates in
/// deserialization and is never emitted — responses stay 100% camelCase.
/// </remarks>
public static class SnakeCaseAliasResolver
{
    /// <summary>Installs the alias modifier on <paramref name="options"/> ahead of the default resolver.</summary>
    public static void Apply(JsonSerializerOptions options)
    {
        options.TypeInfoResolverChain.Insert(0, new DefaultJsonTypeInfoResolver
        {
            Modifiers = { AddSnakeCaseAliases },
        });
    }

    /// <summary>Registers snake_case aliases for every writable property of <paramref name="typeInfo"/>.</summary>
    public static void AddSnakeCaseAliases(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Properties.Count == 0)
        {
            return;
        }

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in typeInfo.Properties)
        {
            taken.Add(property.Name);
        }

        var pending = new List<(string Name, JsonPropertyInfo Source)>();

        foreach (var property in typeInfo.Properties)
        {
            if (property.Set is null)
            {
                continue;
            }

            var alias = ToSnakeCase(property.Name);

            // Only multi-word names have a casing variant to support. Single-word names
            // (Message -> message) are already covered by the case-insensitive binder, and
            // aliasing them would collide with the camelCase contract name.
            if (alias.IndexOf('_') < 0)
            {
                continue;
            }

            // Never shadow a real property that already uses the snake_case spelling.
            if (!taken.Add(alias))
            {
                continue;
            }

            pending.Add((alias, property));
        }

        foreach (var (name, source) in pending)
        {
            var alias = typeInfo.CreateJsonPropertyInfo(source.PropertyType, name);
            alias.Get = source.Get;
            alias.Set = source.Set;

            // Read-side only: the alias is never emitted, so responses stay 100% camelCase.
            alias.ShouldSerialize = static (_, _) => false;

            typeInfo.Properties.Add(alias);
        }
    }

    /// <summary>Converts <paramref name="name"/> from PascalCase/camelCase to snake_case.</summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (char.IsUpper(current))
            {
                var previousIsLower = i > 0 && char.IsLower(name[i - 1]);
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (i > 0 && (previousIsLower || nextIsLower))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
