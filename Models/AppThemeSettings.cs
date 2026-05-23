using System;

namespace PowerGridEditor
{
    public static class AppThemeSettings
    {
        private static bool isDarkTheme = true;
        public static event Action<bool> ThemeChanged;

        public static bool IsDarkTheme
        {
            get => isDarkTheme;
            set
            {
                if (isDarkTheme == value) return;
                isDarkTheme = value;
                ThemeChanged?.Invoke(isDarkTheme);
            }
        }
    }
}
