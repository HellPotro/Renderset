var builder = DistributedApplication.CreateBuilder(args);

// Clave de servicio con la que Web llama a la API. Secreta: se guarda en los
// user-secrets de AppHost (Parameters:renderset-service-key) o el panel de
// Aspire la pide al arrancar. Mínimo 32 caracteres.
var serviceKey = builder.AddParameter("renderset-service-key", secret: true);

// Par de claves ECDSA P-256 del token de usuario (ver SEGURIDAD.md, fase B):
// Web firma con la privada qué usuario, tenant y rol hace cada llamada y la
// API lo comprueba con la pública. Secretas, en los user-secrets de AppHost
// (Parameters:renderset-user-token-private-key / -public-key), en Base64 de
// una línea o PEM.
var userTokenPrivateKey = builder.AddParameter("renderset-user-token-private-key", secret: true);

// Clave de servicio propia del portal DeCA: se puede revocar sin tocar la
// de Web. User-secrets de AppHost (Parameters:renderset-deca-service-key),
// mínimo 32 caracteres.
var decaServiceKey = builder.AddParameter("renderset-deca-service-key", secret: true);
var userTokenPublicKey = builder.AddParameter("renderset-user-token-public-key", secret: true);

// Conversión a PDF: Chromium headless detrás de una API HTTP. En Azure va
// como Container App aparte (ver BUNDLES.md); aquí, como contenedor local.
var gotenberg = builder.AddContainer("gotenberg", "gotenberg/gotenberg", "8")
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithHttpHealthCheck("/health");

var apiService = builder.AddProject<Projects.Renderset_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Pdf__GotenbergUrl", gotenberg.GetEndpoint("http"))
    .WithEnvironment("Security__ServiceKeys__0__Name", "web")
    .WithEnvironment("Security__ServiceKeys__0__Key", serviceKey)
    .WithEnvironment("Security__ServiceKeys__1__Name", "deca")
    .WithEnvironment("Security__ServiceKeys__1__Key", decaServiceKey)
    .WithEnvironment("Security__UserTokenPublicKey", userTokenPublicKey)
    .WaitFor(gotenberg);

var web = builder.AddProject<Projects.Renderset_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("ReportingApi", apiService.GetEndpoint("https"))
    .WithEnvironment("ReportingApiKey", serviceKey)
    .WithEnvironment("Security__UserTokenPrivateKey", userTokenPrivateKey)
    .WaitFor(apiService);

// El QR de la cabecera enlaza al visor de Web (/documents/{id}). Sólo es la
// URL: no hay WaitFor, así que no se crea un ciclo entre los dos proyectos.
// En Azure, DocumentLinks__BaseUrl con el dominio público de Web.
// Con el perfil http de AppHost, Web no tiene endpoint https: se usa el http.
var webHttps = web.GetEndpoint("https");

apiService.WithEnvironment(
    "DocumentLinks__BaseUrl",
    webHttps.Exists ? webHttps : web.GetEndpoint("http"));

// DeCA: el QR de cada DeCA lleva https://<api>/q/{código}. En Azure,
// Deca__PublicBaseUrl con el dominio corto y definitivo (deca.tudominio),
// apuntando a la API con HTTPS.
var apiHttps = apiService.GetEndpoint("https");

apiService.WithEnvironment(
    "Deca__PublicBaseUrl",
    apiHttps.Exists ? apiHttps : apiService.GetEndpoint("http"));

// Logos subidos (Marca / Página pública): la API los sirve en /assets, así
// que su dirección pública es la de la API. En Azure, Assets__PublicBaseUrl
// (o DocumentSharing__PublicBaseUrl) con el dominio público de la API.
apiService.WithEnvironment(
    "Assets__PublicBaseUrl",
    apiHttps.Exists ? apiHttps : apiService.GetEndpoint("http"));

// Portal DeCA: aplicación propia, con su login (mismos usuarios que Web) y
// su clave de servicio. Los usuarios están en la base de datos de
// RenderSet: ConnectionStrings:RenderSet en sus user-secrets, como en Web.
var decaWeb = builder.AddProject<Projects.Renderset_Deca_Web>("decaweb")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("ReportingApi", apiHttps)
    .WithEnvironment("ReportingApiKey", decaServiceKey)
    .WithEnvironment("Security__UserTokenPrivateKey", userTokenPrivateKey)
    .WaitFor(apiService);

// Enlace "DeCA" del menú de Web. Sólo la URL: sin WaitFor.
var decaWebHttps = decaWeb.GetEndpoint("https");

web.WithEnvironment(
    "Deca__PortalUrl",
    decaWebHttps.Exists ? decaWebHttps : decaWeb.GetEndpoint("http"));

builder.Build().Run();
