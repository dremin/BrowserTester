#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ManagedShell.Common.Helpers;
using ManagedShell.Common.Logging;
using ManagedShell.ShellFolders;
using ManagedShell.ShellFolders.Enums;

namespace BrowserTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ShellFolder? folder;
        private IntPtr handle;
        
        public MainWindow()
        {
            InitializeComponent();
            
            Navigate(string.Empty);
        }

        private ShellFolder GetFolder(string path)
        {
            return new ShellFolder(path, handle, true);
        }
        
        private void UpdateBrowser()
        {
            Title = folder?.DisplayName;
            Icon = folder?.LargeIcon;
            location.Text = folder?.Path;
            IconsControl.ItemsSource = folder?.Files;
        }

        private void Navigate(string path)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (folder != null)
                {
                    folder.PropertyChanged -= FolderOnPropertyChanged;
                    folder.Dispose();
                }

                folder = GetFolder(path);
                folder.PropertyChanged += FolderOnPropertyChanged;

                UpdateBrowser();
            });
        }

        private void FolderOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LargeIcon")
            {
                Dispatcher.BeginInvoke(() =>
                {
                    Icon = folder?.LargeIcon;
                });
            }
        }

        private void Window1_OnSourceInitialized(object? sender, EventArgs e)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            handle = helper.Handle;
        }
        
        private void executeAction(string action, ShellItem[] items)
        {
            foreach (var item in items)
            {
                ShellLogger.Info($"Command: {action} Path: {item.Path}");

                if (action == "openFolder")
                {
                    Navigate(item.Path);
                }
                else if (action == ((uint)CommonContextMenuItem.Properties).ToString())
                {
                    ShellHelper.ShowFileProperties(item.Path);
                }
            }
        }
        
        private void folderAction(uint action, string path)
        {
            ShellLogger.Info($"Command: {action} Path: {path}");

            if (action == (uint)CommonContextMenuItem.Properties)
            {
                ShellHelper.ShowFileProperties(path);
            }
        }

        private void Go_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate(location.Text);
        }

        private void Window1_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (folder == null)
            {
                return;
            }

            ShellCommandBuilder builder = new ShellCommandBuilder();
            
            builder.AddCommand(new ShellCommand {Flags = MFT.BYCOMMAND, Label = "Paste", UID = (uint)CommonContextMenuItem.Paste});
            builder.AddSeparator();
            builder.AddShellNewMenu();
            builder.AddSeparator();
            builder.AddCommand(new ShellCommand { Flags = MFT.BYCOMMAND, Label = "Properties", UID = (uint)CommonContextMenuItem.Properties });

            ShellContextMenu menu = new ShellContextMenu(folder, folderAction, builder);

            e.Handled = true;
        }

        private void UIElement_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShellFile? item = (sender as FrameworkElement)?.DataContext as ShellFile;

            if (item is ShellFile file)
            {
                ShellCommandBuilder preBuilder = new ShellCommandBuilder();

                preBuilder.AddCommand(new ShellCommand { Flags = MFT.BYCOMMAND, Label = "Test Top", UID = (uint)CommonContextMenuItem.Properties });
                preBuilder.AddSeparator();

                ShellCommandBuilder postBuilder = new ShellCommandBuilder();

                postBuilder.AddSeparator();
                postBuilder.AddCommand(new ShellCommand { Flags = MFT.BYCOMMAND, Label = "Test Bottom", UID = (uint)CommonContextMenuItem.Properties });

                ShellContextMenu menu = new ShellContextMenu(new ShellItem[] { file }, handle, executeAction, true, preBuilder, postBuilder);
            }

            e.Handled = true;
        }

        private void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShellFile? item = (sender as FrameworkElement)?.DataContext as ShellFile;

            if (item is ShellFile file)
            {
                ShellContextMenu menu = new ShellContextMenu(new ShellItem[] { file }, handle, executeAction, false);
            }

            e.Handled = true;
        }

        private void UpButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (folder is ShellFolder shellFolder)
            {
                Navigate(shellFolder.ParentItem.Path);
                shellFolder.ParentItem.Dispose();
            }
        }

        private void DesktopButton_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate("::{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");
        }

        private void ControlPanelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate("::{21EC2020-3AEA-1069-A2DD-08002B30309D}");
        }

        private void ComputerButton_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate(string.Empty);
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            folder?.Dispose();
        }
    }
}
