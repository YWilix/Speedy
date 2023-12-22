using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Speedy.Scripts;
using Speedy.Scripts.Data;
using Speedy.Scripts.Main;
using Speedy.Windows;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        _DataContext.CanSpecifyParamsCheck += () => Directory.Exists(sourcebox.Text); // a function that checks if the source exists as a directory
        DataContext = _DataContext;
        _DataContext.MaxWidth = this.Width;
        try
        {
            var SavedTheme = SavingSys.LoadTheme();
            ThemeController.MainTheme = (bool)SavedTheme ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
        }
        catch (Exception)
        { //Theme file not found
        }
        LoadLastCopyData();//loads the last paused copy data if exists
        var f = new OpenFileDialog();

    }

    //Event Handlers :

    public void MoveWindow(object sender, PointerPressedEventArgs args)
    {
        BeginMoveDrag(args);
    }

    public void ChangeTheme(object sender, RoutedEventArgs args)
    {
        ThemeController.MainTheme = _DataContext.IsLightTheme ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        SavingSys.SaveTheme();
    }

    public void SourceTextChanged(object sender, TextChangedEventArgs args)
    {
        _DataContext.CanSpecifyParamsChanged();
    }

    public async void MoveDifference(object sender, RoutedEventArgs args)
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
            if (sourcebox.Text == null || sourcebox.Text.Replace(" ","").Replace("\t","") == "")
            {
                var ErrorMessageDial = MessageDialogInCenter(MessageDialogueType.Ok, "ERROR !", "You must specify a source");
                await ErrorMessageDial.ShowDialog(this);
                return;
            }
            if (destinationbox.Text == null || destinationbox.Text.Replace(" ","").Replace("\t", "") == "")
            {
                var ErrorMessageDial = MessageDialogInCenter(MessageDialogueType.Ok, "ERROR !","You must specify a destination");
                await ErrorMessageDial.ShowDialog(this);
                return;
            }

            DeleteDefaultDataFile();//Removing the old copy data file

            SavingSys.ResetLastWorkingFile();//Resetting the "Last working file" file

            PauseTokenSource = new CancellationTokenSource();

            string Dest = destinationbox.Text;//Destination
            string Source = sourcebox.Text;

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

            Task.Run(() => SmartCopy.CopyDifference(Source, Dest, keepdeleted, keeplastver, PauseTokenSource.Token, CompletedCopying,AnErrorHappenedWhenCopying, PausedMoving, _DataContext, width),
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

    public void PauseButtonClicked(object sender, RoutedEventArgs args)
    {
        SetPaused(!_DataContext.IsPaused);
    }

    public async void ShowSourceFolderOptions(object sender, RoutedEventArgs args)
    {
        SourceSelectBt.Flyout.ShowAt(SourceSelectBt);
    }

    public async void SelectSourceAsAFolder(object sender, RoutedEventArgs args)
    {
        var FolderPickerOpts = new FolderPickerOpenOptions()
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(sourcebox.Text),
            Title = "Select The Source Folder"
        };

        var SelectedFolder = await StorageProvider.OpenFolderPickerAsync(FolderPickerOpts);
        sourcebox.Text = SelectedFolder.Count == 0 ? sourcebox.Text : SelectedFolder[0].Path.LocalPath;
    }

    public async void SelectSourceAsAFile(object sender, RoutedEventArgs args)
    {
        var OpenFileOpts = new FilePickerOpenOptions()
        {
            Title = "Select files to move",
            AllowMultiple = true
        };

        var result = await StorageProvider.OpenFilePickerAsync(OpenFileOpts);

        if (result.Count == 0)
            return;

        string Paths = result[0].Path.LocalPath; // a string containning all files paths Seperated by a semicolon ";"

        for (int i = 1; i < result.Count ; i++)//setting up the paths string
            Paths += ";" + result[i].Path.LocalPath;
        
        sourcebox.Text = Paths;
    }

    public async void SelectDestinationFolder(object sender, RoutedEventArgs args)
    {
        var FolderPickerOpts = new FolderPickerOpenOptions()
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(destinationbox.Text),
            Title = "Select The Source Folder"
        };

        var SelectedFolder = await StorageProvider.OpenFolderPickerAsync(FolderPickerOpts);
        destinationbox.Text = SelectedFolder.Count == 0 ? destinationbox.Text : SelectedFolder[0].Path.LocalPath;
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

                var SaveFileOpts = new FilePickerSaveOptions()
                {
                    DefaultExtension = "scd",
                    Title = "Save a Speedy copying data",
                    FileTypeChoices = new[] { new FilePickerFileType("Speedy Copying Data (*.scd)") { Patterns = new[] { "*.scd" } } }
                };

                string? path = (await StorageProvider.SaveFilePickerAsync(SaveFileOpts)).Path.LocalPath;

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

                var OpenFileOpts = new FilePickerOpenOptions()
                {
                    Title = "Open a Speedy copying data",
                    FileTypeFilter = new[] { new FilePickerFileType("Speedy Copying Data (*.scd)") { Patterns = new [] { "*.scd" } } }
                };

                var result = await StorageProvider.OpenFilePickerAsync(OpenFileOpts);

                string? path = result.Count == 0 ? null : result[0].Path.LocalPath;

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
        catch (JsonException e)
        {
            //unable to load the file
            var MsgDialogue = MessageDialogInCenter(MessageDialogueType.Ok, "ERROR !", "Unable to load that file");

            await MsgDialogue.ShowDialog(this);
        }
        catch (Exception e)
        {
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


        _DataContext.IsCopyingFiles = true;
        _DataContext.IsPaused = false;

        string source = Data.Source;
        string dest = Data.Destination;
        bool keepthenewest = Data.KeepTheNewest;
        var width = this.Width;

        PauseTokenSource = new CancellationTokenSource();

        //Continue the copying operation where it stopped
        if (Data.PausedOnDelete)
            Task.Run(() => SmartCopy.CopyDifference(source, dest, Data.PausedOnDelete, keepthenewest, PauseTokenSource.Token, CompletedCopying,AnErrorHappenedWhenCopying, PausedMoving, _DataContext, width),
                       PauseTokenSource.Token);
        else
            Task.Run(() => SmartCopy.CopyDifference(source, dest, false, keepthenewest, PauseTokenSource.Token, CompletedCopying,AnErrorHappenedWhenCopying, PausedMoving, _DataContext, width, Data),
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
    private async Task LoadCopyData(CopyingData data, bool LogErrors = true)
    {
        string ErrorMessage = "";

        //General Errors:
        if (data._DataProductVersion == null)
        {
            ErrorMessage = "The Speedy Copying Data you're trying to load is of an older speedy version and it's no longer supported (Speedy version 1.0.0 needed)";
            goto Error_Logging;
        }
        if (!Directory.Exists(data.Destination))
        {
            ErrorMessage = "The destination directory of the loaded data doesn't exist anymore !";
            goto Error_Logging;
        }


        //Errors of directory content copying:
        if (data.IsDirectoryCopying) // Then this copying data is of a directory content
        {
            //Existence Errors :
            if (!Directory.Exists(data.Source))
            {
                ErrorMessage = "The source directory of the loaded data doesn't exist anymore !";
                goto Error_Logging;
            }

            ////Directory Modified Errors :

            string Last = LogErrors ? "" : "Last ";

            var sourcetime = Directory.GetLastWriteTime(data.Source);
            if (sourcetime != data.SourceLastWriteTime)
            {
                ErrorMessage = $"The source directory of the data has been modified , can't continue the {Last}copying !\nYou can restart the copying if you want";
                goto Error_Logging;
            }

            var destinationtime = Directory.GetLastWriteTime(data.Destination);
            if (destinationtime != data.DestinationLastWriteTime)
            {
                ErrorMessage = $"The destination directory of the data has been modified , can't continue the {Last}copying !\nYou can restart the copying if you want";
                goto Error_Logging;
            }
        }

        //the actual function tasks :

        SetUi(data);

        _DataContext.IsCopyingFiles = true;
        _DataContext.IsPaused = true;

        WorkingData = data;//setting the data we are working on


    Error_Logging:// Logging Errors if needed

        if (LogErrors && ErrorMessage != "")
        {
            var MsgDialouge = MessageDialogInCenter(MessageDialogueType.Ok, "Error", ErrorMessage);
            await MsgDialouge.ShowDialog(this);
            return;
        }
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

        KeepDeletedBox.IsChecked = Data.RemoveAdditionalFiles;
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
        var MsgDialogue = new MessageDialog(DialogType, title, text);
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
    private void PropertyChanged(object sender, PropertyChangedEventArgs args)
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

    private async void AnErrorHappenedWhenCopying(object? msg)
    {
        var MsgDialogue = MessageDialogInCenter(MessageDialogueType.Ok, "ERROR !", (string)msg);

        await MsgDialogue.ShowDialog(this);
    }
}

public class BaseWindowDataContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public event Func<bool> CanSpecifyParamsCheck;

    /// <summary>
    /// Represents if the user can specify parameters for the copying operation
    /// </summary>
    public bool CanSpecifyParams => !IsCopyingFiles && CanSpecifyParamsCheck();

    public bool IsLightTheme => ThemeController.MainTheme == Avalonia.Styling.ThemeVariant.Light;

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
            CanSpecifyParamsChanged();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCopyingFiles)));
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
            Percentage = (int)Math.Floor((_ProgressWidth / MaxWidth) * 100);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressWidth)));
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLightTheme)));
    }

    public void CanSpecifyParamsChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSpecifyParams)));
    }
}
