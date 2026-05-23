using System;

namespace PowerGridEditor
{
    public static class AppRuntimeSettings
    {
        private static int updateIntervalSeconds = 2;
        public static event Action<int> UpdateIntervalChanged;

        public static int UpdateIntervalSeconds
        {
            get { return updateIntervalSeconds; }
            set
            {
                int sanitized = value < 1 ? 1 : value;
                if (updateIntervalSeconds == sanitized) return;
                updateIntervalSeconds = sanitized;
                UpdateIntervalChanged?.Invoke(updateIntervalSeconds);
            }
        }
    }
}
