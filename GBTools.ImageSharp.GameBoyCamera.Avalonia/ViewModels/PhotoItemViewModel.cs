using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PhotoItemViewModel : ViewModelBase, IDisposable
{
    private readonly Action<PhotoItemViewModel> _openDetails;
    private readonly Func<Bitmap> _createPreviewBitmap;
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private bool isSelected;

    public PhotoItemViewModel(
        string title,
        string created,
        GbcPhoto photo,
        Bitmap thumbnailBitmap,
        Func<Bitmap> createPreviewBitmap,
        IReadOnlyList<MetadataEntry> metadataEntries,
        Action<PhotoItemViewModel> openDetails)
    {
        Title = title;
        Created = created;
        Photo = photo;
        ThumbnailBitmap = thumbnailBitmap;
        _createPreviewBitmap = createPreviewBitmap;
        MetadataEntries = metadataEntries;
        _openDetails = openDetails;
        OpenDetailsCommand = new RelayCommand(OpenDetails);
    }

    public string Title { get; }

    public string Created { get; }

    public string SafeFileStem => Title.Replace(' ', '-').ToLowerInvariant();

    public GbcPhoto Photo { get; }

    public Bitmap ThumbnailBitmap { get; }

    public Bitmap PreviewBitmap => _previewBitmap ??= _createPreviewBitmap();

    public IReadOnlyList<MetadataEntry> MetadataEntries { get; }

    public IRelayCommand OpenDetailsCommand { get; }

    public void EnsurePreviewBitmap()
    {
        _ = PreviewBitmap;
    }

    public void ReleasePreviewBitmap()
    {
        _previewBitmap?.Dispose();
        _previewBitmap = null;
    }

    public void Dispose()
    {
        ReleasePreviewBitmap();
        ThumbnailBitmap.Dispose();
    }

    private void OpenDetails()
    {
        _openDetails(this);
    }
}