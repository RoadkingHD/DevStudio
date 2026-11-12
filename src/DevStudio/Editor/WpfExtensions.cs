using System.Windows.Media;

namespace DevStudio.Editor
{
    internal static class WpfExtensions
    {
        public static Color ToMedia(this System.Drawing.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
