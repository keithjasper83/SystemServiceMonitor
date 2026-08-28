using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace SystemServiceMonitor.Tests
{
    public class MainWindowTests
    {
        [Fact]
        public void TextBox_ResourceFilter_DoesNotHaveKeyDownHandler()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "../../../../SystemServiceMonitor.Wpf/MainWindow.xaml");
            filePath = Path.GetFullPath(filePath);

            var doc = XDocument.Load(filePath);
            var ns = doc.Root!.GetDefaultNamespace();
            var xNs = (XNamespace)"http://schemas.microsoft.com/winfx/2006/xaml";

            var txt = doc.Descendants(ns + "TextBox").FirstOrDefault(t => t.Attribute(xNs + "Name")?.Value == "TxtResourceFilter");
            Assert.NotNull(txt);
            Assert.Null(txt.Attribute("KeyDown"));
        }

        [Fact]
        public void Button_Discover_IsDefault()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "../../../../SystemServiceMonitor.Wpf/MainWindow.xaml");
            filePath = Path.GetFullPath(filePath);

            var doc = XDocument.Load(filePath);
            var ns = doc.Root!.GetDefaultNamespace();
            var xNs = (XNamespace)"http://schemas.microsoft.com/winfx/2006/xaml";

            var btn = doc.Descendants(ns + "Button").FirstOrDefault(b => b.Attribute(xNs + "Name")?.Value == "BtnDiscover");
            Assert.NotNull(btn);
            Assert.Equal("True", btn.Attribute("IsDefault")?.Value);
        }
    }
}
