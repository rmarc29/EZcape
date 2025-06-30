// ThemeManager.cs
using System;
using System.Linq;
using System.Windows;

namespace EZcape
{
    public static class ThemeManager
    {
        public static void SetTheme(string themeName)
        {
            // Construct the URI for the theme's ResourceDictionary file.
            // IMPORTANT: Replace "EZcape" with your actual project name if it's different.
            var themeUri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);

            // Find any existing theme dictionary.
            var existingTheme = Application.Current.Resources.MergedDictionaries
                                  .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));

            if (existingTheme != null)
            {
                // If a theme is already loaded, just update its source.
                existingTheme.Source = themeUri;
            }
            else
            {
                // If no theme is loaded, add the new one.
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = themeUri });
            }
        }
    }
}