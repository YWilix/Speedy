﻿using System.Reflection;
using System.Diagnostics;
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
        private const int buffersize = 1048576 * 2;// a size of 2 megabytes

        private static CopyingData LastState = null;

        /// <summary>
        /// Copy a source directory (From) to a destination directory by only moving the differences between the two and ignoring the similarities
        /// </summary>
        /// <param name="From">the source direcrtory</param>
        /// <param name="To">the destination direcrtory</param>
        /// <param name="DeleteAdditionalFiles">
        /// indicates if we should delete the files that are present in the destination directory but not the source directory
        /// </param>
        /// <param name="KeepTheNewest">
        /// indicates if we should always keep the newest version between the destination and the source file
        /// </param>
        /// <param name="ToDoWhenComplete">will be invoked once the copying is done</param>
        /// <param name="PauseToken">the Cancelation token that cancels this copying function</param>
        /// <param name="DataContext">The data context of the main window (used to update the progress bar)</param>
        /// <param name="MaxWidth">The Width of the main window (used to update the progress bar)</param> 
        public static void CopyDifference(string From , string To , bool DeleteAdditionalFiles, bool KeepTheNewest , CancellationToken PauseToken ,
                                          Action ToDoWhenComplete = null , SendOrPostCallback ToDoWhenError = null, Action<CopyingData> ToDoWhenPause = null,BaseWindowDataContext DataContext = null , 
                                          double MaxWidth = 0, CopyingData Data = null)
        {
            bool ispause = false;

            bool IsDirectory = Directory.Exists(From);

            try
            {
                if (IsDirectory)//Then we are copying the content of a directory
                {
                    if (DeleteAdditionalFiles)
                        DeleteDifferenceFiles(From, To, PauseToken, KeepTheNewest);

                    var PassedCopyData = (Data != null && !Data.PausedOnDelete) ? Data : null;//The copy data to passe to CopyModifiedFiles function

                    CopyModifiedFiles(From, To, KeepTheNewest, PauseToken, PassedCopyData, DataContext, MaxWidth);
                }
                else
                {
                    var Paths = GetFilesPaths(From);

                    IsFilesListValid(Paths);

                    var PassedCopyData = (Data != null && !Data.PausedOnDelete) ? Data : null;//The copy data to passe to CopyModifiedFiles functio

                    CopyAllFiles(Paths, To, KeepTheNewest, PauseToken, PassedCopyData, DataContext, MaxWidth);
                }
            }
            catch(OperationCanceledException e) //The operation is canceled
            {
                LastState.Source = From;
                LastState.Destination = To;
                //Setting the Data Product Version
                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = fileVersionInfo.ProductVersion;
                LastState._DataProductVersion = version;

                //The KeepDeleted Bool shouldn't change from the first time it's been saved , this code ensures that 
                if (Data == null)
                    LastState.RemoveAdditionalFiles = DeleteAdditionalFiles;
                else
                    LastState.RemoveAdditionalFiles = Data.RemoveAdditionalFiles;

                if (IsDirectory) 
                {
                    var sourcetime = Directory.GetLastWriteTime(From);
                    LastState.SourceLastWriteTime = sourcetime;

                    var destinationtime = Directory.GetLastWriteTime(To);
                    LastState.DestinationLastWriteTime = destinationtime;
                }
                else
                {
                    LastState.SourceLastWriteTime = null;
                    LastState.DestinationLastWriteTime = null;
                }

                if (ToDoWhenPause != null)
                    ToDoWhenPause(LastState);
                ispause = true;
            }
            catch (Exception e)
            {
                if (ToDoWhenError != null)
                    Dispatcher.UIThread.Post(ToDoWhenError,e.Message);
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
        public static void CopyModifiedFiles(string From, string To , bool KeepTheNewest , CancellationToken PauseToken, CopyingData Data = null ,
                                               BaseWindowDataContext DataContext = null, double MaxWidth = 0)
        {
            if (!Directory.Exists(From))
                throw new DirectoryNotFoundException("The source directory doesn't exist");
            if (!Directory.Exists(To))
                throw new DirectoryNotFoundException("The destination directory doesn't exist");
            if (DataContext == null)
                throw new ArgumentNullException(nameof(DataContext));

            // Reseting the progress bar or setting it to the last state
            DataContext.ProgressWidth = Data == null ? 0 : Data.LastProgressWidthRelativeToFiles;

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
                        LastState = new CopyingData(From,To,false, KeepTheNewest,true);
                        PauseToken.ThrowIfCancellationRequested();
                    }

                    var RelativeDirPath = System.IO.Path.GetRelativePath(From, dir);
                    var DirDestPath = System.IO.Path.Combine(To,RelativeDirPath);// the directory that should be in the destination
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
                double CurrentProgressWidth = Data != null ? Data.LastProgressWidthRelativeToFiles : 0;

                for (int i = StartingFileIndex; i < AllFilesNumber; i++)//Copying all the files from the source
                {
                    string f = AllFiles[i];
                    string relativepath = System.IO.Path.GetRelativePath(From, f);
                    string newfile = System.IO.Path.Combine(To, relativepath);

                    if (File.Exists(newfile))
                    {
                        var destinationfiletime = File.GetLastWriteTime(newfile);
                        var sourcefiletime = File.GetLastWriteTime(f);
                        if (IsStoppingFile)//the file is the one that we need to continue it's copying
                        {
                            OverwriteFile(f, newfile, i, DataContext.ProgressWidth, PauseToken, KeepTheNewest, Data.LastPos, Step: Step, DataContext: DataContext);//copies the remaining difference
                            IsStoppingFile = false;
                        }
                        else if ((KeepTheNewest && destinationfiletime < sourcefiletime) || (!KeepTheNewest && destinationfiletime != sourcefiletime))
                            OverwriteFile(f, newfile, i, DataContext.ProgressWidth, PauseToken, KeepTheNewest,Step:Step,DataContext:DataContext);
                    }
                    else
                    {
                        CopyFile(f, newfile, i, DataContext.ProgressWidth, PauseToken, KeepTheNewest, Step: Step, DataContext: DataContext);
                    }

                    CurrentProgressWidth = DataContext.ProgressWidth;
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
        /// <param name="StartProgressBarWidth">The Starting Progress bar width (the width before the beginning of this function)</param>
        /// <param name="PauseToken">The tokken that pauses the copying operation</param>
        /// <param name="IsKeepTheNewest">indicates if the whole operation keeps the latest version (used when saving the copying data)</param>
        /// <param name="OldPos">the position to start copying the file from (used to continue copying where we stopped)</param>
        /// <param name="Step">The step of change of the progress bar width for each file copied</param>
        /// <param name="DataContext">The data context of the main window (used to modifie the progress bar)</param>
        public static void OverwriteFile(string Source , string Destination ,int index , double StartProgressBarWidth , CancellationToken PauseToken ,
                                         bool IsKeepTheNewest, long OldPos = 0 , double Step = 0 , BaseWindowDataContext DataContext = null)
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

            byte[] buffer = new byte[buffersize];//the buffer
            int bytesnumber;
            while ((bytesnumber = Sourcef.Read(buffer,0,buffersize)) > 0)
            {
                Destf.Write(buffer,0, bytesnumber);

                double Percentage = Convert.ToDouble((Convert.ToDecimal(Destf.Position) / Convert.ToDecimal(Destf.Length)));
                DataContext.ProgressWidth = StartProgressBarWidth + Percentage * Step;// Updating the progress bar

                if (PauseToken.IsCancellationRequested)//Checking if the copying has paused
                {
                    LastState = new CopyingData("", "", index, Destf.Position, StartProgressBarWidth,IsKeepTheNewest, LastProgressBarWidth: DataContext.ProgressWidth);
                    Sourcef.Close();
                    Destf.Close();
                    PauseToken.ThrowIfCancellationRequested();
                }
            }

            DataContext.ProgressWidth = StartProgressBarWidth + Step;

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
        /// <param name="StartProgressBarWidth">The progress bar width when the system paused (used continue copying later)</param>
        /// <param name="PauseToken">The tokken that pauses the copying operation</param>
        /// <param name="IsKeepTheNewest">indicates if the whole operation keeps the latest version (used when saving the copying data)</param>
        /// <param name="Step">The step of change of the progress bar width for each file copied</param>
        /// <param name="DataContext">The data context of the main window (used to modifie the progress bar)</param>
        public static void CopyFile(string Source, string Destination, int index, double StartProgressBarWidth, CancellationToken PauseToken ,
                                    bool IsKeepTheNewest, double Step = 0, BaseWindowDataContext DataContext = null)
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

            byte[] buffer = new byte[buffersize];//the buffer
            int bytesnumber;
            while ((bytesnumber = Sourcef.Read(buffer, 0, buffersize)) > 0)
            {
                Destf.Write(buffer, 0, bytesnumber);

                double Percentage = Convert.ToDouble((Convert.ToDecimal(Destf.Position) / Convert.ToDecimal(Destf.Length)));
                DataContext.ProgressWidth = StartProgressBarWidth + Percentage * Step;// Updating the progress bar

                if (PauseToken.IsCancellationRequested)//Checking if the copying has paused
                {
                    LastState = new CopyingData("", "", index, Destf.Position, StartProgressBarWidth, IsKeepTheNewest,LastProgressBarWidth: DataContext.ProgressWidth);
                    Sourcef.Close();
                    Destf.Close();
                    PauseToken.ThrowIfCancellationRequested();
                }
            }

            DataContext.ProgressWidth = StartProgressBarWidth + Step;

            Sourcef.Close();
            Destf.Close();

            File.SetLastWriteTime(Destination, File.GetLastWriteTime(Source));//Setting the last write time so speedy knows it's not modified
        }

        /// <summary>
        /// Copies all the files in the list (Paths) to a destination directory (To)
        /// </summary>
        /// <param name="Paths">The paths of the files</param>
        /// <param name="To">Destination directory</param>
        /// <param name="KeepTheNewest">
        /// indicates if we should always keep the newest version between the destination and the source file
        /// </param>
        public static void CopyAllFiles(List<string> Paths, string To, bool KeepTheNewest, CancellationToken PauseToken, CopyingData Data = null,
                                               BaseWindowDataContext DataContext = null, double MaxWidth = 0)
        {
            int AllFilesNumber = Paths.Count;//getting the total number of files in the source
            double Step = AllFilesNumber != 0 ? MaxWidth / AllFilesNumber : 0;//getting the step for each file

            int StartingFileIndex = Data != null ? Data.LastFileIndex : 0;
            bool IsStoppingFile = Data != null;//Is it the file that we paused it's copying operation
            long StartingFilePos = IsStoppingFile ? Data.LastPos : 0; // The starting position of the copying of the file
            // used to not start the copying from the beginning if it's Saved

            double CurrentProgressWidth = Data != null ? Data.LastProgressWidthRelativeToFiles : 0;

            for (int i = StartingFileIndex; i < AllFilesNumber; i++)//Copying all the files from the source
            {
                string f = Paths[i];
                string FileName = System.IO.Path.GetFileName(Paths[i]);
                string newfile = System.IO.Path.Combine(To, FileName);

                if (File.Exists(newfile))
                    OverwriteFile(f, newfile, i, CurrentProgressWidth, PauseToken, KeepTheNewest, StartingFilePos, Step: Step, DataContext: DataContext);//copies the remaining difference
                else
                    CopyFile(f, newfile, i, CurrentProgressWidth, PauseToken, KeepTheNewest, Step: Step, DataContext: DataContext);

                CurrentProgressWidth = DataContext.ProgressWidth;
            }

            DataContext.ProgressWidth = MaxWidth;
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
                        LastState = new CopyingData(Source,Destination,true, IsKeepTheNewest);
                        PauseToken.ThrowIfCancellationRequested();//Canceling the copying
                    }
                       

                    string relativepath = System.IO.Path.GetRelativePath(Destination,f);
                    string newfile = System.IO.Path.Combine(Source,relativepath);

                    if (!File.Exists(newfile))
                        File.Delete(f);
                }

                var subDirs = Directory.GetDirectories(dir);

                foreach (string d in subDirs)
                {
                    if (PauseToken.IsCancellationRequested)//The copying is paused
                    {
                        LastState = new CopyingData(Source, Destination, true, IsKeepTheNewest);
                        PauseToken.ThrowIfCancellationRequested();//Canceling the copying
                    }

                    var relativedir = System.IO.Path.GetRelativePath(Destination, d);
                    var newdir = System.IO.Path.Combine(Source,relativedir);
                    if (!Directory.Exists(newdir))
                        Directory.Delete(d,true);
                    else
                        dirs.Push(d);
                }
            }
        }

        public static List<string> GetFilesPaths(string str)
        {
            return new List<string>(str.Split(';'));
        }

        /// <summary>
        /// Checks every file path and throws an exception if it doesn't exist (the function doesn't do anything if all paths are valid)
        /// </summary>
        /// <param name="paths">the list of files paths</param>²
        public static void IsFilesListValid(List<string> paths)
        {
            List<string> FilesNames = new List<string>();

            if (paths.Count == 0)
                throw new Exception("you didn't specifie anything to copy");

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                    throw new Exception($"The file {path} wasn't found ");


                var FileName = System.IO.Path.GetFileName(path);
                
                if (FilesNames.Contains(FileName))
                    throw new Exception($"The file name {FileName} exists more than one time");
                else
                    FilesNames.Add(FileName);
            }


        }

    }
}
