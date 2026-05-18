using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Data.Sqlite;

namespace SiemensApp.Helpers
{
    /// <summary>
    /// دوال مشتركة للواجهة (Toast، رقم الفاتورة، وغيرها)
    /// </summary>
    public static class UiHelper
    {
        /// <summary>
        /// عرض رسالة Toast مؤقتة على الشاشة
        /// </summary>
        public static void ShowToast(string message, string colorHex = "#EF4444", int durationSeconds = 4)
        {
            Window toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new Border
                {
                    Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(colorHex)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(25, 12, 25, 12),
                    Effect = new DropShadowEffect { BlurRadius = 15, Opacity = 0.2 },
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = Brushes.White,
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    }
                }
            };
            toast.Show();
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };
            timer.Tick += (s, e) => { toast.Close(); timer.Stop(); };
            timer.Start();
        }

        /// <summary>
        /// جلب رقم الفاتورة التالي من قاعدة البيانات
        /// </summary>
        public static string GetSimpleNextNumber(string dbPath)
        {
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT IFNULL(MAX(CAST(InvoiceNumber AS INTEGER)), 0) FROM Invoices";
                    var result = cmd.ExecuteScalar();
                    int lastNum = Convert.ToInt32(result);
                    return (lastNum + 1).ToString();
                }
            }
            catch { return "1"; }
        }
    }
}
