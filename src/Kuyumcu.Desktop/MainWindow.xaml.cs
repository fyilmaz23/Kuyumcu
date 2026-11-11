using System.Windows;
using Kuyumcu.Platform;

namespace Kuyumcu.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Resources.Add("services", App.Services);
        InitializeComponent();
        Title = ProductInfo.Name;
    }
}
