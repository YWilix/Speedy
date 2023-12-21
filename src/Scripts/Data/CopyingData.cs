using System;

namespace Speedy.Scripts.Data
{
    [System.Serializable]
    internal class CopyingData
    {
        /// <summary>
        /// The version of the Speedy product that this data belongs to (null means version 1.0.0)
        /// </summary>
        public string? _DataProductVersion = null;

        public bool KeepTheNewest;
        public bool RemoveAdditionalFiles;

        public bool PausedOnDelete;
        public bool PausedWhenCreatingDirectories;

        public string Source;
        public string Destination;
        public int LastFileIndex;
        public long LastPos;
        /// <summary>
        /// The last width of the progress bar without considering the progress added in the last stpped copying operation 
        /// </summary>
        public double LastProgressWidthRelativeToFiles;


        public double LastProgressBarWidth;

        public DateTime? SourceLastWriteTime;//used to check if the source folder has been modified , if so the system can't continue the copying operation
        public DateTime? DestinationLastWriteTime;//used to check if the destination folder has been modified , if so the system can't continue the copying operation 

        /// <summary>
        /// represents if this copying data encapsulates a "directory content copying" (if false that means that it encapsulates a copying of files)
        /// </summary>
        public bool IsDirectoryCopying => SourceLastWriteTime != null && DestinationLastWriteTime != null;


        public CopyingData(string source, string destination, int LastFileOrDirIndex, long LastPos,double lastProgressWidthRelativeToFiles ,
                           bool keepTheNewest, bool pausedOnDelete = false , bool directorypause = false ,double? LastProgressBarWidth = null )
        {
            Source = source;
            Destination = destination;
            this.LastFileIndex = LastFileOrDirIndex;
            this.LastPos = LastPos;
            LastProgressWidthRelativeToFiles = lastProgressWidthRelativeToFiles;
            this.LastProgressBarWidth = LastProgressBarWidth != null ? (double)LastProgressBarWidth : lastProgressWidthRelativeToFiles;
            PausedOnDelete = pausedOnDelete;
            PausedWhenCreatingDirectories = directorypause;
            this.KeepTheNewest = keepTheNewest;
        }

        public CopyingData(string source, string destination , bool pausedOnDelete, bool keepTheNewest ,bool directorypause = false)
        {
            Source = source;
            Destination = destination;
            PausedOnDelete = pausedOnDelete;
            KeepTheNewest = keepTheNewest;

        }
    }
}
