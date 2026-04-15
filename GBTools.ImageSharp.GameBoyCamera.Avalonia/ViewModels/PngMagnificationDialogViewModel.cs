using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PngMagnificationDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string magnificationText = "4";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public PngMagnificationDialogViewModel()
    {
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event Action<int?>? CloseRequested;

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    private void Confirm()
    {
        if (!int.TryParse(MagnificationText, out int magnification) || magnification < 1 || magnification > 64)
        {
            ErrorMessage = "Enter a whole-number magnification between 1 and 64.";
            return;
        }

        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(magnification);
    }

    private void Cancel() => CloseRequested?.Invoke(null);
}