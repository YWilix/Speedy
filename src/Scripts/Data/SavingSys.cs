using Avalonia.Styling;
using System;
using System.IO;
using System.Text.Json;

namespace Speedy.Scripts.Data
{
    internal static class SavingSys
    {
        /// <summary>
        /// The default copying data path
        /// </summary>
        public static string defaultdatapath = Environment.CurrentDirectory + @"\SpeedyData\LastCopyingData.scd";

        public static string defaultlastworkingfilepath = Environment.CurrentDirectory + @"\SpeedyData\LastCopyingData.lwf";

        /// <summary>
        /// Save an object to a path using a binaryformatter
        /// </summary>
        /// <param name="Path">The path to save to</param>
        /// <param name="Obj">The object to save</param>
        public static void SaveObj<T>(string Path , T Obj)
        {
            FileStream fs = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.Write);
            JsonSerializerOptions opts = new JsonSerializerOptions() { IncludeFields = true};
            JsonSerializer.Serialize(fs, Obj,opts);
            fs.Close();
        }

        /// <summary>
        /// loads an object from a path using a binaryformatter and returns it
        /// </summary>
        /// <param name="Path">The path to load from</param>
        public static T LoadObj<T>(string Path)
        {
            if (!File.Exists(Path))
                throw new Exception($"File {Path} not found !");

            FileStream fs = new FileStream(Path, FileMode.Open, FileAccess.Read);
            JsonSerializerOptions opts = new JsonSerializerOptions() { IncludeFields = true };
            T ToReturn = JsonSerializer.Deserialize<T>(fs,opts);

            fs.Close();

            return ToReturn;
        } 

        /// <summary>
        /// Saves the current theme to the default theme file
        /// </summary>
        public static void SaveTheme()
        {
            string Path = Environment.CurrentDirectory + @"\SpeedyData\Theme.std"; //the default theme path
            var CurrentTheme = ThemeController.MainTheme == ThemeVariant.Light;

            SaveObj(Path,CurrentTheme);
        }

        /// <summary>
        /// Loads the current theme from the default theme file and returns it
        /// </summary>
        public static bool? LoadTheme()
        {
            string Path = Environment.CurrentDirectory + @"\SpeedyData\Theme.std"; //the default theme path
            return LoadObj<bool?>(Path);
        }

        /// <summary>
        /// Saves a specific Copying Data to a path (if the path is null it's saved to the default path) 
        /// </summary>
        public static void SaveCopyingData(CopyingData Data ,string? path = null)
        {
            string _path = path == null ? defaultdatapath : path;

            SaveObj(_path,Data);
        }
        
        /// <summary>
        /// Loads a specific Copying Data from a path (if the path is null the data is loaded from the default path) 
        /// </summary>
        public static CopyingData LoadCopyingData(string? path = null)
        {
            string _path = path == null ? defaultdatapath : path;

            return LoadObj<CopyingData>(_path);
        } 

        /// <summary>
        /// Saves the last worked on Copying Data file path
        /// </summary>
        /// <param name="WorkingFilePath">the copying data file path</param>
        public static void SaveLastWorkingFile(string WorkingFilePath)
        {
            SaveObj(defaultlastworkingfilepath, WorkingFilePath);
        }
        
        /// <summary>
        /// Loads the last worked on Copying Data file path
        /// </summary>
        public static string LoadLastWorkingFile()
        {
            return LoadObj<string>(defaultlastworkingfilepath);
        }

        /// <summary>
        /// Loads the last worked on Copying Data file path to the default path
        /// </summary>
        public static void ResetLastWorkingFile()
        {
            SaveLastWorkingFile(defaultdatapath);
        }
    }
}
