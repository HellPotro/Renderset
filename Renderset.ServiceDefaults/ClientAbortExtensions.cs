using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

public static class ClientAbortExtensions
{
    /// <summary>
    /// Cuando el navegador corta una petición (cierra la pestaña, el iframe
    /// se recarga, Blazor sustituye el HTML prerenderizado…), todo lo que
    /// estaba esperando con ese token lanza OperationCanceledException: el
    /// HttpClient del proxy de Web, la lectura del blob en la API. No es un
    /// error, nadie espera ya la respuesta.
    ///
    /// Se captura aquí para que no llegue al log como fallo ni haga parar el
    /// depurador de Visual Studio en cada recarga. Cualquier cancelación que
    /// NO venga del cliente (un timeout de verdad) sigue su camino.
    /// </summary>
    public static IApplicationBuilder UseClientAbortHandling(
        this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // 499 (Client Closed Request, convención de nginx): sólo
                // para los logs, el cliente ya no está para leerlo.
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = 499;
            }
        });
    }
}
