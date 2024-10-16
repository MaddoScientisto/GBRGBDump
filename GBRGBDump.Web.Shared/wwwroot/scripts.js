function downloadBase64File(fileName, base64String)
{
    var link = document.createElement('a');
    link.href = base64String;
    link.download = fileName;
    link.click();
}