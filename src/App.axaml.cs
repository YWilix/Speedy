using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.IO;

namespace Speedy;

public partial class App : Application
{
    public override void Initialize()
    {
        Directory.CreateDirectory(Environment.CurrentDirectory + @"\SpeedyData\");
        AvaloniaXamlLoader.Load(this);
    }

    
    
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

}