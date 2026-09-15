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
