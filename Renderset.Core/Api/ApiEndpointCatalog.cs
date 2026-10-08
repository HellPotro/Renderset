namespace Renderset.Core.Api;

/// <summary>
/// Qué hace cada endpoint de la API, en un solo sitio.
///
/// Lo usan dos: ApiService, que lo vuelca en el documento OpenAPI (resumen y
/// descripción de cada operación), y la página API de Web, que lo pinta. Así
/// la documentación no puede quedarse atrás respecto a lo que se enseña: si
/// se añade un endpoint, se añade aquí y sale en los dos.
///
/// Las rutas van como se mapean, con restricciones incluidas
/// ({bundleId:guid}); para comparar con la ruta de ApiExplorer se usa
/// <see cref="ApiEndpointInfo.NormalizedRoute"/>.
/// </summary>
public static class ApiEndpointCatalog
{
    public static IReadOnlyList<ApiEndpointGroup> Groups { get; } =
    [
        new(
            "Render",
            "Emitir documentos. Es lo que usa un ERP: datos (JSON o filas del SELECT) → documento guardado, en HTML y PDF.",
            [
                new("POST", "/api/render/{tenantId}",
                    "Emitir un documento",
                    "Recibe 'data' (JSON jerárquico) o 'rows' (filas planas; se aplica el mapping del report o 'mappingId'), resuelve el preset (por 'presetId' o por contexto: customer, proveedor…), pinta y guarda el documento. Devuelve 201 con documentId, url y pdfUrl. Con output.format = Pdf el PDF se genera en la misma llamada. Errores 400 con code, message y path.",
                    Integration: true),

                new("POST", "/api/render/{tenantId}/file",
                    "Emitir y descargar el fichero",
                    "Igual que POST /api/render/{tenantId}, pero la respuesta es el fichero (PDF o HTML según output.format) en vez del JSON. El id del documento viaja en la cabecera X-Renderset-Document-Id y su URL en Location. Es lo que usa Renderset.Client en GenerateAsync.",
                    Integration: true),

                new("POST", "/api/render/{tenantId}/validate",
                    "Validar sin emitir",
                    "Hace todo lo del render hasta antes de pintar (report, mapping y filas, preset) y no guarda nada. Devuelve { valid, reportId, presetId, presetVersion, errors } con los mismos errores que daría el render. Útil para comprobar una consulta nueva antes de ponerla en producción.",
                    Integration: true)
            ]),

        new(
            "Documentos",
            "Documentos ya emitidos. Son inmutables: lo que se envió es lo que se ve aunque después cambie el diseño.",
            [
                new("GET", "/api/documents/{tenantId}",
                    "Buscar documentos",
                    "Listado sin contenido, los más recientes primero. Filtros: search, reportId, fromUtc, toUtc. Paginado por cursor: { items, nextCursor }; para la página siguiente se repite la petición con cursor = nextCursor.",
                    Integration: true),

                new("GET", "/api/documents/{tenantId}/{documentId}",
                    "Ver el documento (HTML)",
                    "El HTML del documento para abrirlo en el navegador. Es la url que devuelve el render."),

                new("GET", "/api/documents/{tenantId}/{documentId}/download",
                    "Descargar el HTML",
                    "El mismo HTML como fichero adjunto, con su nombre."),

                new("GET", "/api/documents/{tenantId}/{documentId}/metadata",
                    "Datos del documento",
                    "Lo mismo que devolvió el render (report, preset y versión, idioma, fecha, urls) sin el contenido.",
                    Integration: true),

                new("GET", "/api/documents/{tenantId}/{documentId}/pdf",
                    "Descargar el PDF",
                    "El PDF del documento; se genera la primera vez que se pide y después se sirve el guardado. Inline por defecto; ?download=true lo fuerza como descarga. 503 si la API no tiene conversor de PDF.",
                    Integration: true)
            ]),

        new(
            "Compartir (bundles)",
            "Varios documentos detrás de un enlace público con caducidad: la factura, el packing list y el certificado de un envío.",
            [
                new("POST", "/api/bundles/{tenantId}",
                    "Crear un enlace",
                    "Agrupa documentos ya emitidos (documentId) o los emite en la misma llamada (render). Opciones: title, message, culture (idioma de la página), expiresInDays o expiresAtUtc y allowDataDownload (CSV y JSON para el cliente). Devuelve el bundle con su url pública.",
                    Integration: true),

                new("GET", "/api/bundles/{tenantId}",
                    "Buscar bundles",
                    "Listado paginado por cursor con estado y accesos. Filtros: search, status (Active, Expired, Revoked), fromUtc, toUtc."),

                new("GET", "/api/bundles/{tenantId}/{bundleId:guid}",
                    "Detalle de un bundle",
                    "Estado, documentos, accesos y el enlace (si se puede recuperar).",
                    Integration: true),

                new("POST", "/api/bundles/{tenantId}/{bundleId:guid}/revoke",
                    "Desactivar el enlace",
                    "El enlace deja de funcionar al momento; quien lo abra verá un aviso de enlace desactivado.",
                    Integration: true),

                new("POST", "/api/bundles/{tenantId}/{bundleId:guid}/link",
                    "Enlace nuevo",
                    "Emite un token nuevo con caducidad nueva; el enlace anterior deja de valer."),

                new("GET", "/api/sharing/{tenantId}/settings",
                    "Configuración de la página pública",
                    "Logo, colores, mensaje por defecto, pie de contacto y caducidad por defecto del tenant."),

                new("PUT", "/api/sharing/{tenantId}/settings",
                    "Guardar la configuración de la página pública",
                    "Se valida al guardar (colores hex, URLs http/https) y se aplica también a los enlaces ya enviados. Sólo desde Web, con rol de administrador.",
                    ServiceOnly: true),

                new("POST", "/api/sharing/{tenantId}/preview",
                    "Vista previa de la página pública",
                    "HTML de la página con la configuración enviada (sin guardar) y documentos de ejemplo.")
            ]),

        new(
            "Reports",
            "La definición de cada tipo de documento: estructura de datos, secciones, campos y columnas.",
            [
                new("GET", "/api/reports/{tenantId}",
                    "Listar reports",
                    "Todos los reports del tenant."),

                new("GET", "/api/reports/{tenantId}/{reportId}",
                    "Detalle de un report",
                    "Definición, esquema de datos y datos de ejemplo."),

                new("PUT", "/api/reports/{tenantId}/{reportId}",
                    "Crear o actualizar un report",
                    "Guarda definición, dataSchema y sampleData. Siembra en el diccionario las claves que falten (secciones, campos, columnas) sin tocar las traducidas."),

                new("DELETE", "/api/reports/{tenantId}/{reportId}",
                    "Borrar un report",
                    "Borra la definición del report.")
            ]),

        new(
            "Mappings",
            "Cómo pasar de las filas planas de una consulta al JSON del report: niveles, claves de agrupación, campos y tipos.",
            [
                new("GET", "/api/mappings/{tenantId}",
                    "Listar mappings",
                    "Con ?reportId= sólo los de ese report."),

                new("GET", "/api/mappings/{tenantId}/{mappingId}",
                    "Detalle de un mapping",
                    "Niveles, columnas clave, campos y tipos."),

                new("PUT", "/api/mappings/{tenantId}/{mappingId}",
                    "Crear o actualizar un mapping",
                    "Devuelve { mapping, issues }: avisos de lo que el report espera y el mapping no produce (o al revés)."),

                new("DELETE", "/api/mappings/{tenantId}/{mappingId}",
                    "Borrar un mapping",
                    "Las peticiones con filas para ese report empezarán a fallar con mapping.not_configured."),

                new("POST", "/api/mappings/{tenantId}/{mappingId}/preview",
                    "Probar unas filas",
                    "Filas → los JSON que se usarían en el render, sin emitir nada.",
                    Integration: true)
            ]),

        new(
            "Diseño",
            "Presets (diseño de un report), temas, bloques reutilizables y asignaciones por cliente.",
            [
                new("GET", "/api/presets/{tenantId}",
                    "Listar presets",
                    "Todos los diseños del tenant."),

                new("GET", "/api/presets/{tenantId}/{presetId}",
                    "Detalle de un preset",
                    "Configuración sparse (sólo lo que cambia respecto al report) y tema."),

                new("PUT", "/api/presets/{tenantId}/{presetId}",
                    "Guardar un preset",
                    "Crea una versión nueva; los documentos emitidos guardan con qué versión se hicieron."),

                new("DELETE", "/api/presets/{tenantId}/{presetId}",
                    "Borrar un preset",
                    "Baja del preset y de sus asignaciones. Los documentos ya emitidos no cambian. 409 si es el preset por defecto y el report tiene otros: antes hay que marcar otro como predeterminado."),

                new("GET", "/api/presets/resolve/{tenantId}/{reportId}",
                    "Qué preset se usaría",
                    "Con ?contextType=&contextKey= (p. ej. customer y 000123) devuelve el preset asignado a ese contexto o el de por defecto del report."),

                new("GET", "/api/assignments/{tenantId}/{reportId}",
                    "Asignaciones de un report",
                    "Qué preset usa cada contexto (cliente, proveedor…) y cuál es el de por defecto."),

                new("GET", "/api/assignments/{tenantId}/{reportId}/default",
                    "Preset por defecto",
                    "La asignación por defecto del report."),

                new("GET", "/api/assignments/{tenantId}/{reportId}/{contextType}/{contextKey}",
                    "Asignación de un contexto",
                    "La asignación concreta de, por ejemplo, un cliente."),

                new("PUT", "/api/assignments/{tenantId}",
                    "Guardar una asignación",
                    "Asigna un preset a un contexto o como defecto del report. El tipo de contexto puede ser la ruta de un campo de los datos (cliente.codigo): entonces el render elige el preset con el valor de ese campo sin que la petición mande contexto."),

                new("PUT", "/api/assignments/{tenantId}/batch",
                    "Guardar asignaciones en bloque",
                    "Lista de asignaciones (la importación de CSV de la ficha del preset). Todas o ninguna: si alguna apunta a un preset que no existe o es de otro report, 400 con las posiciones. Devuelve { saved }."),

                new("DELETE", "/api/assignments/{tenantId}/id/{assignmentId:long}",
                    "Quitar una asignación",
                    "Por su id. El contexto vuelve a usar el preset por defecto del report."),

                new("GET", "/api/themes/{tenantId}",
                    "Listar temas",
                    "Temas de empresa (colores y tipografía) con cuántos presets los usan."),

                new("GET", "/api/themes/{tenantId}/{themeId}",
                    "Detalle de un tema",
                    "Colores, tipografía y si es el de por defecto."),

                new("PUT", "/api/themes/{tenantId}/{themeId}",
                    "Guardar un tema",
                    "Los presets que lo usan cambian solos en su siguiente documento."),

                new("DELETE", "/api/themes/{tenantId}/{themeId}",
                    "Borrar un tema",
                    "Los presets que lo usaban siguen saliendo igual con la copia que guardan."),

                new("GET", "/api/blocks/{tenantId}",
                    "Listar bloques",
                    "Cabeceras, pies y bloques de contenido reutilizables."),

                new("GET", "/api/blocks/{tenantId}/type/{type}",
                    "Bloques de un tipo",
                    "Header, Footer, Text, Image o Fields."),

                new("GET", "/api/blocks/{tenantId}/{blockId}",
                    "Detalle de un bloque",
                    "Configuración tal como se guarda, con variables sin resolver."),

                new("GET", "/api/blocks/{tenantId}/{blockId}/resolved",
                    "Bloque con variables resueltas",
                    "El bloque con {{variables}} sustituidas para una cultura (?culture=)."),

                new("PUT", "/api/blocks/{tenantId}/{blockId}",
                    "Guardar un bloque",
                    "Crea o actualiza un bloque reutilizable.")
            ]),

        new(
            "Textos e idiomas",
            "Diccionario de traducciones, variables de empresa e idiomas del tenant.",
            [
                new("GET", "/api/cultures/{tenantId}",
                    "Idiomas del tenant",
                    "Culturas activas y cuál es la base."),

                new("PUT", "/api/cultures/{tenantId}/{culture}",
                    "Añadir o cambiar un idioma",
                    "Alta de una cultura (es-ES, en-GB…) o cambio de la base. Sólo desde Web, con rol de administrador.",
                    ServiceOnly: true),

                new("DELETE", "/api/cultures/{tenantId}/{culture}",
                    "Quitar un idioma",
                    "Quita la cultura del tenant. La cultura base no se puede quitar (400). Sólo desde Web, con rol de administrador.",
                    ServiceOnly: true),

                new("GET", "/api/resources/{tenantId}/coverage",
                    "Cobertura de traducciones",
                    "Cuántas claves hay traducidas por ámbito y cultura."),

                new("GET", "/api/resources/{tenantId}/{scope}",
                    "Textos de un ámbito",
                    "Todas las claves de un report (o de 'system:sharing', la página pública) en todas las culturas."),

                new("GET", "/api/resources/{tenantId}/{scope}/catalog",
                    "Diccionario aplanado",
                    "Los textos ya resueltos para una cultura (?culture=&presetId=), como los usa el render."),

                new("PUT", "/api/resources/{tenantId}",
                    "Guardar textos",
                    "Crea o actualiza varias claves de una vez."),

                new("POST", "/api/resources/{tenantId}/copy-missing",
                    "Copiar textos pendientes",
                    "Rellena las claves vacías de una cultura copiando las de otra."),

                new("POST", "/api/resources/{tenantId}/translate-missing",
                    "Traducir textos pendientes",
                    "Traduce con el proveedor configurado las claves vacías de una cultura, sin pisar lo escrito a mano y respetando los {{marcadores}}."),

                new("DELETE", "/api/resources/{tenantId}/{scope}/{key}",
                    "Borrar un texto",
                    "Quita una clave del ámbito."),

                new("GET", "/api/variables/{tenantId}",
                    "Variables de empresa",
                    "Valores comunes ({{empresa.nif}}, {{empresa.telefono}}…) que usan los bloques."),

                new("PUT", "/api/variables/{tenantId}/{key}",
                    "Guardar una variable",
                    "Si es traducible, siembra su clave en el diccionario común."),

                new("DELETE", "/api/variables/{tenantId}/{key}",
                    "Borrar una variable",
                    "Los bloques que la usen la dejarán sin sustituir.")
            ]),

        new(
            "Administración",
            "Claves de API. Sólo con clave de servicio (RenderSet Web) y un usuario administrador del tenant: una clave de tenant no puede crear otras.",
            [
                new("GET", "/api/keys/{tenantId}",
                    "Listar claves",
                    "Nombre, prefijo visible, caducidad y último uso. Nunca la clave.",
                    ServiceOnly: true),

                new("POST", "/api/keys/{tenantId}",
                    "Crear una clave",
                    "{ name, expiresInDays? } → la clave en claro, una sola vez. Se guarda sólo su hash.",
                    ServiceOnly: true),

                new("POST", "/api/keys/{tenantId}/{keyId:guid}/revoke",
                    "Revocar una clave",
                    "Deja de valer al momento.",
                    ServiceOnly: true)
            ]),

        new(
            "Enlace público",
            "Lo que abre el cliente final. Sin API key: la autorización es el token del enlace.",
            [
                new("GET", "/share/{bundleId:guid}/{token}",
                    "Página del bundle",
                    "Lista de documentos, visor, descargas y, si el bundle lo permite, CSV y JSON.",
                    Public: true),

                new("GET", "/share/{bundleId:guid}/{token}/documents/{position:int}",
                    "Un documento del bundle",
                    "El HTML del documento en esa posición (1, 2…), para el visor.",
                    Public: true),

                new("GET", "/share/{bundleId:guid}/{token}/documents/{position:int}/download",
                    "Descargar un documento",
                    "El HTML de ese documento como fichero (sin conversor de PDF).",
                    Public: true),

                new("GET", "/share/{bundleId:guid}/{token}/documents/{position:int}/pdf",
                    "PDF de un documento",
                    "Descarga del PDF de ese documento.",
                    Public: true),

                new("GET", "/share/{bundleId:guid}/{token}/pdf",
                    "Todo en un PDF",
                    "Los documentos del bundle unidos en un único PDF.",
                    Public: true),

                new("GET", "/share/{bundleId:guid}/{token}/download",
                    "Todo en un ZIP",
                    "Los documentos del bundle en HTML dentro de un ZIP.",
                    Public: true)
            ]),

        new(
            "DeCA",
            "Documento electrónico de control administrativo (Orden FOM/2861/2012, Resolución de 5 de junio de 2026). El PDF se genera y congela al emitir, con fecha y hora en los metadatos y un QR que lo descarga sin login.",
            [
                new("GET", "/api/deca/{tenantId}",
                    "Buscar DeCA",
                    "Listado sin contenido, los más recientes primero. Filtros: search (número, referencia, cargador, transportista o matrícula), from y to (fecha del transporte), skip y take (máx. 200). Devuelve { items, total }.",
                    Integration: true),

                new("GET", "/api/deca/{tenantId}/{decaId}",
                    "Ver un DeCA",
                    "Datos de la versión vigente, versiones con su SHA-256, eventos, el enlace del QR y si descarga ahora mismo.",
                    Integration: true),

                new("POST", "/api/deca/{tenantId}/validate",
                    "Validar sin emitir",
                    "Las mismas comprobaciones que al emitir (NIF con letra de control, matrículas, fecha anterior al transporte, envíos con origen, destino, mercancía y peso). Devuelve { isValid, issues }; los avisos llevan isWarning = true.",
                    Integration: true),

                new("POST", "/api/deca/{tenantId}",
                    "Emitir un DeCA",
                    "Valida, genera el PDF (máx. 5 MB) con fecha y hora de creación y el QR, y lo guarda. Devuelve 201 con el DeCA y publicUrl. Errores de datos: 400 con code, path y message. Sin conversor de PDF: 503, y no se emite nada.",
                    Integration: true),

                new("POST", "/api/deca/{tenantId}/{decaId}/finish",
                    "Terminar el servicio",
                    "Marca el fin del servicio (endedAtUtc, o ahora). El QR sigue descargando durante el plazo configurado, siete días naturales como mínimo; el DeCA se conserva un año igualmente.",
                    Integration: true),

                new("GET", "/api/deca/{tenantId}/{decaId}/pdf",
                    "Descargar el PDF",
                    "El PDF tal como se emitió, para el emisor: sin plazo (se conserva un año). ?version=N para una versión anterior y ?download=true como adjunto.",
                    Integration: true),

                new("GET", "/api/deca/{tenantId}/template",
                    "Ver el diseño del DeCA",
                    "Configuración, tema y textos del diseño del tenant, o el base con su logo y colores si no tiene. version va de vuelta al guardar.",
                    Integration: false),

                new("PUT", "/api/deca/{tenantId}/template",
                    "Guardar el diseño del DeCA",
                    "Guarda una versión nueva ({ configuration, theme, texts, expectedVersion }). Lo obligatorio que se oculte se vuelve a mostrar; si aun así faltase algo, 400 con la lista. 409 si otro ha guardado entretanto.",
                    Integration: false),

                new("DELETE", "/api/deca/{tenantId}/template",
                    "Volver al diseño base",
                    "Guarda una versión que restablece el diseño base (?expectedVersion=N). Los DeCA ya emitidos no cambian.",
                    Integration: false),

                new("GET", "/q/{code}",
                    "Descargar el DeCA (QR)",
                    "El PDF del DeCA, sin autenticación ni interacción: es la URL del QR. Sirve el PDF guardado al emitir. 410 cuando ha pasado el plazo tras terminar el servicio.",
                    Public: true)
            ]),

        new(
            "Imágenes",
            "El logo del tenant subido a RenderSet en vez de enlazado desde otra web. Se guarda por el hash de su contenido, así que su URL no cambia mientras no cambie la imagen.",
            [
                new("POST", "/api/assets/{tenantId}/logo",
                    "Subir el logo",
                    "multipart/form-data con el campo file: PNG, JPG, WebP o GIF de 1 MB como mucho (SVG no). Devuelve { url, assetId, contentType, sizeBytes }; la url es la que se pone como logo. Rol Admin.",
                    Integration: false),

                new("GET", "/assets/{tenantId}/{file}",
                    "Ver una imagen",
                    "La imagen subida, sin autenticación (la pintan el visor público y el conversor de PDF) y cacheable para siempre.",
                    Public: true)
            ])
    ];

    /// <summary>
    /// Busca un endpoint por método y ruta, tal como los da ApiExplorer
    /// ("api/render/{tenantId}", sin barra inicial ni restricciones).
    /// </summary>
    public static ApiEndpointInfo? Find(
        string? method,
        string? route)
    {
        if (string.IsNullOrWhiteSpace(method) || route is null)
            return null;

        var normalized = ApiEndpointInfo.Normalize(route);

        return Groups
            .SelectMany(x => x.Endpoints)
            .FirstOrDefault(x =>
                string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.NormalizedRoute, normalized, StringComparison.OrdinalIgnoreCase));
    }
}


public sealed record ApiEndpointGroup(
    string Name,
    string Description,
    IReadOnlyList<ApiEndpointInfo> Endpoints);


/// <param name="Integration">Lo que normalmente usa un ERP o una aplicación externa.</param>
/// <param name="ServiceOnly">Sólo con clave de servicio.</param>
/// <param name="Public">Sin API key (enlace público).</param>
public sealed record ApiEndpointInfo(
    string Method,
    string Route,
    string Summary,
    string Description,
    bool Integration = false,
    bool ServiceOnly = false,
    bool Public = false)
{
    public string NormalizedRoute =>
        Normalize(Route);

    /// <summary>
    /// Sin barra inicial y sin restricciones: "{bundleId:guid}" → "{bundleId}".
    /// </summary>
    public static string Normalize(
        string route)
    {
        var trimmed = route.Trim().TrimStart('/');

        return System.Text.RegularExpressions.Regex.Replace(
            trimmed,
            @"\{([^}:?]+)[^}]*\}",
            "{$1}");
    }
}
