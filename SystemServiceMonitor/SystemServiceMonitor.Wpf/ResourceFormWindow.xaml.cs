using System;
using System.Windows;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Wpf;

public partial class ResourceFormWindow : Window
{
    public Resource Resource => FormControl.Resource;

    public ResourceFormWindow(Resource? existingResource = null)
    {
        InitializeComponent();

        FormControl.SetResource(existingResource);
        FormControl.OnSave += FormControl_OnSave;
        FormControl.OnCancel += FormControl_OnCancel;
    }

    private void FormControl_OnSave(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void FormControl_OnCancel(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
