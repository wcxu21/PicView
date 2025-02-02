﻿using PicView.FileHandling;
using PicView.ImageHandling;
using PicView.PicGallery;
using PicView.Properties;
using PicView.SystemIntegration;
using PicView.UILogic;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using XamlAnimatedGif;
using static PicView.ChangeImage.LoadPic;
using static PicView.ChangeImage.Navigation;
using static PicView.ChangeTitlebar.SetTitle;
using static PicView.FileHandling.ArchiveExtraction;
using static PicView.FileHandling.FileLists;
using static PicView.UILogic.Sizing.ScaleImage;

namespace PicView.ChangeImage;

internal static class QuickLoad
{
    /// <summary>
    /// Load Image from blank values and show loading preview
    /// </summary>
    /// <param name="file"></param>
    internal static async Task QuickLoadAsync(string file)
    {
        var mainWindow = ConfigureWindows.GetMainWindow;
        InitialPath = file;
        var fileInfo = new FileInfo(file);
        if (!fileInfo.Exists) // If not file, try to load if URL, base64 or directory
        {
            await LoadPicFromStringAsync(file, fileInfo).ConfigureAwait(false);
            return;
        }
        if (file.IsArchive()) // Handle if file exist and is archive
        {
            await LoadPicFromArchiveAsync(file).ConfigureAwait(false);
            return;
        }
        var bitmapSource = await ImageDecoder.ReturnBitmapSourceAsync(fileInfo).ConfigureAwait(false);
        await mainWindow.MainImage.Dispatcher.InvokeAsync(() =>
        {
            if (fileInfo.Extension.ToLowerInvariant() == ".gif")
            {
                AnimationBehavior.SetSourceUri(ConfigureWindows.GetMainWindow.MainImage, new Uri(fileInfo.FullName));
            }
            else
            {
                ConfigureWindows.GetMainWindow.MainImage.Source = bitmapSource;
            }

            FitImage(bitmapSource.Width, bitmapSource.Height);
        }, DispatcherPriority.Send);

        Pics = await Task.FromResult(FileList(fileInfo)).ConfigureAwait(false);
        FolderIndex = Pics.IndexOf(fileInfo.FullName);

        await mainWindow.Dispatcher.InvokeAsync(() =>
        {
            SetTitleString(bitmapSource.PixelWidth, bitmapSource.PixelHeight, FolderIndex, fileInfo);
            UC.GetSpinWaiter.Visibility = Visibility.Collapsed;
            ConfigureWindows.GetMainWindow.MainImage.Cursor = Cursors.Arrow;
        }, DispatcherPriority.Normal);

        if (FolderIndex > 0)
        {
            Taskbar.Progress((double)FolderIndex / Pics.Count);
            _ = PreLoader.PreLoadAsync(FolderIndex, Pics.Count).ConfigureAwait(false);
        }

        _ = PreLoader.AddAsync(FolderIndex, fileInfo, bitmapSource).ConfigureAwait(false);

        if (Settings.Default.IsBottomGalleryShown)
        {
            await GalleryLoad.LoadAsync().ConfigureAwait(false);
            // Update gallery selections
            await UC.GetPicGallery.Dispatcher.InvokeAsync(() =>
            {
                // Select current item
                GalleryNavigation.SetSelected(FolderIndex, true);
                GalleryNavigation.SelectedGalleryItem = FolderIndex;
                GalleryNavigation.ScrollToGalleryCenter();
            });
        }

        // Add recent files, except when browsing archive
        if (string.IsNullOrWhiteSpace(TempZipFile) && Pics.Count > FolderIndex)
        {
            GetFileHistory ??= new FileHistory();
            GetFileHistory.Add(Pics[FolderIndex]);
        }
    }
}