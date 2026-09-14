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