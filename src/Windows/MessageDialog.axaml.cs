using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace Speedy.Windows;

public enum MessageDialogueType 
{ 
    Ok , YesNo
}

public partial class MessageDialog : Window
{
    private BaseWindowDataContext _BaseWindowDataContext = new BaseWindowDataContext();

    /// <summary>
    /// The Event that's called when the user click yes
    /// </summary>
    public event Action YesEvent;

    /// <summary>
    /// The Event that's called when the user click No
    /// </summary>
    public event Action NoEvent;

    /// <summary>
    /// The Event that's called when the user click Ok
    /// </summary>
    public event Action OkEvent; 

    public MessageDialog()
    {
        InitializeComponent();
        DataContext = _BaseWindowDataContext;
    }

    public MessageDialog(MessageDialogueType DialogType , string title = "" , string text = "")
    {
        InitializeComponent();
        DataContext = _BaseWindowDataContext;
        TitleText.Text = title;
        MainText.Text = text;

        if(DialogType == MessageDialogueType.Ok)
        {
            FirstButton.IsVisible = false;
            SecondButton.Content = "Ok";
        }
        else
        {
            FirstButton.Content = "No";
            SecondButton.Content = "Yes";
        }
    }

    public void MoveWindow(object sender, PointerPressedEventArgs args)
    {
        BeginMoveDrag(args);
    }

    public void YesOkClicked(object sender, RoutedEventArgs args)
    {
        if (FirstButton.IsVisible)
            YesEvent?.Invoke();
        else
            OkEvent?.Invoke();
        this.Close();
    }

    public void NoClicked(object sender, RoutedEventArgs args)
    {
        NoEvent?.Invoke();
        this.Close();
    }
}