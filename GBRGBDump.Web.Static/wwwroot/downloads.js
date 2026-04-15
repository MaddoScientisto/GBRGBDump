window.gbrgbdumpDownloads = {
    saveBase64File: function (fileName, contentType, base64Payload) {
        const byteCharacters = atob(base64Payload);
        const byteNumbers = new Array(byteCharacters.length);

        for (let index = 0; index < byteCharacters.length; index++) {
            byteNumbers[index] = byteCharacters.charCodeAt(index);
        }

        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: contentType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    }
};

window.gbrgbdumpDiagnostics = {
    logError: function (message) {
        console.error(message);
    }
};