using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GBRGBDump.GUI.Controls;

public partial class LabelTextBox : UserControl
{
    public LabelTextBox()
    {
        InitializeComponent();
    }
    
    // Dependency Property for Label Text
    public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(
        nameof(LabelText), typeof(string), typeof(LabelTextBox), new PropertyMetadata(string.Empty));

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // Dependency Property for TextBox Content
    public static readonly DependencyProperty TextBoxContentProperty = DependencyProperty.Register(
        nameof(TextBoxContent), typeof(string), typeof(LabelTextBox), new PropertyMetadata(string.Empty));

    public string TextBoxContent
    {
        get => (string)GetValue(TextBoxContentProperty);
        set => SetValue(TextBoxContentProperty, value);
    }

    // Dependency Property for Button Visibility
    public static readonly DependencyProperty ButtonVisibilityProperty = DependencyProperty.Register(
        nameof(ButtonVisibility), typeof(Visibility), typeof(LabelTextBox), new PropertyMetadata(Visibility.Collapsed));

    public Visibility ButtonVisibility
    {
        get => (Visibility)GetValue(ButtonVisibilityProperty);
        set => SetValue(ButtonVisibilityProperty, value);
    }

    // Dependency Property for Button Command
    public static readonly DependencyProperty ButtonCommandProperty = DependencyProperty.Register(
        nameof(ButtonCommand), typeof(ICommand), typeof(LabelTextBox), new PropertyMetadata(null));

    public ICommand ButtonCommand
    {
        get => (ICommand)GetValue(ButtonCommandProperty);
        set => SetValue(ButtonCommandProperty, value);
    }
    
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(LabelTextBox), new PropertyMetadata(null));

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
}