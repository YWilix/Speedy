using Avalonia.Controls;
using System.ComponentModel;
using Speedy.Scripts;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.IO;
using Speedy.Scripts.Main;
using System.Threading.Tasks;
using System.Threading;
using Speedy.Windows;
using Avalonia;
using System;

namespace Speedy;

public partial class MainWindow : Window
{
    private BaseWindowDataContext _DataContext;
    private bool? IsAnswerFromMessageYes = null;  

    private Task MovingFiles = null;

    /// <summary>
    /// Used to cancel files copying
    /// </summary>
    private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();

    public MainWindow()
    {
        InitializeComponent();
        _DataContext = new BaseWindowDataContext();
        ThemeController.OnThemeChanged += _DataContext.ThemeChanged;
        DataContext = _DataContext;
    }

    public void MoveWindow(object sender ,PointerPressedEventArgs args )
    {
        BeginMoveDrag(args);
    }

    public void ChangeTheme(object sender , RoutedEventArgs args)
    {
        ThemeController.MainTheme = _DataContext.IsLightTheme ? Avalonia.Themes.Fluent.FluentThemeMode.Dark : Avalonia.Themes.Fluent.FluentThemeMode.Light;
    }

    public async void MoveDifference(object sender , RoutedEventArgs args)
    {
        if (MoveButton.Content == "Cancel") // Cancels the Files copying
        {
            CancelTokenSource.Cancel(true);
            MoveButton.Content = "Move";
            return;
        }
        try //Copying Files
        {
            CancelTokenSource = new CancellationTokenSource();

            string Dest = destinationbox.Text;//Destination
            string Source = sourcebox.Text;

            if (!Directory.Exists(Source))
                throw new DirectoryNotFoundException("The source directory you indicated doesn't exist !");
            if (!Directory.Exists(Dest))
                throw new DirectoryNotFoundException("The destination directory you indicated doesn't exist !");

            //Asking if the user is sure to copy the files
            var MsgDialogue = new MessageDialog(MessageDialogueType.YesNo , "Are you sure ?" ,
            "Are you sure you want to move files ?\nOnce started you can cancel but the files that are copied won't go back");
            MsgDialogue.NoEvent += () => { IsAnswerFromMessageYes = false; };
            MsgDialogue.YesEvent += () => { IsAnswerFromMessageYes = true; };

            await MsgDialogue.ShowDialog(this);

            if (IsAnswerFromMessageYes == false)// Not copying files if the user says no
                return;

            bool keepdeleted = (bool)KeepDeletedBox.IsChecked;
            bool keeplastver = (bool)LatestVerBox.IsChecked;

            MoveButton.Content = "Cancel";

            var width = this.Width;

            MovingFiles = Task.Run(() => SmartCopy.CopyDifference(Source, Dest, keepdeleted, keeplastver,CancelTokenSource.Token, CompletedMoving, _DataContext,width),
                                   CancelTokenSource.Token);
            // , Widthv
        }
        catch (System.Exception e)
        {
            //Asking if the user is sure to copy the files
            var MsgDialogue = new MessageDialog(MessageDialogueType.Ok, "ERROR !",e.Message);

            await MsgDialogue.ShowDialog(this);
        }
    }

    public void QuitApplication(object sender, RoutedEventArgs args)
    {
        Environment.Exit(0);
    }

    public void SelectSourceFolder(object sender, RoutedEventArgs args)
    {
        OpenFolderDialog fd = new OpenFolderDialog();
        fd.Directory = sourcebox.Text;
        var t = fd.ShowAsync(this); 
        var result = t.Result;
        sourcebox.Text = result == null ? sourcebox.Text : result;
    }

    public void SelectDestinationFolder(object sender, RoutedEventArgs args)
    {
        OpenFolderDialog fd = new OpenFolderDialog();
        fd.Directory = destinationbox.Text;
        var t = fd.ShowAsync(this);
        var result = t.Result;
        destinationbox.Text = result == null ? destinationbox.Text : result;
    }

    private void CompletedMoving()
    {
        MoveButton.Content = "Move";
        _DataContext.ProgressWidth = 0;
    }
}

public class BaseWindowDataContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLightTheme => ThemeController.MainTheme == Avalonia.Themes.Fluent.FluentThemeMode.Light;

    public double ProgressWidth
    {
        get
        {
            return _ProgressWidth;
        }
        set
        {
            _ProgressWidth = value;
            PropertyChanged?.Invoke(this , new PropertyChangedEventArgs(nameof(ProgressWidth)));
        }
    }

    private double _ProgressWidth;

    public void ThemeChanged()
    {
        PropertyChanged?.Invoke(this , new PropertyChangedEventArgs(nameof(IsLightTheme)));
    }
}
