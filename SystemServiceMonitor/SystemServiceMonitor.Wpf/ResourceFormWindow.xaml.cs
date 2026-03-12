using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Wpf;

public partial class ResourceFormWindow : Window
{
    public Resource Resource { get; private set; }

    public ResourceFormWindow(Resource? existingResource = null)
    {
        InitializeComponent();

        CboType.ItemsSource = Enum.GetValues(typeof(ResourceType));

        if (existingResource != null)
        {
            Resource = existingResource;
            LoadResource();
        }
        else
        {
            Resource = new Resource();
            CboType.SelectedIndex = 0;
        }
    }

    private void LoadResource()
    {
        TxtDisplayName.Text = Resource.DisplayName;
        CboType.SelectedItem = Resource.Type;
        TxtStartCommand.Text = Resource.StartCommand;
        TxtStopCommand.Text = Resource.StopCommand;
        TxtRestartCommand.Text = Resource.RestartCommand;
        TxtHealthcheck.Text = Resource.HealthcheckCommand;
        TxtWorkingDir.Text = Resource.WorkingDirectory;
        TxtWslDistro.Text = Resource.WslDistroName;
        TxtDockerId.Text = Resource.DockerIdentifier;
        TxtDependencies.Text = Resource.DependencyIds;
        TxtGitHubRepo.Text = Resource.GitHubRepoUrl;
        ChkAutoRepair.IsChecked = Resource.AutoRepairEnabled;
        ChkRequiresElevation.IsChecked = Resource.RequiresElevation;
    }

    private void SaveResource()
    {
        Resource.DisplayName = TxtDisplayName.Text;
        Resource.Type = (ResourceType)CboType.SelectedItem;
        Resource.StartCommand = TxtStartCommand.Text;
        Resource.StopCommand = TxtStopCommand.Text;
        Resource.RestartCommand = TxtRestartCommand.Text;
        Resource.HealthcheckCommand = TxtHealthcheck.Text;
        Resource.WorkingDirectory = TxtWorkingDir.Text;
        Resource.WslDistroName = TxtWslDistro.Text;
        Resource.DockerIdentifier = TxtDockerId.Text;
        Resource.DependencyIds = TxtDependencies.Text;
        Resource.GitHubRepoUrl = TxtGitHubRepo.Text;
        Resource.AutoRepairEnabled = ChkAutoRepair.IsChecked ?? false;
        Resource.RequiresElevation = ChkRequiresElevation.IsChecked ?? false;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtDisplayName.Text))
        {
            MessageBox.Show("Display Name is required.");
            return;
        }

        SaveResource();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CboType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Simple UX changes based on type can go here
    }
}
