using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Nexus.Gateway.MockEngine;

public sealed class MockEngineDocumentFilter(IMockEngine mockEngine) : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var definitions = mockEngine.GetDefinitions();

        foreach (var def in definitions)
        {
            if (!string.Equals(context.DocumentName, "v1", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(def.Service, context.DocumentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cleanPath = def.NormalizedPath;
            if (!swaggerDoc.Paths.ContainsKey(cleanPath))
            {
                swaggerDoc.Paths.Add(cleanPath, new OpenApiPathItem());
            }

            var pathItem = swaggerDoc.Paths[cleanPath];
            var operationType = def.NormalizedMethod switch
            {
                "POST" => OperationType.Post,
                "PUT" => OperationType.Put,
                "DELETE" => OperationType.Delete,
                "PATCH" => OperationType.Patch,
                _ => OperationType.Get
            };

            // Don't overwrite if manually mapped via Controller
            if (pathItem.Operations.ContainsKey(operationType))
            {
                continue;
            }

            var tagName = string.IsNullOrEmpty(def.Service)
                ? "Mock"
                : char.ToUpperInvariant(def.Service[0]) + def.Service[1..];

            var operation = new OpenApiOperation
            {
                Tags = [new OpenApiTag { Name = tagName }],
                Summary = string.IsNullOrEmpty(def.Description) ? $"[Mock] {def.NormalizedMethod} {cleanPath}" : def.Description,
                Description = $"[Mock Engine] Service: {def.Service} | Policy: {def.Policy} | Status: {def.StatusCode}"
            };

            // Path parameters
            var segments = cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                if (seg.StartsWith('{') && seg.EndsWith('}'))
                {
                    var paramName = seg[1..^1].Trim();
                    var colon = paramName.IndexOf(':');
                    if (colon > 0) paramName = paramName[..colon];

                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = paramName,
                        In = ParameterLocation.Path,
                        Required = true,
                        Schema = new OpenApiSchema { Type = "string" }
                    });
                }
            }

            // Request body example
            if (def.Request.HasValue && operationType is OperationType.Post or OperationType.Put or OperationType.Patch)
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString(def.Request.Value.GetRawText())
                        }
                    }
                };
            }

            // Response example
            var respJson = def.Response.HasValue ? def.Response.Value.GetRawText() : "{}";
            operation.Responses.Add(def.StatusCode.ToString(), new OpenApiResponse
            {
                Description = def.Message ?? "Mock response",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Example = new OpenApiString(respJson)
                    }
                }
            });

            // Security requirement
            if (!string.Equals(def.Policy, "public", StringComparison.OrdinalIgnoreCase))
            {
                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        }] = Array.Empty<string>()
                    }
                ];
            }

            pathItem.AddOperation(operationType, operation);
        }
    }
}
