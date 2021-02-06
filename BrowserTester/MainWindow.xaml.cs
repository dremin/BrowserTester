#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
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
        private readonly FileOperationWorker _fileWorker;
        private ShellFolder? folder;
        private IntPtr handle;

        // Controls whether shell item enumeration occurs in the background. Slower but keeps UI responsive while loading large folders.
        private bool enumAsync = false;

        public MainWindow()
        {
            InitializeComponent();

            _fileWorker = new FileOperationWorker();
            Navigate(string.Empty);
        }

        private void MemoryTest()
        {
            for (int i = 0; i < 10000; i++)
            {
                ShellFolder folder = GetFolder("C:\\Windows");
                int p = folder.Files.Count;
                folder.Dispose();
            }
        }

        private ShellFolder GetFolder(string path)
        {
            return new ShellFolder(path, handle, enumAsync);
        }

        private ICollectionView GetCollectionView()
        {
            if (folder == null)
            {
                return CollectionViewSource.GetDefaultView(new List<ShellItem>());
            }
            
            ICollectionView cvs = CollectionViewSource.GetDefaultView(folder.Files);
            if (folder.IsFileSystem) cvs.SortDescriptions.Add(new SortDescription("IsFolder", ListSortDirection.Descending));
            cvs.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));

            if (cvs is ICollectionViewLiveShaping cvls)
            {
                cvls.IsLiveSorting = true;
                if (folder.IsFileSystem) cvls.LiveSortingProperties.Add("IsFolder");
                cvls.LiveSortingProperties.Add("DisplayName");
            }

            return cvs;
        }
        
        private void UpdateBrowser()
        {
            Title = folder?.DisplayName;
            Icon = folder?.LargeIcon;
            location.Text = folder?.Path;
            IconsControl.ItemsSource = GetCollectionView();
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
                // The folder icon was fetched asynchronously and is now ready
                Dispatcher.BeginInvoke(() => { Icon = folder?.LargeIcon; });
            }
        }

        private void Window1_OnSourceInitialized(object? sender, EventArgs e)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            handle = helper.Handle;
        }

        #region Context menu command handlers

        private void executeAction(string action, ShellItem[] items)
        {
            foreach (var item in items)
            {
                ShellLogger.Info($"Command: {action} Path: {item.Path}");

                if (action == "openFolder")
                {
                    Navigate(item.Path);
                }
                else if (action == ((uint) CommonContextMenuItem.Properties).ToString())
                {
                    ShellHelper.ShowFileProperties(item.Path);
                }
            }
        }

        private void folderAction(uint action, string path)
        {
            ShellLogger.Info($"Command: {action} Path: {path}");

            if (action == (uint) CommonContextMenuItem.Properties)
            {
                ShellHelper.ShowFileProperties(path);
            }
            else if (action == (uint)CommonContextMenuItem.Paste)
            {
                _fileWorker.PasteFromClipboard(path);
            }
        }

        #endregion

        #region Context menu builders

        private ShellMenuCommandBuilder GetFolderCommandBuilder()
        {
            if (folder == null)
            {
                return new ShellMenuCommandBuilder();
            }

            ShellMenuCommandBuilder builder = new ShellMenuCommandBuilder();
            MFT flags = MFT.BYCOMMAND;

            if (!folder.IsFileSystem)
            {
                flags |= MFT.DISABLED;
            }

            builder.AddCommand(new ShellMenuCommand
            {
                Flags = flags,
                Label = "Paste",
                UID = (uint) CommonContextMenuItem.Paste
            });
            builder.AddSeparator();

            if (folder.IsFileSystem && folder.IsFolder)
            {
                builder.AddShellNewMenu();
                builder.AddSeparator();
            }

            builder.AddCommand(new ShellMenuCommand
            {
                Flags = flags,
                Label = "Properties",
                UID = (uint) CommonContextMenuItem.Properties
            });

            return builder;
        }

        private ShellMenuCommandBuilder GetFileCommandBuilderTop(ShellFile file)
        {
            ShellMenuCommandBuilder builder = new ShellMenuCommandBuilder();

            builder.AddCommand(new ShellMenuCommand
            {
                Flags = MFT.BYCOMMAND,
                Label = "Test Top",
                UID = (uint) CommonContextMenuItem.Properties
            });

            if (file.IsFolder)
            {
                builder.AddCommand(new ShellMenuCommand
                {
                    Flags = MFT.BYCOMMAND,
                    Label = "This is a folder!",
                    UID = (uint) CommonContextMenuItem.Properties
                });
            }

            builder.AddSeparator();

            return builder;
        }

        private ShellMenuCommandBuilder GetFileCommandBuilderBottom()
        {
            ShellMenuCommandBuilder builder = new ShellMenuCommandBuilder();

            builder.AddSeparator();
            builder.AddCommand(new ShellMenuCommand
            {
                Flags = MFT.BYCOMMAND,
                Label = "Test Bottom",
                UID = (uint) CommonContextMenuItem.Properties
            });

            return builder;
        }

        #endregion

        #region UI actions

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

            ShellFolderContextMenu menu = new ShellFolderContextMenu(folder, folderAction, GetFolderCommandBuilder());

            e.Handled = true;
        }

        private void UIElement_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShellFile? item = (sender as FrameworkElement)?.DataContext as ShellFile;

            if (item is ShellFile file)
            {
                ShellItemContextMenu menu = new ShellItemContextMenu(new ShellItem[] {file}, handle, executeAction, true,
                    GetFileCommandBuilderTop(file), GetFileCommandBuilderBottom());
            }

            e.Handled = true;
        }

        private void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShellFile? item = (sender as FrameworkElement)?.DataContext as ShellFile;

            if (item is ShellFile file)
            {
                ShellItemContextMenu menu = new ShellItemContextMenu(new ShellItem[] {file}, handle, executeAction, false);
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
            Navigate("shell:::{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");
        }

        private void ComputerButton_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate(string.Empty);
        }

        private void ControlPanelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate("::{21EC2020-3AEA-1069-A2DD-08002B30309D}");
        }

        private void AppsButton_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate("shell:AppsFolder");
        }

        #endregion

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            folder?.Dispose();
        }
    }
}