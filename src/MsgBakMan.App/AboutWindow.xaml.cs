using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MsgBakMan.App;

public partial class AboutWindow : Window
{
    private readonly string? _projectFolder;

    public AboutWindow(string? projectFolder)
    {
        InitializeComponent();

        _projectFolder = string.IsNullOrWhiteSpace(projectFolder) ? null : projectFolder;

        VersionText.Text = $"Version {GetAppVersion()}";
        RuntimeText.Text = $"{RuntimeInformation.FrameworkDescription} • {RuntimeInformation.OSDescription}";

        if (_projectFolder is null)
        {
            ProjectSection.Visibility = Visibility.Collapsed;
        }
        else
        {
            ProjectFolderText.Text = _projectFolder;

            OpenProjectFolderButton.IsEnabled = Directory.Exists(_projectFolder);
            OpenDbFolderButton.IsEnabled = Directory.Exists(Path.Combine(_projectFolder, "db"));
            OpenMediaFolderButton.IsEnabled = Directory.Exists(Path.Combine(_projectFolder, "media"));
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
}
