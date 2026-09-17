window.renderSet = window.renderSet || {};

window.renderSet.downloadText = function (fileName, content, contentType) {
    const blob = new Blob(
        [content],
        {
            type: contentType || "text/plain;charset=utf-8"
        });

    const url = URL.createObjectURL(blob);

    const anchor = document.createElement("a");

    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = "none";

    document.body.appendChild(anchor);

    anchor.click();
    anchor.remove();

    URL.revokeObjectURL(url);
};

window.renderSet.copyText = async function (content) {
    if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(content || "");
        return;
    }

    const textarea = document.createElement("textarea");
    textarea.value = content || "";
    textarea.style.position = "fixed";
    textarea.style.left = "-9999px";
    textarea.style.top = "-9999px";

    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    document.execCommand("copy");
    textarea.remove();
};

window.renderSet.initEditorResizer = function (root) {
    if (!root || root.dataset.resizerReady === "1") {
        return;
    }

    const handle = root.querySelector(".report-editor-resizer");

    if (!handle) {
        return;
    }

    root.dataset.resizerReady = "1";

    const storageKey = "renderset.editor.sidebarWidth";
    const defaultWidth = 600;
    const min = 260;
    const max = 900;

    const apply = function (width) {
        const clamped = Math.round(Math.min(max, Math.max(min, width)));
        root.style.setProperty("--report-editor-sidebar-width", clamped + "px");
        return clamped;
    };

    let current = defaultWidth;

    try {
        const stored = parseInt(window.localStorage.getItem(storageKey), 10);

        if (!isNaN(stored)) {
            current = apply(stored);
        }
    } catch (e) {
        // localStorage puede estar bloqueado; el ancho por defecto sirve.
    }

    const remember = function () {
        try {
            window.localStorage.setItem(storageKey, current);
        } catch (e) {
        }
    };

    let dragging = false;

    handle.addEventListener("pointerdown", function (e) {
        dragging = true;
        handle.setPointerCapture(e.pointerId);
        root.classList.add("is-resizing");
        e.preventDefault();
    });

    handle.addEventListener("pointermove", function (e) {
        if (!dragging) {
            return;
        }

        current = apply(e.clientX - root.getBoundingClientRect().left);
    });

    const stop = function (e) {
        if (!dragging) {
            return;
        }

        dragging = false;

        try {
            handle.releasePointerCapture(e.pointerId);
        } catch (error) {
        }

        root.classList.remove("is-resizing");
        remember();
    };

    handle.addEventListener("pointerup", stop);
    handle.addEventListener("pointercancel", stop);

    handle.addEventListener("dblclick", function () {
        current = apply(defaultWidth);
        remember();
    });
};

/* Preferencias de pantalla que no merecen viajar al servidor: son de este
   navegador y de este usuario, y perderlas no rompe nada. */

window.renderSet.getPreference = function (key, fallback) {
    try {
        const stored = window.localStorage.getItem(key);

        return stored === null ? (fallback || null) : stored;
    } catch (e) {
        return fallback || null;
    }
};

window.renderSet.setPreference = function (key, value) {
    try {
        window.localStorage.setItem(key, value);
    } catch (e) {
        // localStorage puede estar bloqueado; se pierde la preferencia y ya.
    }
};
