using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SiemensApp.Views
{
    public class DebtThemItem
    {
        public int Id { get; set; }
        public string DebtorName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public double TotalAmount { get; set; }
        public string Currency { get; set; } = "IQD";
        public string Notes { get; set; } = "";

        public string DisplayAmount => TotalAmount.ToString("N0") + " " + (Currency == "USD" ? "$" : "د.ع");
    }

    public partial class DebtsThemView : UserControl
    {
        private static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private string dbPath = $"Data Source={System.IO.Path.Combine(currentDirectory, "SiemensData.db")}";
        private bool isEditMode = false;
        private int selectedDebtId = 0;
        private string selectedCurrency = "IQD";
        private DebtThemItem selectedDebtForPay;
        public ObservableCollection<DebtThemItem> DebtList { get; set; } = new ObservableCollection<DebtThemItem>();

        public DebtsThemView()
        {
            InitializeComponent();
            CreateDebtsTable();
            LoadDebtsData();
        }

        private void CreateDebtsTable()
        {
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS DebtsThem (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DebtorName TEXT NOT NULL,
                        PhoneNumber TEXT,
                        TotalAmount REAL DEFAULT 0,
                        Currency TEXT DEFAULT 'IQD',
                        Notes TEXT);";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { MessageBox.Show("خطأ في إنشاء الجدول: " + ex.Message); }
        }
        private void dgvDebts_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // نجلب السطر اللي ضغط عليه المستخدم
            var selectedDebt = dgvDebts.SelectedItem as DebtThemItem;

            if (selectedDebt != null)
            {
                // 1. إنشاء نسخة من الصفحة الثانية وتمرير بيانات الدائن المختار لها
                var detailsView = new DebtDetailsView(selectedDebt);

                // 2. الوصول للـ Window الرئيسية لتبديل المحتوى
                var mainWindow = Window.GetWindow(this) as MainWindow;

                if (mainWindow != null)
                {
                    // استخدمنا الاسم الجديد اللي موجود بـ MainWindow.xaml مالتك
                    mainWindow.MainContentFrame.Content = detailsView;
                }
            }
        }
        private void UpdateSummaryTotals()
        {
            if (DebtList == null) return;
            double totalIQD = DebtList.Where(x => x.Currency == "د.ع" || x.Currency == "IQD").Sum(x => x.TotalAmount);
            double totalUSD = DebtList.Where(x => x.Currency == "$" || x.Currency == "USD").Sum(x => x.TotalAmount);

        }

        private void LoadDebtsData(string filter = "")
        {
            DebtList.Clear();
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT Id, DebtorName, PhoneNumber, TotalAmount, Currency, Notes FROM DebtsThem WHERE DebtorName LIKE @p OR PhoneNumber LIKE @p";
                    cmd.Parameters.AddWithValue("@p", "%" + filter + "%");

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DebtList.Add(new DebtThemItem
                            {
                                Id = reader.GetInt32(0),
                                DebtorName = reader.GetString(1),
                                PhoneNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                TotalAmount = reader.GetDouble(3),
                                Currency = reader.GetString(4),
                                Notes = reader.IsDBNull(5) ? "" : reader.GetString(5)
                            });
                        }
                    }
                }
                dgvDebts.ItemsSource = null;
                dgvDebts.ItemsSource = DebtList;
                UpdateSummaryTotals();
            }
            catch (Exception ex) { MessageBox.Show("خطأ في جلب البيانات: " + ex.Message); }
        }

        // --- أزرار العملة ---
        private void Currency_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.Tag == null) return;

            selectedCurrency = btn.Tag.ToString();
            UpdateCurrencyUI();
            UpdateAmountWord();
        }

        private void UpdateCurrencyUI()
        {
            // تأكد من وجود الأزرار في الـ XAML بهذه الأسماء
            if (btnCurrencyIQD == null || btnCurrencyUSD == null) return;

            if (selectedCurrency == "IQD")
            {
                btnCurrencyIQD.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                btnCurrencyIQD.Foreground = Brushes.White;
                btnCurrencyUSD.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                btnCurrencyUSD.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            }
            else
            {
                btnCurrencyUSD.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
                btnCurrencyUSD.Foreground = Brushes.White;
                btnCurrencyIQD.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                btnCurrencyIQD.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            }
        }

        // --- التفقيط ---
        private void txtInitialAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAmountWord();
        }

        private void UpdateAmountWord()
        {
            if (lblAmountWord == null || txtInitialAmount == null) return;

            if (long.TryParse(txtInitialAmount.Text, out long amt))
            {
                // استدعاء خوارزمية التفقيط
                string wordAmount = ToWordArabic.Convert(amt);
                string currencyName = selectedCurrency == "IQD" ? "دينار عراقي" : "دولار أمريكي";

                lblAmountWord.Text = $"فقط {wordAmount} {currencyName} لا غير";
            }
            else if (string.IsNullOrEmpty(txtInitialAmount.Text))
            {
                lblAmountWord.Text = "المبلغ كتابةً: صفر";
            }
            else
            {
                lblAmountWord.Text = "يرجى إدخال رقم صحيح بدون فواصل";
            }
        }

        private void SaveDebt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtDebtorName.Text) || string.IsNullOrEmpty(txtInitialAmount.Text))
            {
                MessageBox.Show("حجي املي الحقول أولاً!");
                return;
            }

            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    if (isEditMode)
                    {
                        cmd.CommandText = "UPDATE DebtsThem SET DebtorName=@n, PhoneNumber=@p, TotalAmount=@a, Currency=@c, Notes=@note WHERE Id=@id";
                        cmd.Parameters.AddWithValue("@id", selectedDebtId);
                    }
                    else
                    {
                        cmd.CommandText = "INSERT INTO DebtsThem (DebtorName, PhoneNumber, TotalAmount, Currency, Notes) VALUES (@n,@p,@a,@c,@note)";
                    }
                    cmd.Parameters.AddWithValue("@n", txtDebtorName.Text);
                    cmd.Parameters.AddWithValue("@p", txtPhoneNumber.Text);
                    cmd.Parameters.AddWithValue("@a", double.TryParse(txtInitialAmount.Text, out double amt) ? amt : 0);
                    cmd.Parameters.AddWithValue("@c", selectedCurrency);
                    cmd.Parameters.AddWithValue("@note", txtDebtNote.Text);
                    cmd.ExecuteNonQuery();
                }
                AddDebtCard.Visibility = Visibility.Collapsed;
                LoadDebtsData();
            }
            catch (Exception ex) { MessageBox.Show("خطأ في الحفظ: " + ex.Message); }
        }

        public void BtnEditDebt_Click(object sender, RoutedEventArgs e)
        {
            var selected = (sender as Button).DataContext as DebtThemItem;
            if (selected == null) return;

            isEditMode = true;
            selectedDebtId = selected.Id;
            txtDebtorName.Text = selected.DebtorName;
            txtPhoneNumber.Text = selected.PhoneNumber;
            txtInitialAmount.Text = selected.TotalAmount.ToString("F0");
            txtDebtNote.Text = selected.Notes;

            selectedCurrency = (selected.Currency == "$" || selected.Currency == "USD") ? "USD" : "IQD";

            UpdateCurrencyUI();
            UpdateAmountWord();
            AddDebtCard.Visibility = Visibility.Visible;
        }

        public void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            selectedDebtForPay = (sender as Button).DataContext as DebtThemItem;
            if (selectedDebtForPay == null) return;
            txtPayTargetName.Text = $"الدائن: {selectedDebtForPay.DebtorName}";
            txtPayCurrentDebt.Text = $"المبلغ المطلوب لهُ: {selectedDebtForPay.DisplayAmount}";
            txtPayAmountInput.Text = "";
            PayDebtCard.Visibility = Visibility.Visible;
            txtPayAmountInput.Focus();
        }

        private void ConfirmPay_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(txtPayAmountInput.Text, out double payAmount) && payAmount > 0)
            {
                if (payAmount > selectedDebtForPay.TotalAmount)
                {
                    MessageBox.Show("حجي، المبلغ المدفوع أكبر من المطلوب!");
                    return;
                }
                try
                {
                    using (var connection = new SqliteConnection(dbPath))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE DebtsThem SET TotalAmount = TotalAmount - @amt WHERE Id = @id";
                        cmd.Parameters.AddWithValue("@amt", payAmount);
                        cmd.Parameters.AddWithValue("@id", selectedDebtForPay.Id);
                        cmd.ExecuteNonQuery();
                    }
                    PayDebtCard.Visibility = Visibility.Collapsed;
                    LoadDebtsData();
                    MessageBox.Show("تم دفع المبلغ بنجاح.");
                }
                catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message); }
            }
        }

        public void BtnDeleteDebt_Click(object sender, RoutedEventArgs e)
        {
            var selected = (sender as Button).DataContext as DebtThemItem;
            if (selected == null) return;
            if (MessageBox.Show($"حجي متأكد تريد تحذف سجل الدائن '{selected.DebtorName}'؟", "تأكيد", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM DebtsThem WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", selected.Id);
                    cmd.ExecuteNonQuery();
                }
                LoadDebtsData();
            }
        }
        public static class ToWordArabic
        {
            private static string[] ones = { "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر" };
            private static string[] tens = { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
            private static string[] hundreds = { "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة" };

            public static string Convert(long number)
            {
                if (number == 0) return "صفر";
                if (number < 0) return "سالب " + Convert(Math.Abs(number));

                string words = "";

                if ((number / 1000000000) > 0)
                {
                    words += (number / 1000000000 == 1 ? "مليار" : (number / 1000000000 == 2 ? "ملياران" : Convert(number / 1000000000) + " مليارات")) + " و ";
                    number %= 1000000000;
                }

                if ((number / 1000000) > 0)
                {
                    words += (number / 1000000 == 1 ? "مليون" : (number / 1000000 == 2 ? "مليونان" : Convert(number / 1000000) + " ملايين")) + " و ";
                    number %= 1000000;
                }

                if ((number / 1000) > 0)
                {
                    words += (number / 1000 == 1 ? "ألف" : (number / 1000 == 2 ? "ألفان" : (number / 1000 >= 3 && number / 1000 <= 10 ? Convert(number / 1000) + " آلاف" : Convert(number / 1000) + " ألف"))) + " و ";
                    number %= 1000;
                }

                if ((number / 100) > 0)
                {
                    words += hundreds[number / 100] + " و ";
                    number %= 100;
                }

                if (number > 0)
                {
                    if (number < 20) words += ones[number];
                    else
                    {
                        words += ones[number % 10];
                        if (number % 10 > 0) words += " و ";
                        words += tens[number / 10];
                    }
                }

                words = words.TrimEnd(' ', 'و');
                return words.Replace("  ", " ").Trim();
            }
        }
        private void ShowAddDebt_Click(object sender, RoutedEventArgs e)
        {
            isEditMode = false;
            ClearDebtInputs();
            selectedCurrency = "IQD";
            UpdateCurrencyUI();
            UpdateAmountWord();
            AddDebtCard.Visibility = Visibility.Visible;
        }

        private void HideAddDebt_Click(object sender, RoutedEventArgs e) => AddDebtCard.Visibility = Visibility.Collapsed;
        private void ClosePayCard_Click(object sender, RoutedEventArgs e) => PayDebtCard.Visibility = Visibility.Collapsed;
        private void txtSearchDebt_TextChanged(object sender, TextChangedEventArgs e) => LoadDebtsData(txtSearchDebt.Text);
        private void ClearDebtInputs() { txtDebtorName.Clear(); txtPhoneNumber.Clear(); txtInitialAmount.Clear(); txtDebtNote.Clear(); }
    }
}