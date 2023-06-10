using Avalonia.Controls;
using System.ComponentModel;
using Speedy.Scripts;
using Avalonia.Input;

namespace Speedy;

public partial class MainWindow : Window
{
    private MainWindowDataContext _DataContext;

    public MainWindow()
    {
        InitializeComponent();
        _DataContext = new MainWindowDataContext();
        ThemeController.OnThemeChanged += _DataContext.ThemeChanged;
        DataContext = _DataContext;
    }

    public void MoveWindow(object sender ,PointerPressedEventArgs args )
    {
        BeginMoveDrag(args);
    }
}

public class MainWindowDataContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLightTheme => ThemeController.MainTheme == Avalonia.Themes.Fluent.FluentThemeMode.Light;

    public void ThemeChanged()
    {
        PropertyChanged?.Invoke(this , new PropertyChangedEventArgs(nameof(IsLightTheme)));
    }
}
