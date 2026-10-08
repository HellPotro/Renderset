using FluentAssertions;
using Renderset.Core.Api;

namespace Renderset.Tests.Core;

public sealed class ApiEndpointCatalogTests
{
    [Theory]
    [InlineData("POST", "api/render/{tenantId}/file", "Emitir y descargar el fichero")]
    [InlineData("POST", "api/bundles/{tenantId}/{bundleId}/revoke", "Desactivar el enlace")]
    [InlineData("get", "api/documents/{tenantId}/{documentId}/pdf", "Descargar el PDF")]
    public void Find_ShouldMatchApiExplorerRoutes(
        string method,
        string route,
        string summary)
    {
        // ApiExplorer da la ruta sin barra inicial y sin restricciones.
        ApiEndpointCatalog.Find(method, route)!
            .Summary.Should().Be(summary);
    }

    [Fact]
    public void Catalog_ShouldNotRepeatAnEndpoint()
    {
        ApiEndpointCatalog.Groups
            .SelectMany(x => x.Endpoints)
            .GroupBy(x => $"{x.Method} {x.NormalizedRoute}", StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Catalog_ShouldDescribeEveryEndpoint()
    {
        ApiEndpointCatalog.Groups
            .SelectMany(x => x.Endpoints)
            .Should()
            .OnlyContain(x =>
                !string.IsNullOrWhiteSpace(x.Summary) &&
                !string.IsNullOrWhiteSpace(x.Description));
    }
}
