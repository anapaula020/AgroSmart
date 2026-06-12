using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Middleware;

// Shows JWT OR ApiKey security per endpoint (only on [Authorize] actions)
public class SecurityRequirementsOperationFilter : IOperationFilter
{
    private static readonly OpenApiSecurityRequirement JwtReq = new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    };

    private static readonly OpenApiSecurityRequirement ApiKeyReq = new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAnon =
            context.MethodInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ||
            (context.MethodInfo.DeclaringType?.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ?? false);

        if (hasAnon)
        {
            operation.Security.Clear();
            return;
        }

        var hasAuth =
            context.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(true).Any() ||
            (context.MethodInfo.DeclaringType?.GetCustomAttributes<AuthorizeAttribute>(true).Any() ?? false);

        if (!hasAuth) return;

        // Two separate requirements = OR in OpenAPI (either scheme satisfies auth)
        operation.Security.Add(JwtReq);
        operation.Security.Add(ApiKeyReq);
    }
}

// Makes enum schemas show string values instead of integers
public class StringEnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        schema.Enum.Clear();
        foreach (var name in Enum.GetNames(context.Type))
            schema.Enum.Add(new OpenApiString(name));

        schema.Type   = "string";
        schema.Format = null;
    }
}
