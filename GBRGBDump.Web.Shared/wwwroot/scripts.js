function downloadBase64File(fileName, base64String)
{
    var link = document.createElement('a');
    link.href = base64String;
    link.download = fileName;
    link.click();
}

window.imageUtils = {
    scaleAndDownloadImage: (imgSrc, imgName, scaleFactor) => {
        const img = new Image();
        img.crossOrigin = "anonymous";  // Ensure CORS is handled if loading from external sources
        img.src = imgSrc;

        img.onload = () => {
            const canvas = document.createElement('canvas');
            canvas.width = img.width * scaleFactor;
            canvas.height = img.height * scaleFactor;

            const ctx = canvas.getContext('2d');
            ctx.imageSmoothingEnabled = false;  // Nearest neighbor scaling

            // Draw the scaled image
            ctx.drawImage(img, 0, 0, img.width * scaleFactor, img.height * scaleFactor);

            // Trigger download
            const link = document.createElement('a');
            link.href = canvas.toDataURL('image/png');
            link.download = `${imgName} x${scaleFactor}.png`;
            link.click();
        };

        img.onerror = () => {
            console.error("Failed to load image.");
        };
    }
};
