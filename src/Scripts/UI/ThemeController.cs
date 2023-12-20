using System;
using Avalonia.Styling;
namespace Speedy.Scripts
{
    /// <summary>
    /// Used to control the app's Theme
    /// </summary>
    internal static class ThemeController
    {
        /// <summary>
        /// The main theme for the app
        /// </summary>
        public static ThemeVariant MainTheme 
        { 
            get 
            {
                return _MainTheme;
            } 
            set 
            { 
                _MainTheme = value;
                OnThemeChanged?.Invoke();
            } 
        }

        private static ThemeVariant _MainTheme = ThemeVariant.Light;

        public static event Action OnThemeChanged;
    }
}
