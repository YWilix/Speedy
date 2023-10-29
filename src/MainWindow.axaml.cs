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
using System;
using Speedy.Scripts.Data;
using Avalonia.Media.Imaging;
using Avalonia.Media;

//using Rs = Speedy.Properties;//The Resources Namespace
using SysDraw = System.Drawing;
using SkiaSharp;
using Avalonia.Platform;
using Avalonia;
using Avalonia.OpenGL;

namespace Speedy;

public partial class MainWindow : Window
{
    private BaseWindowDataContext _DataContext;
    private bool? IsAnswerFromMessageYes = null;

    private string workingfile = Environment.CurrentDirectory + @"\SpeedyData\LastCopyingData.scd";
    private CopyingData WorkingData;//the data we are moving

    public bool IsLightTheme = true;

    /// <summary>
    /// Used to cancel files copying
    /// </summary>
    private CancellationTokenSource PauseTokenSource = new CancellationTokenSource();

    public MainWindow()
    {
        InitializeComponent();
        _DataContext = new BaseWindowDataContext();
        ThemeController.OnThemeChanged += _DataContext.ThemeChanged;
        _DataContext.PropertyChanged += PropertyChanged;//a function that handles some other changes when some property changes
        DataContext = _DataContext;
        _DataContext.MaxWidth = this.Width;
        var SavedTheme = SavingSys.LoadTheme();
        if (SavedTheme != null)
            ThemeController.MainTheme = (bool)SavedTheme ? Avalonia.Themes.Fluent.FluentThemeMode.Light : Avalonia.Themes.Fluent.FluentThemeMode.Dark;
        LoadLastCopyData();//loads the last paused copy data if exists
    }

    //Event Handlers :

    public void MoveWindow(object sender ,PointerPressedEventArgs args )
    {
        BeginMoveDrag(args);
    }

    public void ChangeTheme(object sender , RoutedEventArgs args)
    {
        ThemeController.MainTheme = _DataContext.IsLightTheme ? Avalonia.Themes.Fluent.FluentThemeMode.Dark : Avalonia.Themes.Fluent.FluentThemeMode.Light;
        SavingSys.SaveTheme();
    }

    public async void MoveDifference(object sender , RoutedEventArgs args)
    {
        if (MoveButton.Content == "Cancel") // Cancels the Files copying
        {
            SetPaused(true);

            //Asking if the user is sure to cancel
            var MsgDialogue = MessageDialogInCenter(MessageDialogueType.YesNo, "Are you sure ?",
            "Are you sure you want to cancel ?\nit's advisable to save the copying data before cancelling so you can continue again later");

            await MsgDialogue.ShowDialog(this);

            if (IsAnswerFromMessageYes == false)// Not cancelling
                return;

            //yes clicked :
            if (File.Exists(SavingSys.defaultlastworkingfilepath))//deleting the "last working file" file 
                File.Delete(SavingSys.defaultlastworkingfilepath);

            DeleteDefaultDataFile();//Removing the old copy data file
            ResetUi();//Resetting the UI

            return;
        }
        try //Copying Files
        {
            DeleteDefaultDataFile();//Removing the old copy data file

            SavingSys.ResetLastWorkingFile();//Resetting the "Last working file" file

            PauseTokenSource = new CancellationTokenSource();

            string Dest = destinationbox.Text;//Destination
            string Source = sourcebox.Text;

            if (!Directory.Exists(Source))
                throw new DirectoryNotFoundException("The source directory you indicated doesn't exist !");
            if (!Directory.Exists(Dest))
                throw new DirectoryNotFoundException("The destination directory you indicated doesn't exist !");

            //Resetting the main file 
            workingfile = SavingSys.defaultdatapath;

            //Asking if the user is sure to copy the files
            var MsgDialogue = MessageDialogInCenter(MessageDialogueType.YesNo, "Are you sure ?",
            "Are you sure you want to move files ?\nOnce started you can cancel but the files that are copied won't go back");

            await MsgDialogue.ShowDialog(this);

            if (IsAnswerFromMessageYes == false)// Not copying files if the user says no
                return;

            _DataContext.IsCopyingFiles = true;
            _DataContext.IsPaused = false;

            bool keepdeleted = (bool)KeepDeletedBox.IsChecked;
            bool keeplastver = (bool)LatestVerBox.IsChecked;

            var width = this.Width;

            Task.Run(() => SmartCopy.CopyDifference(Source, Dest, keepdeleted, keeplastver, PauseTokenSource.Token, CompletedCopying,PausedMoving,_DataContext, width),
                                   PauseTokenSource.Token);
        }
        catch (Exception e)
        {
            //an Error has occured
            var MsgDialogue = MessageDialogInCenter(MessageDialogueType.Ok, "ERROR !", e.Message);

            await MsgDialogue.ShowDialog(this);
        }
    }

    public async void QuitApplication(object sender, RoutedEventArgs args)
    {
        bool paused = _DataContext.IsPaused;
        SetPaused(true);//Saving the data to the working file
        string m1 = "Are you sure you want to quit ?\n";
        string Msg = _DataContext.IsCopyingFiles ? m1 + "don't worry you can continue the copying operation later" : m1;

        var MsgDialogue = MessageDialogInCenter(MessageDialogueType.YesNo, "Are you sure ?", Msg);

        await MsgDialogue.ShowDialog(this);

        if (IsAnswerFromMessageYes == false)// Not copying files if the user says no
        {
            SetPaused(paused);//Continue the copying
            return;
        }
        Environment.Exit(0);
    }

    public void PauseButtonClicked(object sender , RoutedEventArgs args)
    {
        SetPaused(!_DataContext.IsPaused);
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

    public async void LoadSaveButtonClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            if (_DataContext.IsCopyingFiles)//then we want to save
            {

                if (workingfile != SavingSys.defaultdatapath)//then the working file is a saved file and not the default one
                {
                    //Overwrites the save file with the new data
                    SetPaused(true);
                    Thread ShowThatSaved = new Thread(ShowSaved);
                    ShowThatSaved.Start();
                    return;
                }

                bool paused = _DataContext.IsPaused;

                SetPaused(true);//Pausing so the working data is Up to date

                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Title = "Save a Speedy Copying Data";
                
                var mainfilter = new FileDialogFilter();
                mainfilter.Name = "Speedy Copying Data(*.scd)";
                mainfilter.Extensions.Add("scd");
                saveFileDialog.Filters.Add(mainfilter);
                saveFileDialog.DefaultExtension = "scd";

                string path = await saveFileDialog.ShowAsync(this);

                if (path == null)
                {
                    SetPaused(paused);//setting pause to false to continue moving the file
                    return;
                }

                SavingSys.SaveCopyingData(WorkingData, path);//saving the data to the path
                DeleteDefaultDataFile();
                SavingSys.SaveLastWorkingFile(path);//Saves the last working file as the saved file path 
                workingfile = path;//Setting the working file to the saved data
            }
            else//then we want to load
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();

                //making a filter
                var mainfilter = new FileDialogFilter();
                mainfilter.Extensions.Add("scd");
                mainfilter.Name = "Speedy Copying Data (*.scd)";
                openFileDialog.Filters.Add(mainfilter);

                openFileDialog.Title = "Load a Speedy Copying Data"; 
                openFileDialog.AllowMultiple = false;


                string? path = (await openFileDialog.ShowAsync(this))?[0];

                if (path == null)
                    return;

                //loading the data
                var data = SavingSys.LoadCopyingData(path);
                await LoadCopyData(data);

                if (_DataContext.IsCopyingFiles)// if the system is copying files then LoadCopyData didn't encounter any errors
                {
                    DeleteDefaultDataFile();
                    SavingSys.SaveLastWorkingFile(path);//Saves the last working file as the loaded file path 
                    workingfile = path;
                }
                //setting the mainworkingfile so if paused again the copying operation save the data to the same file
            }
        }
        catch(Exception e){
            //An Error has occured
            var MsgDialogue = MessageDialogInCenter(MessageDialogueType.Ok, "ERROR !", e.Message);

            await MsgDialogue.ShowDialog(this);
        }
    }

    //Other methods :

    /// <summary>
    /// Sets the paused value of speedy 
    /// </summary>
    private void SetPaused(bool value)
    {
        if (!_DataContext.IsCopyingFiles)
            return;

        if (value)
            PauseTokenSource.Cancel();
        else
        {
            ContinueCopying(WorkingData);
        }
    }
    
    /// <summary>
    /// Continues the copying operation according to a Copy data
    /// </summary>
    private void ContinueCopying(CopyingData Data)
    {
        if (!Directory.Exists(Data.Source))
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", "The source directory of the current data doesn't exist anymore !");
            MsgDialouge.ShowDialog(this);
            return;
        }
        if (!Directory.Exists(Data.Destination))
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", "The destination directory of the current data doesn't exist anymore !");
            MsgDialouge.ShowDialog(this);
            return;
        }


        _DataContext.IsCopyingFiles = true;
        _DataContext.IsPaused = false;

        string source = Data.Source;
        string dest = Data.Destination;
        bool keepthenewest = Data.KeepTheNewest;
        var width = this.Width;

        PauseTokenSource = new CancellationTokenSource();

        //Continue the copying operation where it stopped
        if (Data.PausedOnDelete)
            Task.Run(() => SmartCopy.CopyDifference(source,dest, !Data.PausedOnDelete, keepthenewest, PauseTokenSource.Token, CompletedCopying,PausedMoving, _DataContext, width),
                       PauseTokenSource.Token);
        else
            Task.Run(() => SmartCopy.CopyDifference(source, dest,true, keepthenewest, PauseTokenSource.Token, CompletedCopying, PausedMoving, _DataContext, width,Data),
            PauseTokenSource.Token);
    }

    /// <summary>
    /// Loads the last copy data speedy was working on (the method also setup the Ui)
    /// </summary>
    private async Task LoadLastCopyData()
    {
        if (!File.Exists(SavingSys.defaultlastworkingfilepath))
            return;

        string path = SavingSys.LoadLastWorkingFile();

        if (!File.Exists(path))//Can't load the last copy data because it doesn't exist
            return;

        CopyingData data = SavingSys.LoadCopyingData(path);

        await LoadCopyData(data, false);//loads the last copy data


        if (_DataContext.IsCopyingFiles)// if the system is copying files then LoadCopyData didn't encounter any errors
            workingfile = path;
    }

    /// <summary>
    /// Loads a specific copy data (the method also setup the Ui)
    /// </summary>
    private async Task LoadCopyData(CopyingData data , bool LogErrors = true)
    {
        //Existence Errors :
        if (LogErrors && !Directory.Exists(data.Source))
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", "The source directory of the loaded data doesn't exist anymore !");
            await MsgDialouge.ShowDialog(this);
            return;
        }
        if (LogErrors && !Directory.Exists(data.Destination))
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", "The destination directory of the loaded data doesn't exist anymore !");
            await MsgDialouge.ShowDialog(this);
            return;
        }
        if (!LogErrors && (!Directory.Exists(data.Destination) || !Directory.Exists(data.Source)))
            return;

        //Directory Modified Errors :
        string Last = LogErrors ? "" : "Last ";

        var sourcetime = Directory.GetLastWriteTime(data.Source);
        if(sourcetime != data.SourceLastWriteTime)
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", $"The source directory of the data has been modified , can't continue the {Last}copying !\nYou can restart the copying if you want");
            await MsgDialouge.ShowDialog(this);
            return;
        }

        var destinationtime = Directory.GetLastWriteTime(data.Destination);
        if (destinationtime != data.DestinationLastWriteTime)
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", $"The destination directory of the data has been modified , can't continue the {Last}copying !\nYou can restart the copying if you want");
            await MsgDialouge.ShowDialog(this);
            return;
        }

        SetUi(data);

        _DataContext.IsCopyingFiles = true;
        _DataContext.IsPaused = true;

        WorkingData = data;//setting the data we are working on
    }

    //Ui related methods :

    /// <summary>
    /// does the setup of the ui according to a data object
    /// </summary>
    /// <param name="Data">the data</param>
    private void SetUi(CopyingData Data)
    {
        if (Data == null)
            throw new Exception("SETUP DATA IS NULL");

        sourcebox.Text = Data.Source;
        destinationbox.Text = Data.Destination;

        KeepDeletedBox.IsChecked = Data.KeepDeleted;
        LatestVerBox.IsChecked = Data.KeepTheNewest;

        _DataContext.ProgressWidth = Data.LastProgressBarWidth;
    }
    /// <summary>
    /// Resets the progress bar and the entire Ui to be usable again
    /// </summary>
    private void ResetUi()
    {
        _DataContext.IsCopyingFiles = false;
        _DataContext.IsPaused = true;
        _DataContext.ProgressWidth = 0;
    }

    /// <summary>
    /// Shows the saved indicator text if it isn't already shown
    /// </summary>
    private void ShowSaved()
    {
        if (_DataContext.Saved)
            return;

        //Sets Saved to true for some time to show that the copying data has saved
        _DataContext.Saved = true;
        Thread.Sleep(1200);
        _DataContext.Saved = false;
    }

    /// <summary>
    /// Creates a useful message dialogue instance
    /// </summary>
    private MessageDialog MessageDialogInCenter(MessageDialogueType DialogType, string title = "", string text = "")
    {
        var MsgDialogue = new MessageDialog(DialogType, title,text);
        MsgDialogue.NoEvent += () => { IsAnswerFromMessageYes = false; };
        MsgDialogue.YesEvent += () => { IsAnswerFromMessageYes = true; };

        return MsgDialogue;
    }


    /// <summary>
    /// Deletes the default copy data file
    /// </summary>
    private void DeleteDefaultDataFile()
    {
        if (File.Exists(SavingSys.defaultdatapath))
            File.Delete(SavingSys.defaultdatapath);
    }
    /// <summary>
    /// Deletes the working copy data file
    /// </summary>
    private void DeleteWorkingDataFile()
    {
        if (File.Exists(workingfile))
            File.Delete(workingfile);
    }

    /// <summary>
    /// a method that updates the ui when a property changes
    /// </summary>
    private void PropertyChanged(object sender , PropertyChangedEventArgs args)
    {
        string loadtext = "Load a saved copying file to continue copying";
        string savetext = "Save a copying file of the current copying operation to continue later ";
        
        if (args.PropertyName == nameof(_DataContext.IsCopyingFiles))
            LoadButtonTipText.Text = _DataContext.IsCopyingFiles ? savetext : loadtext;
    }


    /// <summary>
    /// Called when the copying operation is completed
    /// </summary>
    private void CompletedCopying()
    {
        ResetUi();
        DeleteWorkingDataFile();//deletes the working file so it doesn't make confusion for the user
        //(he can reload it again thiking he didn't complete the copying process)
    }
    /// <summary>
    /// Called when the copying operation is paused
    /// </summary>
    private void PausedMoving(CopyingData Data)
    {
        //Saves the copying data
        SavingSys.SaveCopyingData(Data, workingfile);
        WorkingData = Data;
        _DataContext.IsPaused = true;
    }
}

public class BaseWindowDataContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLightTheme => ThemeController.MainTheme == Avalonia.Themes.Fluent.FluentThemeMode.Light;

    public bool IsPaused
    {
        get
        {
            return _IsPaused;
        }
        set 
        {
            _IsPaused = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
        }
    }

    public bool IsCopyingFiles
    {
        get
        {
            return _IsCopyingFiles;
        }
        set
        {
            _IsCopyingFiles = value;
            PropertyChanged?.Invoke(this , new PropertyChangedEventArgs(nameof(IsCopyingFiles)));
        }
    }

    private bool _IsCopyingFiles = false;

    private bool _IsPaused = true;

    public double ProgressWidth
    {
        get
        {
            return _ProgressWidth;
        }
        set
        {
            _ProgressWidth = value;
            Percentage = (int)Math.Floor((_ProgressWidth / MaxWidth)*100);
            PropertyChanged?.Invoke(this , new PropertyChangedEventArgs(nameof(ProgressWidth)));
        }
    }

    private double _ProgressWidth;

    public string PercentageText => Percentage.ToString() + "%";

    private int Percentage
    {
        get { return _Percentage; }
        set
        {
            _Percentage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PercentageText)));
        }
    }

    private int _Percentage = 0;

    public bool Saved
    {
        get { return _Saved; }
        set
        {
            _Saved = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Saved)));
        }
    }

    private bool _Saved = false;

    public double MaxWidth;

    public void ThemeChanged()
    {
        PropertyChanged?.Invoke(this , new PropertyChangedEventArgs(nameof(IsLightTheme)));
    }
}
