using GBRGBDump.GUI.Commands;
using GBTools.Common.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GBRGBDump.GUI.ViewModels
{

    public class ScriptModel
    {
        public string Path { get; set; }
        public string RunLocation { get; set; }
        public string Arguments { get; set; }

        public bool Success { get; set; }

        public string Output { get; set; }
    }

    public class ScriptViewModel : ViewModelBase<ScriptModel>
    {
        private readonly IExecutionService _executionService;
        private string _input;

        public ObservableCollection<string> OutputLines { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ErrorLines { get; set; } = new ObservableCollection<string>();

        public string Input
        {
            get => _input;
            set
            {
                _input = value;
                OnPropertyChanged();
            }
        }

        public ICommand SendInputCommand { get; }

        public ScriptViewModel(IExecutionService executionService)
        {
            _executionService = executionService;

            // Subscribe to real-time output and error
            _executionService.OutputReceived += (sender, output) =>
            {
                Application.Current.Dispatcher.Invoke(() => OutputLines.Add(output)); // Update UI in real-time
            };

            _executionService.ErrorReceived += (sender, error) =>
            {
                Application.Current.Dispatcher.Invoke(() => ErrorLines.Add(error)); // Update UI in real-time
            };

            SendInputCommand = new RelayCommand(async () => await SendInputAsync());
        }

        public override async Task OnInitializedAsync()
        {
            await RunScriptAsync(Model.Path, Model.RunLocation, Model.Arguments);

            this.CloseDialog(Model.Success);
        }

        public async Task RunScriptAsync(string scriptPath, string workingDirectory, string arguments)
        {
            OutputLines.Clear();
            ErrorLines.Clear();

            var success = await _executionService.RunScriptAsync(scriptPath, workingDirectory, arguments);

            //if (!success)
            //{
            //    OutputLines.Add("Process failed.");
            //}

            Model.Success = success;
            Model.Output = OutputLines.Last();
        }

        public async Task SendInputAsync()
        {
            if (!string.IsNullOrEmpty(Input))
            {
                await _executionService.WriteToStandardInputAsync(Input);
                Input = string.Empty;  // Clear input after sending
            }
        }
    }
}
