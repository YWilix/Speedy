﻿using Avalonia.Themes.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static FluentThemeMode MainTheme 
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

        private static FluentThemeMode _MainTheme = FluentThemeMode.Dark;

        public static event Action OnThemeChanged;
    }
}