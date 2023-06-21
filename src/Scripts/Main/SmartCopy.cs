using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Speedy.Scripts.Main
{
    internal static class SmartCopy
    {
        /// <summary>
        /// Copy a source directory (From) to a destination directory by only moving the differences between the two and ignoring the similarities
        /// </summary>
        /// <param name="From">the source direcrtory</param>
        /// <param name="To">the destination direcrtory</param>
        /// <param name="KeepDeleted">
        /// indicates if we should keep the files that are present in the destination directory but not the source direcotory
        /// </param>
        /// <param name="KeepTheNewest">
        /// indicates if we should always keep the newest version between the destination and the source file
        /// </param>
        /// <param name="ToDoWhenComplete">will be invoked once the copying is done</param>
        /// <param name="CancelToken">the Cancelation token that cancels this copying function</param>
        /// <param name="DataContext">The data context of the main window (used to update the progress bar)</param>
        /// <param name="MaxWidth">The Width of the main window (used to update the progress bar)</param> 
        public static void CopyDifference(string From , string To , bool KeepDeleted , bool KeepTheNewest , CancellationToken CancelToken ,
                                          Action ToDoWhenComplete = null , BaseWindowDataContext DataContext = null , double MaxWidth = 0)
        {
            try
            {
                if (!KeepDeleted)
                    DeleteDifferenceFiles(From, To, CancelToken);

                MoveDifferenceFiles(From, To, KeepTheNewest, CancelToken , DataContext , MaxWidth);
                if (ToDoWhenComplete != null)
                    Dispatcher.UIThread.Post(ToDoWhenComplete);
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// Move the new or changed files by comparing a source directory (From) and a destination directory (To)
        /// </summary>
        /// <param name="From">Source directory</param>
        /// <param name="To">Destination directory</param>
        /// <param name="KeepTheNewest">
        /// indicates if we should always keep the newest version between the destination and the source file
        /// </param>
        public static void MoveDifferenceFiles(string From, string To , bool KeepTheNewest , CancellationToken CancelToken,
                                               BaseWindowDataContext DataContext = null, double MaxWidth = 0)
        {
            if (!Directory.Exists(From))
                throw new DirectoryNotFoundException("The source directory doesn't exist");
            if (!Directory.Exists(To))
                throw new DirectoryNotFoundException("The destination directory doesn't exist");

            if (DataContext != null) // Reseting the progress bar
                DataContext.ProgressWidth = 0;

            int TotalFiles = Directory.GetFiles(From, "*.*", SearchOption.AllDirectories).Length;//getting the total number of files in the source
            double Step = TotalFiles != 0 ? MaxWidth / TotalFiles : 0;//getting the step for each file

            Stack<string> dirs = new Stack<string>(new string[] { From }); // the directories to retrieve the files from

            while (dirs.Count > 0) 
            {
                var dir = dirs.Pop();
                var fs = Directory.GetFiles(dir);

                foreach (string f in fs)
                {
                    if (CancelToken.IsCancellationRequested)
                        CancelToken.ThrowIfCancellationRequested();//Canceling the copying if requested

                    string relativepath = Path.GetRelativePath(From,f);
                    string newfile = Path.Combine(To,relativepath);

                    if (File.Exists(newfile))
                    {
                        var destinationfiletime = File.GetLastWriteTime(newfile);
                        var sourcefiletime = File.GetLastWriteTime(f);
                        if ((KeepTheNewest && destinationfiletime < sourcefiletime) || (!KeepTheNewest && destinationfiletime != sourcefiletime))
                            File.Copy(f, newfile, true);
                    }
                    else
                        File.Copy(f, newfile);
                    if (DataContext != null)// Updating the progress bar
                        DataContext.ProgressWidth += Step;
                }

                var subDirs = Directory.GetDirectories(dir);

                foreach (string d in subDirs)
                {
                    if (CancelToken.IsCancellationRequested)
                        CancelToken.ThrowIfCancellationRequested();//Canceling the copying if requested

                    var relativedir = Path.GetRelativePath(From, d);
                    var newdir = Path.Combine(To,relativedir);
                    if (!Directory.Exists(newdir))
                        Directory.CreateDirectory(newdir);
                        
                    dirs.Push(d);
                }
            }

            if (DataContext != null) // Reseting the progress bar
                DataContext.ProgressWidth = MaxWidth;
        }

        public static void MoveDifferenceBetween(string Source , string Destination)
        {
            if (!File.Exists(Source))
                throw new FileNotFoundException($"The Source File {Source} doesn't exist !");
            if (!File.Exists(Destination))
                throw new FileNotFoundException($"The Destination File {Destination} doesn't exist !");
        }

        /// <summary>
        /// Deletes the files that are in the destination directory but not in the source directory
        /// </summary>
        /// <param name="Source">The source directory</param>
        /// <param name="Destination">The destination directory</param>
        public static void DeleteDifferenceFiles(string Source,string Destination, CancellationToken CancelToken)
        {
            if (!Directory.Exists(Destination))
                throw new DirectoryNotFoundException("The destination directory doesn't exist");
            if (!Directory.Exists(Source))
                throw new DirectoryNotFoundException("The source directory doesn't exist");

            Stack<string> dirs = new Stack<string>(new string[] { Destination }); // the directories to retrieve the files from

            while (dirs.Count > 0) 
            {
                var dir = dirs.Pop();
                var fs = Directory.GetFiles(dir);

                foreach (string f in fs)
                {
                    if (CancelToken.IsCancellationRequested)
                        CancelToken.ThrowIfCancellationRequested();//Canceling the copying if requested

                    string relativepath = Path.GetRelativePath(Destination,f);
                    string newfile = Path.Combine(Source,relativepath);

                    if (!File.Exists(newfile))
                        File.Delete(f);
                }

                var subDirs = Directory.GetDirectories(dir);

                foreach (string d in subDirs)
                {
                    if (CancelToken.IsCancellationRequested)
                        CancelToken.ThrowIfCancellationRequested();//Canceling the copying if requested

                    var relativedir = Path.GetRelativePath(Destination, d);
                    var newdir = Path.Combine(Source,relativedir);
                    if (!Directory.Exists(newdir))
                        Directory.Delete(d,true);
                    else
                        dirs.Push(d);
                }
            }
        }
    }
}
