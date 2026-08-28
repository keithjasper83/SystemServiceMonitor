using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace SystemServiceMonitor.Tests;

public class MainWindowTests
{
    private string GetXamlPath()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var srcPath = Path.GetFullPath(Path.Combine(basePath, "../../../../../SystemServiceMonitor.Wpf/MainWindow.xaml"));
        if (File.Exists(srcPath)) return srcPath;

        srcPath = Path.GetFullPath(Path.Combine(basePath, "../../../../SystemServiceMonitor.Wpf/MainWindow.xaml"));
        if (File.Exists(srcPath)) return srcPath;

        throw new FileNotFoundException("Could not find MainWindow.xaml from " + basePath);
    }

    [Fact]
    public void MainWindow_HasAccessKeys_ForImportantButtons()
    {
        string path = GetXamlPath();

        var doc = XDocument.Load(path);
        var ns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        var buttons = doc.Descendants(ns + "Button").ToList();

        Assert.Contains(buttons, b => b.Attribute("Content")?.Value == "_Discover");
        Assert.Contains(buttons, b => b.Attribute("Content")?.Value == "_Add Resource");
        Assert.Contains(buttons, b => b.Attribute("Content")?.Value == "_Edit Selected");
        Assert.Contains(buttons, b => b.Attribute("Content")?.Value == "_Delete Selected");
        Assert.Contains(buttons, b => b.Attribute("Content")?.Value == "AI D_iagnosis");
        Assert.Contains(buttons, b => b.Attribute("Content")?.Value == "E_xecute Repair");
    }

    [Fact]
    public void MainWindow_HasEnterKeyDown_ForFilterTextBox()
    {
        string path = GetXamlPath();

        var doc = XDocument.Load(path);
        var ns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var xNs = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var textboxes = doc.Descendants(ns + "TextBox").ToList();

        var filterTb = textboxes.FirstOrDefault(tb => tb.Attribute(xNs + "Name")?.Value == "TxtResourceFilter");

        Assert.NotNull(filterTb);
        Assert.Equal("TxtResourceFilter_KeyDown", filterTb.Attribute("KeyDown")?.Value);
    }
}
