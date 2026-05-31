using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SecureNotesWin.Helpers
{
    public static class BookIconPicker
    {
        public static readonly string[] Icons =
        {
            "📔", "📓", "📖", "📕", "📗", "📘", "📙", "📚", "📒", "🗒️",
            "✏️", "🖊️", "📝", "💼", "🎒", "🧳", "🗂️", "📁", "📂", "🔖",
            "⭐", "🌟", "💡", "🔥", "❤️", "🎯", "🏠", "✈️", "🌍", "🎵",
            "🎨", "⚽", "🍎", "☕", "🌙", "☀️", "🌈", "🐾", "🌿", "🔐"
        };

        public static void Populate(WrapPanel panel, string selectedIcon, RoutedEventHandler onSelected)
        {
            panel.Children.Clear();
            foreach (var icon in Icons)
            {
                var btn = new Button
                {
                    Content = icon,
                    Tag = icon,
                    Width = 40,
                    Height = 40,
                    Margin = new Thickness(3),
                    FontSize = 20,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = icon == selectedIcon
                        ? new SolidColorBrush(Color.FromArgb(64, 245, 158, 11))
                        : Brushes.Gray,
                    BorderThickness = new Thickness(icon == selectedIcon ? 2 : 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11))
                };
                btn.Click += onSelected;
                panel.Children.Add(btn);
            }
        }
    }
}
