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
        // Wrap maintenance commands with confirmation dialogs
        try
        {
            var vm = DataContext as MainViewModel;
            if (vm is not null)
            {
                RepairMaintenanceButton.Command = new ConfirmingCommand(vm.RepairPhoneNumbersCommand, this, "Repair phone numbers", "This will update phone numbers in the project database (converts legacy +10… to +0…) and rebuild conversation data. Continue?");
                RemoveSpacesButton.Command = new ConfirmingCommand(vm.RemoveSpacesFromNumbersCommand, this, "Remove spaces from numbers", "This will remove spaces from stored phone numbers (both raw and normalized) and rebuild conversation data where required. Continue?");
                AddPlus61Button.Command = new ConfirmingCommand(vm.AddPlus61NumbersCommand, this, "Add +61 / remove leading 0", "This will prefix '+61' and remove a single leading 0 from normalized addresses (may merge recipients). Continue?");
            }
        }
        catch
        {
            // best-effort wiring; ignore failures
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        var projectFolder = vm?.ProjectFolder;

        var about = new AboutWindow(projectFolder, vm?.RepairPhoneNumbersCommand, vm)
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
