using Renderset.Core.Api;

namespace Renderset.Api.OpenApi;

/// <summary>
/// Documento OpenAPI con el resumen y la descripción de cada operación
/// sacados de <see cref="ApiEndpointCatalog"/>, el mismo texto que enseña la
/// página API de Web. En desarrollo: /openapi/v1.json.
/// </summary>
public static class RendersetOpenApi
{
    private const string Overview =
        "API de RenderSet: de datos estructurados (JSON o las filas planas de un SELECT) a documentos " +
        "HTML y PDF con el diseño configurado en RenderSet.\n\n" +
        "Autenticación: cabecera X-Api-Key con una clave de tenant (rs_live_…), que sólo da acceso a su " +
        "tenant, o con la clave de servicio de RenderSet Web. El {tenantId} de la ruta tiene que ser el de " +
        "la clave.\n\n" +
        "Errores: 400 con una lista de { code, message, path, expected, actual }. 401 sin clave, 403 con " +
        "clave de otro tenant.\n\n" +
        "Desde .NET (también .NET Framework 4.6.1+ y WinForms): Renderset.Client.";

    public static IServiceCollection AddRendersetOpenApi(
        this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                if (document.Info is not null)
                {
                    document.Info.Title = "RenderSet API";
                    document.Info.Description = Overview;
                }

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var info =
                    ApiEndpointCatalog.Find(
                        context.Description.HttpMethod,
                        context.Description.RelativePath);

                if (info is not null)
                {
                    operation.Summary = info.Summary;
                    operation.Description = info.ServiceOnly
                        ? info.Description + " Requiere clave de servicio."
                        : info.Description;
                }

                return Task.CompletedTask;
            });
        });
}
