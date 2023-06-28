﻿namespace Speedy.Scripts.Data
{
    [System.Serializable]
    internal class CopyingData
    {
        public bool KeepTheNewest;
        public bool KeepDeleted;

        public bool PausedOnDelete;
        public bool PausedWhenCreatingDirectories;

        public string Source;
        public string Destination;
        public int LastFileIndex;
        public long LastPos;
        public double LastProgressBarWidth;

        public CopyingData(string source, string destination, int LastFileOrDirIndex, long LastPos,
                             double lastProgressBarWidth , bool keepTheNewest, bool pausedOnDelete = false , bool directorypause = false)
        {
            Source = source;
            Destination = destination;
            this.LastFileIndex = LastFileOrDirIndex;
            this.LastPos = LastPos;
            LastProgressBarWidth = lastProgressBarWidth;
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