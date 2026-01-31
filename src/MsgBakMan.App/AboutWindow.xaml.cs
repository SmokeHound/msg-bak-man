using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System;
using System.Windows;
using System.Windows.Input;

namespace MsgBakMan.App;

public partial class AboutWindow : Window
{
    private readonly string? _projectFolder;

    public ICommand? RepairPhoneNumbersCommand { get; }

    public AboutWindow(string? projectFolder, ICommand? repairPhoneNumbersCommand = null)
    {
        InitializeComponent();

        _projectFolder = string.IsNullOrWhiteSpace(projectFolder) ? null : projectFolder;
        RepairPhoneNumbersCommand = repairPhoneNumbersCommand is null
            ? null
            : new ConfirmingCommand(
                repairPhoneNumbersCommand,
                owner: this,
                title: "Repair phone numbers",
                message: "This will update phone numbers in the project database (converts legacy +10… to +0…).\n\nContinue?");

        VersionText.Text = $"Version {GetAppVersion()}";
        RuntimeText.Text = $"{RuntimeInformation.FrameworkDescription} • {RuntimeInformation.OSDescription}";

        if (_projectFolder is null)
        {
            ProjectSection.Visibility = Visibility.Collapsed;
            MaintenanceSection.Visibility = Visibility.Collapsed;
        }
        else
        {
            ProjectFolderText.Text = _projectFolder;

            OpenProjectFolderButton.IsEnabled = Directory.Exists(_projectFolder);
            var dbFolder = Path.Combine(_projectFolder, "db");

            OpenDbFolderButton.IsEnabled = Directory.Exists(dbFolder);
            OpenMediaFolderButton.IsEnabled = Directory.Exists(Path.Combine(_projectFolder, "media"));

            if (RepairPhoneNumbersCommand is null)
            {
                MaintenanceSection.Visibility = Visibility.Collapsed;
            }
            else
            {
                RepairPhoneNumbersButton.IsEnabled = Directory.Exists(dbFolder);
            }
        }
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_projectFolder is null) return;
        OpenFolder(_projectFolder);
    }

    private void OpenDbFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_projectFolder is null) return;
        OpenFolder(Path.Combine(_projectFolder, "db"));
    }

    private void OpenMediaFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_projectFolder is null) return;
        OpenFolder(Path.Combine(_projectFolder, "media"));
    }

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private sealed class ConfirmingCommand : ICommand
    {
        private readonly ICommand _inner;
        private readonly Window _owner;
        private readonly string _title;
        private readonly string _message;

        public ConfirmingCommand(ICommand inner, Window owner, string title, string message)
        {
            _inner = inner;
            _owner = owner;
            _title = title;
            _message = message;

            _inner.CanExecuteChanged += Inner_CanExecuteChanged;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _inner.CanExecute(parameter);

        public void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                _owner,
                _message,
                _title,
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.OK)
            {
                return;
            }

            _inner.Execute(parameter);
        }

        private void Inner_CanExecuteChanged(object? sender, EventArgs e)
        {
            CanExecuteChanged?.Invoke(this, e);
        }
    }
}
