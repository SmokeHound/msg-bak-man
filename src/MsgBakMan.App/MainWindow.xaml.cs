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
        var projectFolder = (DataContext as MainViewModel)?.ProjectFolder;

        var about = new AboutWindow(projectFolder)
        {
            Owner = this,
        };

        about.ShowDialog();
    }
}
