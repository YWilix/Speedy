using Avalonia.Threading;
using Speedy.Scripts.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Speedy.Scripts.Main
{
    internal static class SmartCopy
    {
        private static CopyStateData LastState = null;

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
        /// <param name="PauseToken">the Cancelation token that cancels this copying function</param>
        /// <param name="DataContext">The data context of the main window (used to update the progress bar)</param>
        /// <param name="MaxWidth">The Width of the main window (used to update the progress bar)</param> 
        public static void CopyDifference(string From , string To , bool KeepDeleted , bool KeepTheNewest , CancellationToken PauseToken ,
                                          Action ToDoWhenComplete = null , Action<CopyStateData> ToDoWhenPause = null,BaseWindowDataContext DataContext = null , 
                                          double MaxWidth = 0, CopyStateData Data = null)
        {
            bool ispause = false;

            try
            {
                if (!KeepDeleted)
                    DeleteDifferenceFiles(From, To, PauseToken,KeepTheNewest);

                var PassedCopyData = (Data != null && !Data.PausedOnDelete) ? Data : null;//The copy data to passe to CopyModifiedFiles function

                CopyModifiedFiles(From, To, KeepTheNewest, PauseToken ,PassedCopyData, DataContext , MaxWidth);
            }
            catch(OperationCanceledException e) //The operation is canceled
            {
                LastState.Source = From;
                LastState.Destination = To;

                //The KeepDeleted Bool shouldn't change from the first time it's been saved , this code ensures that 
                if (Data == null)
                    LastState.KeepDeleted = KeepDeleted;
                else
                    LastState.KeepDeleted = Data.KeepDeleted;

                if (ToDoWhenPause != null)
                    ToDoWhenPause(LastState);
                ispause = true;
            }
            finally 
            {
                if (!ispause && ToDoWhenComplete != null)
                    Dispatcher.UIThread.Post(ToDoWhenComplete);
            }
        }

        /// <summary>
        /// Copies the new or changed files by comparing a source directory (From) and a destination directory (To)
        /// </summary>
        /// <param name="From">Source directory</param>
        /// <param name="To">Destination directory</param>
        /// <param name="KeepTheNewest">
        /// indicates if we should always keep the newest version between the destination and the source file
        /// </param>
        public static void CopyModifiedFiles(string From, string To , bool KeepTheNewest , CancellationToken PauseToken, CopyStateData Data = null ,
                                               BaseWindowDataContext DataContext = null, double MaxWidth = 0)
        {
            if (!Directory.Exists(From))
                throw new DirectoryNotFoundException("The source directory doesn't exist");
            if (!Directory.Exists(To))
                throw new DirectoryNotFoundException("The destination directory doesn't exist");
            if (DataContext == null)
                throw new ArgumentNullException(nameof(DataContext));

            // Reseting the progress bar or setting it to the last state
            DataContext.ProgressWidth = Data == null ? 0 : Data.LastProgressBarWidth;

            if (Data != null && !Data.PausedWhenCreatingDirectories)//All the directories are already created
                goto CopyingFiles;

            var AllDirectories = Directory.GetDirectories(From, "**", SearchOption.AllDirectories);//getting all the direcories available in the source

            if (AllDirectories != null)
            {
                //Create the same directories but in the destination
                foreach ( string dir in AllDirectories )
                {
                    if (PauseToken.IsCancellationRequested)
                    {
                        LastState = new CopyStateData(From,To,false, KeepTheNewest,true);
                        PauseToken.ThrowIfCancellationRequested();
                    }

                    var RelativeDirPath = Path.GetRelativePath(From, dir);
                    var DirDestPath = Path.Combine(To,RelativeDirPath);// the directory that should be in the destination
                    Directory.CreateDirectory(DirDestPath);
                }
            }

        CopyingFiles:

            var AllFiles = Directory.GetFiles(From, "*.*", SearchOption.AllDirectories);//getting all the files in the source
            int AllFilesNumber = AllFiles != null ? AllFiles.Length : 0;//getting the total number of files in the source
            double Step = AllFilesNumber != 0 ? MaxWidth / AllFilesNumber : 0;//getting the step for each file

            int StartingFileIndex = (Data != null && !Data.PausedWhenCreatingDirectories) ? Data.LastFileIndex : 0;
            bool IsStoppingFile = Data != null && !Data.PausedWhenCreatingDirectories;//Is it the file that we paused it's copying operation

            if (AllFiles != null)
            {
                for (int i = StartingFileIndex; i < AllFilesNumber; i++)//Copying all the files from the source
                {
                    string f = AllFiles[i];
                    string relativepath = Path.GetRelativePath(From, f);
                    string newfile = Path.Combine(To, relativepath);

                    if (File.Exists(newfile))
                    {
                        var destinationfiletime = File.GetLastWriteTime(newfile);
                        var sourcefiletime = File.GetLastWriteTime(f);
                        if (IsStoppingFile)//the file is the one that we need to continue it's copying
                        {
                            OverwriteFile(f, newfile, i, DataContext.ProgressWidth, PauseToken, KeepTheNewest, Data.LastPos);//copies the remaining difference
                            IsStoppingFile = false;
                        }
                        else if ((KeepTheNewest && destinationfiletime < sourcefiletime) || (!KeepTheNewest && destinationfiletime != sourcefiletime))
                            OverwriteFile(f, newfile, i, DataContext.ProgressWidth, PauseToken, KeepTheNewest);
                    }
                    else
                    {
                        CopyFile(f, newfile, i, DataContext.ProgressWidth, PauseToken, KeepTheNewest);
                    }
                    DataContext.ProgressWidth += Step;// Updating the progress bar
                }
            }
            DataContext.ProgressWidth = MaxWidth;
        }

        /// <summary>
        /// Overwrites a file with the ability to pause and save the copying data (save where the system stopped)
        /// </summary>
        /// <param name="Source">The file to copy</param>
        /// <param name="Destination">The file to overwrite</param>
        /// <param name="index">The index of the file inside the main directory (used to save the copying data)</param>
        /// <param name="ProgressBarWidth">The progress bar width when the system paused (used continue copying later)</param>
        /// <param name="PauseToken">The tokken that pauses the copying operation</param>
        /// <param name="IsKeepTheNewest">indicates if the whole operation keeps the latest version (used when saving the copying data)</param>
        /// <param name="OldPos">the position to start copying the file from (used to continue copying where we stopped)</param>
        public static void OverwriteFile(string Source , string Destination ,int index , double ProgressBarWidth ,
                                                      CancellationToken PauseToken ,bool IsKeepTheNewest, long OldPos = 0)
        {
            if (!File.Exists(Source))
                throw new FileNotFoundException($"The Source File {Source} doesn't exist {File.Exists(Source)} !");
            if (!File.Exists(Destination))
                throw new FileNotFoundException($"The Destination File {Destination} doesn't exist !");

            var Sourcef = new FileStream(Source, FileMode.Open , FileAccess.Read);// the source file stream
            var Destf = new FileStream(Destination, FileMode.Open , FileAccess.ReadWrite);// the destination file stream

            Destf.SetLength(Sourcef.Length);//Setting the length of the destination file

            //Setting the positions to where we stopped (zero if it's a new copying operation)
            Sourcef.Position = OldPos;
            Destf.Position = OldPos;

            int buffersize = 1048576 * 2;// a size of 2 megabytes
            byte[] buffer = new byte[buffersize];//the buffer
            int bytesnumber;
            while ((bytesnumber = Sourcef.Read(buffer,0,buffersize)) > 0)
            {
                Destf.Write(buffer,0, bytesnumber);

                if (PauseToken.IsCancellationRequested)//Checking if the copying has paused
                {
                    LastState = new CopyStateData("", "", index, Destf.Position, ProgressBarWidth,IsKeepTheNewest);
                    PauseToken.ThrowIfCancellationRequested();
                }
            }

            Sourcef.Close();
            Destf.Close();

            File.SetLastWriteTime(Destination,File.GetLastWriteTime(Source));//Setting the last write time so speedy knows it's not modified
        }
        /// <summary>
        /// Copies a file (to a destination that doesn't exit) with the ability to pause and save the copying data (save where the system stopped)
        /// </summary>
        /// <param name="Source">The file to copy</param>
        /// <param name="Destination">The destination to copy the file to</param>
        /// <param name="index">The index of the file inside the main directory (used to save the copying data)</param>
        /// <param name="ProgressBarWidth">The progress bar width when the system paused (used continue copying later)</param>
        /// <param name="PauseToken">The tokken that pauses the copying operation</param>
        /// <param name="IsKeepTheNewest">indicates if the whole operation keeps the latest version (used when saving the copying data)</param>
        public static void CopyFile(string Source, string Destination, int index, double ProgressBarWidth,
                                                      CancellationToken PauseToken, bool IsKeepTheNewest)
        {
            if (!File.Exists(Source))
                throw new FileNotFoundException($"The Source File {Source} doesn't exist {File.Exists(Source)} !");
            if (File.Exists(Destination))
                throw new Exception($"The Destiantion File {Destination} shouldn't exist !");

            var Destf = File.Create(Destination);//Creating The File of the destination

            var Sourcef = new FileStream(Source, FileMode.Open, FileAccess.Read);// the source file stream

            Destf.SetLength(Sourcef.Length);

            Sourcef.Position = 0;
            Destf.Position = 0;

            int buffersize = 1048576 * 2;// a size of 2 megabytes
            byte[] buffer = new byte[buffersize];//the buffer
            int bytesnumber;
            while ((bytesnumber = Sourcef.Read(buffer, 0, buffersize)) > 0)
            {
                Destf.Write(buffer, 0, bytesnumber);

                if (PauseToken.IsCancellationRequested)//Checking if the copying has paused
                {
                    LastState = new CopyStateData("", "", index, Destf.Position, ProgressBarWidth, IsKeepTheNewest);
                    PauseToken.ThrowIfCancellationRequested();
                }
            }
            Sourcef.Close();
            Destf.Close();

            File.SetLastWriteTime(Destination, File.GetLastWriteTime(Source));//Setting the last write time so speedy knows it's not modified
        }

        /// <summary>
        /// Deletes the files that are in the destination directory but not in the source directory
        /// </summary>
        /// <param name="Source">The source directory</param>
        /// <param name="Destination">The destination directory</param>
        public static void DeleteDifferenceFiles(string Source,string Destination, CancellationToken PauseToken ,bool IsKeepTheNewest)
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
                    if (PauseToken.IsCancellationRequested)//The copying is paused
                    {
                        LastState = new CopyStateData(Source,Destination,true, IsKeepTheNewest);
                        PauseToken.ThrowIfCancellationRequested();//Canceling the copying
                    }
                       

                    string relativepath = Path.GetRelativePath(Destination,f);
                    string newfile = Path.Combine(Source,relativepath);

                    if (!File.Exists(newfile))
                        File.Delete(f);
                }

                var subDirs = Directory.GetDirectories(dir);

                foreach (string d in subDirs)
                {
                    if (PauseToken.IsCancellationRequested)//The copying is paused
                    {
                        LastState = new CopyStateData(Source, Destination, true, IsKeepTheNewest);
                        PauseToken.ThrowIfCancellationRequested();//Canceling the copying
                    }

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
