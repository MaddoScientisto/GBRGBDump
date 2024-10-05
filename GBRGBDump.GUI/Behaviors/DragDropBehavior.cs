using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GBRGBDump.GUI.Behaviors;

public static class DragDropBehavior
{
    public static readonly DependencyProperty DropCommandProperty =
        DependencyProperty.RegisterAttached("DropCommand", typeof(ICommand), typeof(DragDropBehavior), new PropertyMetadata(null, OnDropCommandChanged));

    public static ICommand GetDropCommand(DependencyObject obj)
    {
        return (ICommand)obj.GetValue(DropCommandProperty);
    }

    public static void SetDropCommand(DependencyObject obj, ICommand value)
    {
        obj.SetValue(DropCommandProperty, value);
    }

    private static void OnDropCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement uiElement)
        {
            if (e.NewValue != null)
            {
                uiElement.Drop += OnElementDrop;
                uiElement.AllowDrop = true;
                uiElement.DragOver += OnElementDragOver;
            }
            else
            {
                uiElement.Drop -= OnElementDrop;
                uiElement.DragOver -= OnElementDragOver;
            }
        }
    }

    private static void OnElementDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private static void OnElementDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                var command = GetDropCommand(sender as DependencyObject);
                if (command != null && command.CanExecute(files[0]))
                {
                    command.Execute(files[0]);
                }
            }
        }
    }
}