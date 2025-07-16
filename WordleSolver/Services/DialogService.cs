using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace WordleSolver.Services;

public class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmDialog(string title, string message)
    {
        bool result = false;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var window = CreateWindow(title, message, true, r => result = r);
            await window.ShowDialog(GetMainWindow());
        });
        return result;
    }

    public async Task ShowMessageDialog(string title, string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var window = CreateWindow(title, message, false, _ => { });
            await window.ShowDialog(GetMainWindow());
        });
    }

    public async Task<IDisposable> ShowLoadingDialog(string message)
    {
        Window? window = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new Window
            {
                Title = "Loading",
                Width = 250,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new ProgressBar
                        {
                            IsIndeterminate = true,
                            Height = 10
                        }
                    }
                }
            };
            window.Show(GetMainWindow());
        });

        return new LoadingHandle(window ?? throw new InvalidOperationException());
    }

    private Window GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
               ?? throw new InvalidOperationException("Cannot find MainWindow");
    }

    private Window CreateWindow(string title, string message, bool withConfirm, Action<bool> callback)
    {
        var yesButton = new Button { Content = "Yes", Tag = true };
        var noButton = new Button { Content = "No", Tag = false };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (withConfirm)
        {
            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
        }
        else
        {
            var ok = new Button { Content = "OK", Tag = true };
            buttonPanel.Children.Add(ok);
        }

        var window = new Window
        {
            Title = title,
            Width = 300,
            Height = 150,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    buttonPanel
                }
            }
        };

        foreach (var control in buttonPanel.Children)
        {
            var btn = (Button)control;
            btn.Click += (_, _) =>
            {
                callback((bool)btn.Tag!);
                window.Close();
            };
        }

        return window;
    }

    private class LoadingHandle(Window window) : IDisposable
    {
        public void Dispose()
        {
            Dispatcher.UIThread.Post(window.Close);
        }
    }
}
