using System;

namespace SubmarineLDJam.Presentation
{
    public static class TimeFormatterHelper
    {
        public static string FormatTime(float time)
        {
            var timeSpan = TimeSpan.FromSeconds(time);
            var format = @"mm\:ss\.ff";
            return $"<mspace=0.7em>{timeSpan.ToString(format)}</mspace>";
        }
    }
}
