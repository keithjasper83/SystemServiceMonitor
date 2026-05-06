using System;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Xunit;

namespace SystemServiceMonitor.Tests;

public class ResourceFormWindowAccessKeyTests
{
    private XElement GetWindowElement()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SystemServiceMonitor.Wpf", "ResourceFormWindow.xaml");
        string xamlContent = File.ReadAllText(xamlPath);
        return XElement.Parse(xamlContent);
    }

    [Fact]
    public void ResourceFormWindow_HasCancelAndSaveButtonsWithAccessKeys()
    {
        var window = GetWindowElement();
        XNamespace defaultNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var buttons = window.Descendants(defaultNs + "Button").ToList();

        var cancelBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnCancel_Click");
        Assert.NotNull(cancelBtn);
        // We ensure cancel has an access key or is configured correctly (e.g., IsCancel)
        Assert.True(cancelBtn.Attribute("IsCancel")?.Value == "True" || (cancelBtn.Attribute("Content")?.Value ?? "").Contains("_"), "Cancel button should have access key or IsCancel=True");

        var saveBtn = buttons.FirstOrDefault(b => b.Attribute("Click")?.Value == "BtnSave_Click");
        Assert.NotNull(saveBtn);
        // We ensure save has an access key or is configured correctly (e.g., IsDefault)
        Assert.True(saveBtn.Attribute("IsDefault")?.Value == "True" || (saveBtn.Attribute("Content")?.Value ?? "").Contains("_"), "Save button should have access key or IsDefault=True");
    }
}
