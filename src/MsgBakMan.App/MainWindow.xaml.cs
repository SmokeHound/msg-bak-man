using System.Windows;
using MsgBakMan.App.ViewModels;

namespace MsgBakMan.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        var projectFolder = vm?.ProjectFolder;

        var about = new AboutWindow(projectFolder, vm?.RepairPhoneNumbersCommand)
        {
            Owner = this,
        };

        about.ShowDialog();
    }

    private void ExportMediaButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        if (fe.ContextMenu is null)
        {
            return;
        }

        fe.ContextMenu.PlacementTarget = fe;
        fe.ContextMenu.IsOpen = true;
    }
}
