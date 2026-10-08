/* =========================================================
   RENDERSET - BARRA DEL VISOR DE DOCUMENTOS

   La misma para el visor interno (Web, /documents/{id}) y para la página
   pública de un bundle (/share/...). Web la carga como asset de la RCL;
   la API la incrusta en la página pública, igual que el CSS.

   Marcado: DocumentViewerToolbar.razor. Raíz con data-rs-viewer que
   contiene la barra y el iframe (data-rs-viewer-frame).

   Lee los datos del documento del propio iframe (mismo origen):
     - script#rs-document-data       JSON del documento
     - script.dynamic-report-data    uno por tabla, para el CSV
   Cada vez que el iframe carga otro documento se vuelven a leer.

   Sin dependencias y en ES5, como el resto de la página pública.
   ========================================================= */

(function () {

    if (window.RenderSetViewer)
        return;

    function query(root, name) {
        return root.querySelector("[data-rs-viewer-" + name + "]");
    }

    /* Una tabla dentro de una sección que se repite aparece una vez por
       elemento con el mismo id: se juntan, para que exportar "Líneas" de
       una factura con tres albaranes dé todas las líneas. */
    function readTables(doc) {

        var blocks = doc.querySelectorAll("script.dynamic-report-data");
        var byId = {};
        var order = [];

        for (var i = 0; i < blocks.length; i++) {

            var payload;

            try { payload = JSON.parse(blocks[i].textContent); }
            catch (e) { continue; }

            if (!byId[payload.id]) {
                byId[payload.id] = {
                    id: payload.id,
                    title: payload.title || payload.id,
                    columns: payload.columns || [],
                    rows: []
                };
                order.push(payload.id);
            }

            byId[payload.id].rows = byId[payload.id].rows.concat(payload.rows || []);
        }

        return order.map(function (id) { return byId[id]; });
    }

    /* Decimales del idioma del documento y sin miles: es lo único que una
       hoja de cálculo reconoce como número al abrir el CSV. */
    function formatCell(value, culture) {

        if (value === null || value === undefined)
            return "";

        if (typeof value === "number")
            return value.toLocaleString(culture, { useGrouping: false, maximumFractionDigits: 6 });

        if (typeof value === "boolean")
            return value ? "1" : "0";

        return String(value);
    }

    function quote(value) {
        return '"' + String(value === undefined ? "" : value).replace(/"/g, '""') + '"';
    }

    function slug(text) {
        var value = String(text || "").replace(/[^\w\-]+/g, "-").replace(/^-+|-+$/g, "").toLowerCase();
        return value || "documento";
    }

    function save(fileName, text, type) {
        var blob = new Blob([text], { type: type });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");

        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.parentNode.removeChild(link);

        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }

    function copy(text) {

        if (navigator.clipboard && window.isSecureContext)
            return navigator.clipboard.writeText(text);

        /* Sin API de portapapeles (http, navegadores viejos). */
        return new Promise(function (resolve, reject) {
            var area = document.createElement("textarea");
            area.value = text;
            area.setAttribute("readonly", "");
            area.style.position = "fixed";
            area.style.opacity = "0";
            document.body.appendChild(area);
            area.select();

            try {
                document.execCommand("copy") ? resolve() : reject();
            } catch (e) {
                reject(e);
            } finally {
                area.parentNode.removeChild(area);
            }
        });
    }

    /* Un único par de escuchas para toda la página: en Web el visor se
       vuelve a crear al cambiar de documento y no deben acumularse. */
    var globalCloseInstalled = false;

    function installGlobalClose() {

        if (globalCloseInstalled)
            return;

        globalCloseInstalled = true;

        function closeAll() {
            var open = document.querySelectorAll("[data-rs-viewer-menu].is-open");
            for (var i = 0; i < open.length; i++)
                open[i].classList.remove("is-open");
        }

        document.addEventListener("click", closeAll);

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape")
                closeAll();
        });
    }

    function init(root) {

        if (!root || root.getAttribute("data-rs-viewer-ready") === "1")
            return;

        var frame = query(root, "frame");

        if (!frame)
            return;

        root.setAttribute("data-rs-viewer-ready", "1");

        var title = query(root, "title");
        var csvMenu = query(root, "csv");
        var csvPanel = query(root, "csv-panel");
        var jsonMenu = query(root, "json");
        var jsonDownload = query(root, "json-download");
        var jsonCopy = query(root, "json-copy");
        var printButton = query(root, "print");
        var note = query(root, "note");

        var tables = [];
        var documentJson = null;
        var noteTimer = null;

        function culture() {
            var doc = null;
            try { doc = frame.contentDocument; } catch (e) { doc = null; }

            return root.getAttribute("data-culture") ||
                (doc && doc.documentElement.lang) ||
                document.documentElement.lang ||
                "es";
        }

        function currentTitle() {
            return title ? title.textContent.trim() : "";
        }

        function showNote(message) {
            if (!note || !message)
                return;

            note.textContent = message;
            note.hidden = false;

            clearTimeout(noteTimer);
            noteTimer = setTimeout(function () { note.hidden = true; }, 2500);
        }

        function closeMenus(except) {
            var menus = root.querySelectorAll("[data-rs-viewer-menu]");
            for (var i = 0; i < menus.length; i++) {
                if (menus[i] !== except)
                    menus[i].classList.remove("is-open");
            }
        }

        function exportTable(table) {
            var lines = [table.columns.map(quote).join(";")];
            var code = culture();

            table.rows.forEach(function (row) {
                lines.push(table.columns
                    .map(function (column) { return quote(formatCell(row[column], code)); })
                    .join(";"));
            });

            /* El BOM hace que Excel abra el fichero como UTF-8. */
            save(slug(table.title) + ".csv", "\uFEFF" + lines.join("\r\n"), "text/csv;charset=utf-8");
        }

        function buildCsvMenu() {

            if (!csvMenu || !csvPanel)
                return;

            csvPanel.textContent = "";
            csvMenu.hidden = tables.length === 0;

            tables.forEach(function (table) {
                var button = document.createElement("button");
                button.type = "button";
                button.textContent = table.title + " (" + table.rows.length + ")";
                button.addEventListener("click", function () {
                    closeMenus();
                    exportTable(table);
                });
                csvPanel.appendChild(button);
            });

            if (tables.length > 1) {
                var all = document.createElement("button");
                all.type = "button";
                all.className = "is-all";
                all.textContent = csvPanel.getAttribute("data-all-label") || "*";
                all.addEventListener("click", function () {
                    closeMenus();
                    tables.forEach(exportTable);
                });
                csvPanel.appendChild(all);
            }
        }

        function readData() {

            var doc = null;
            try { doc = frame.contentDocument; } catch (e) { doc = null; }

            tables = doc ? readTables(doc) : [];

            var data = doc ? doc.getElementById("rs-document-data") : null;
            documentJson = data ? data.textContent.trim() : null;

            closeMenus();
            buildCsvMenu();

            if (jsonMenu)
                jsonMenu.hidden = !documentJson;
        }

        frame.addEventListener("load", readData);

        /* Si el iframe ya había cargado antes de llegar aquí. */
        try {
            if (frame.contentDocument && frame.contentDocument.readyState === "complete")
                readData();
        } catch (e) { /* otro origen: sin datos */ }

        var toggles = root.querySelectorAll("[data-rs-viewer-toggle]");

        for (var i = 0; i < toggles.length; i++) {
            toggles[i].addEventListener("click", function (e) {
                e.stopPropagation();

                var menu = this.closest("[data-rs-viewer-menu]");
                closeMenus(menu);

                if (menu)
                    menu.classList.toggle("is-open");
            });
        }

        installGlobalClose();

        if (jsonDownload) {
            jsonDownload.addEventListener("click", function () {
                closeMenus();

                if (documentJson)
                    save(slug(currentTitle()) + ".json", documentJson, "application/json;charset=utf-8");
            });
        }

        if (jsonCopy) {
            jsonCopy.addEventListener("click", function () {
                closeMenus();

                if (!documentJson)
                    return;

                copy(documentJson).then(function () {
                    showNote(note ? note.getAttribute("data-copied-label") : "");
                }, function () { /* sin permiso: no hay nada que avisar */ });
            });
        }

        if (printButton) {
            printButton.addEventListener("click", function () {
                /* Se imprime el documento y no la página del visor. Si el
                   navegador no lo permite, se abre en una pestaña para
                   imprimir desde allí. */
                try {
                    frame.contentWindow.focus();
                    frame.contentWindow.print();
                } catch (e) {
                    var open = query(root, "open");
                    window.open(open ? open.href : frame.src, "_blank", "noopener");
                }
            });
        }
    }

    window.RenderSetViewer = {
        init: init,

        /* La página pública cambia de documento con enlaces que apuntan al
           iframe: esto sólo pone al día título y enlaces de la barra. */
        setDocument: function (root, info) {
            if (!root || !info)
                return;

            var title = query(root, "title");
            var open = query(root, "open");
            var download = query(root, "download");
            var frame = query(root, "frame");

            if (title && info.title !== undefined)
                title.textContent = info.title;

            if (frame && info.title !== undefined)
                frame.title = info.title;

            if (open && info.viewUrl)
                open.href = info.viewUrl;

            if (download && info.downloadUrl)
                download.href = info.downloadUrl;
        }
    };

    function initAll() {
        var roots = document.querySelectorAll("[data-rs-viewer]");
        for (var i = 0; i < roots.length; i++)
            init(roots[i]);
    }

    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", initAll);
    else
        initAll();

})();
