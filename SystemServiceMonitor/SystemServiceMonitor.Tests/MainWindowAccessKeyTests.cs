using System;
using System.Reflection;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Xunit;

namespace SystemServiceMonitor.Tests;

public class MainWindowAccessKeyTests
{
    private XElement GetWindowElement()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SystemServiceMonitor.Wpf", "MainWindow.xaml");
        string xamlContent = File.ReadAllText(xamlPath);
        return XElement.Parse(xamlContent);
    }

    [Fact]
    public void MainWindow_HasExpectedAccessKeys()
    {
        var window = GetWindowElement();
        XNamespace defaultNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        var buttons = window.Descendants(defaultNs + "Button").ToList();

        // Discover button
        var discoverBtn = buttons.FirstOrDefault(b => b.Attribute(xNs + "Name")?.Value == "BtnDiscover");
        Assert.NotNull(discoverBtn);
        Assert.Contains("_", discoverBtn.Attribute("Content")?.Value ?? string.Empty);

        // Add Discovered
        var addDiscoveredBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnAddDiscovered_Click");
        Assert.NotNull(addDiscoveredBtn);
        Assert.Contains("_", addDiscoveredBtn.Attribute("Content")?.Value ?? string.Empty);

        // Add button
        var addBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnAddResource_Click");
        Assert.NotNull(addBtn);
        Assert.Contains("_", addBtn.Attribute("Content")?.Value ?? string.Empty);

        // Edit button
        var editBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnEditResource_Click");
        Assert.NotNull(editBtn);
        Assert.Contains("_", editBtn.Attribute("Content")?.Value ?? string.Empty);

        // Delete button
        var deleteBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnDeleteResource_Click");
        Assert.NotNull(deleteBtn);
        Assert.Contains("_", deleteBtn.Attribute("Content")?.Value ?? string.Empty);

        // AI Diagnosis button
        var diagnosisBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnAiDiagnosis_Click");
        Assert.NotNull(diagnosisBtn);
        Assert.Contains("_", diagnosisBtn.Attribute("Content")?.Value ?? string.Empty);

        // Execute Repair button
        var executeBtn = buttons.FirstOrDefault(b => b.Attribute(xNs + "Name")?.Value == "BtnExecuteRepair");
        Assert.NotNull(executeBtn);
        Assert.Contains("_", executeBtn.Attribute("Content")?.Value ?? string.Empty);

        // Refresh button
        var refreshBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnRefresh_Click");
        Assert.NotNull(refreshBtn);
        Assert.Contains("_", refreshBtn.Attribute("Content")?.Value ?? string.Empty);
    }

    [Fact]
    public void TxtResourceFilter_IsWiredToEnterKeyAndHandlesIt()
    {
        string csPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SystemServiceMonitor.Wpf", "MainWindow.xaml.cs");
        string csContent = File.ReadAllText(csPath);

        // Verify TxtResourceFilter_KeyDown handles Enter and calls BtnDiscover_Click
        Assert.Contains("e.Key == System.Windows.Input.Key.Enter", csContent);
        Assert.Contains("BtnDiscover_Click", csContent);

        // Memory explicitly says:
        // When handling the Enter key in a WPF `KeyDown` event for controls like `TextBox` or `ComboBox`,
        // set `e.Handled = true` after processing the logic to prevent the event from bubbling up
        // and potentially triggering the window's `IsDefault` button twice.
        Assert.Contains("e.Handled = true;", csContent);
    }
}
