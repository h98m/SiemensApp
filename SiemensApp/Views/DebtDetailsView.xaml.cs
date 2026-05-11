using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;

namespace SiemensApp.Views
{
    public class PurchasedItem
    {
        public int Id { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public string Currency { get; set; }
        public double Total => Quantity * Price;

        // هذا الحقل هو اللي راح نلعب بيه بالبرمجة
        public double RemainingTotal { get; set; }

        // العرض يعتمد على المتبقي
        public string DisplayTotal => RemainingTotal.ToString("N3") + " " + (Currency == "USD" ? "$" : "د.ع");
    }

    public partial class DebtDetailsView : UserControl
    {
        private string dbPath = $"Data Source={System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SiemensData.db")}";
        private DebtThemItem currentDebtor;
        public ObservableCollection<PurchasedItem> ItemsList { get; set; } = new ObservableCollection<PurchasedItem>();
        private bool isEditMode = false;
        private int selectedItemId = 0;
        private string currentInputCurrency = "IQD";
        private string currentPaymentCurrency = "IQD";

        public DebtDetailsView(DebtThemItem debtor)
        {
            InitializeComponent();
            currentDebtor = debtor;
            txtHeaderName.Text = $"حساب: {debtor.DebtorName}";
            CreateItemsTable();
            LoadPurchasedItems();
        }

        // --- الأساسيات (جداول وحسابات) ---
        private void CreateItemsTable()
        {
            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS PurchasedItems (Id INTEGER PRIMARY KEY AUTOINCREMENT, DebtorId INTEGER, ItemName TEXT, Quantity INTEGER, Price REAL, Currency TEXT, CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP);
                    CREATE TABLE IF NOT EXISTS Payments (Id INTEGER PRIMARY KEY AUTOINCREMENT, DebtorId INTEGER, Amount REAL, Currency TEXT, PaymentDate DATETIME DEFAULT CURRENT_TIMESTAMP);
                    CREATE TABLE IF NOT EXISTS SystemLogs (Id INTEGER PRIMARY KEY AUTOINCREMENT, DebtorId INTEGER, ActionType TEXT, Details TEXT, LogDate DATETIME DEFAULT CURRENT_TIMESTAMP);";
                cmd.ExecuteNonQuery();
            }
        }

        private void LoadPurchasedItems()
        {
            ItemsList.Clear();
            double totalPaidIQD = 0, totalPaidUSD = 0;
            double netIQD = 0, netUSD = 0;

            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();

                // 1. جلب مجموع التسديدات أولاً
                var cmdPaid = connection.CreateCommand();
                cmdPaid.CommandText = "SELECT Amount, Currency FROM Payments WHERE DebtorId = @id";
                cmdPaid.Parameters.AddWithValue("@id", currentDebtor.Id);
                using (var reader = cmdPaid.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.GetString(1) == "USD") totalPaidUSD += reader.GetDouble(0);
                        else totalPaidIQD += reader.GetDouble(0);
                    }
                }

                // 2. جلب المواد وتوزيع الخصم عليها
                var cmdItems = connection.CreateCommand();
                cmdItems.CommandText = "SELECT Id, ItemName, Quantity, Price, Currency FROM PurchasedItems WHERE DebtorId = @id ORDER BY Id ASC";
                cmdItems.Parameters.AddWithValue("@id", currentDebtor.Id);

                using (var reader = cmdItems.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new PurchasedItem
                        {
                            Id = reader.GetInt32(0),
                            ItemName = reader.GetString(1),
                            Quantity = reader.GetInt32(2),
                            Price = reader.GetDouble(3),
                            Currency = reader.GetString(4)
                        };

                        double itemTotal = item.Total;

                        // منطق الخصم الذكي:
                        if (item.Currency == "USD")
                        {
                            double deduction = Math.Min(itemTotal, totalPaidUSD);
                            item.RemainingTotal = itemTotal - deduction;
                            totalPaidUSD -= deduction; // نطرح من "خزنة" المسدد
                            netUSD += item.RemainingTotal;
                        }
                        else
                        {
                            double deduction = Math.Min(itemTotal, totalPaidIQD);
                            item.RemainingTotal = itemTotal - deduction;
                            totalPaidIQD -= deduction;
                            netIQD += item.RemainingTotal;
                        }

                        ItemsList.Add(item);
                    }
                }
            }

            dgvItems.ItemsSource = null;
            dgvItems.ItemsSource = ItemsList;

            // تحديث الملصقات العلوية بالصافي الفعلي
            lblDebtIQD.Text = $"{netIQD:N3} د.ع";
            lblDebtUSD.Text = $"$ {netUSD:N3}";
        }

        // --- منطق التسديد المنبثق ---
        private void OpenPayment_Click(object sender, RoutedEventArgs e) { txtPaymentDebtorName.Text = currentDebtor.DebtorName; txtPaymentAmount.Clear(); gridPaymentOverlay.Visibility = Visibility.Visible; }
        private void ClosePayment_Click(object sender, RoutedEventArgs e) => gridPaymentOverlay.Visibility = Visibility.Collapsed;
        private void PaymentCurrency_Changed(object sender, RoutedEventArgs e)
        {
            // 1. تحديد العملة الحالية بناءً على حالة الزر
            // (افترضنا أن currentPaymentCurrency تتحدث هنا)
            if (btnPaymentCurrencyToggle.IsChecked == true)
            {
                currentPaymentCurrency = "USD"; // أو العملة الثانية
                txtPayCurrencyFlag.Text = "🇺🇸";
                txtPayCurrencyName.Text = "دولار أمريكي";
            }
            else
            {
                currentPaymentCurrency = "IQD";
                txtPayCurrencyFlag.Text = "🇮🇶";
                txtPayCurrencyName.Text = "دينار عراقي";
            }

            // 2. الحل هنا حجي: استدعاء التحديث فوراً
            UpdateWordsOnly(txtPaymentAmount.Text);
        }

        private void ConfirmPayment_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(txtPaymentAmount.Text, out double amount) || amount <= 0) return;
            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                using (var trans = connection.BeginTransaction())
                {
                    var cmd = connection.CreateCommand(); cmd.Transaction = trans;
                    cmd.CommandText = "INSERT INTO Payments (DebtorId, Amount, Currency) VALUES (@did, @amt, @cur)";
                    cmd.Parameters.AddWithValue("@did", currentDebtor.Id); cmd.Parameters.AddWithValue("@amt", amount); cmd.Parameters.AddWithValue("@cur", currentPaymentCurrency);
                    cmd.ExecuteNonQuery();

                    string sym = (currentPaymentCurrency == "IQD") ? "د.ع" : "$";
                    var log = connection.CreateCommand(); log.Transaction = trans;
                    log.CommandText = "INSERT INTO SystemLogs (DebtorId, ActionType, Details) VALUES (@did, 'تسديد', @det)";
                    log.Parameters.AddWithValue("@did", currentDebtor.Id);
                    // داخل ConfirmPayment_Click و AddItem_Click
                    // غير {amount:N0} إلى {amount:N3}
                    log.Parameters.AddWithValue("@det", $"سدد السيد {currentDebtor.DebtorName} مبلغ قدره {amount:N3} {sym}"); log.ExecuteNonQuery();
                    trans.Commit();
                }
            }
            gridPaymentOverlay.Visibility = Visibility.Collapsed; LoadPurchasedItems(); MessageBox.Show("تم التسديد بنجاح.");
        }

        // --- منطق المشتريات والحذف ---
        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInputProduct.Text) || !double.TryParse(txtInputPrice.Text, out double p)) return;
            bool wasEdit = isEditMode;
            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                using (var trans = connection.BeginTransaction())
                {
                    var cmd = connection.CreateCommand(); cmd.Transaction = trans;
                    if (wasEdit)
                    {
                        cmd.CommandText = "UPDATE PurchasedItems SET ItemName=@n, Quantity=@q, Price=@p, Currency=@c WHERE Id=@id";
                        cmd.Parameters.AddWithValue("@id", selectedItemId);
                    }
                    else
                    {
                        cmd.CommandText = "INSERT INTO PurchasedItems (DebtorId, ItemName, Quantity, Price, Currency) VALUES (@did, @n, @q, @p, @c)";
                        cmd.Parameters.AddWithValue("@did", currentDebtor.Id);
                    }
                    cmd.Parameters.AddWithValue("@n", txtInputProduct.Text);
                    cmd.Parameters.AddWithValue("@q", int.TryParse(txtInputQty.Text, out int q) ? q : 1);
                    cmd.Parameters.AddWithValue("@p", p); cmd.Parameters.AddWithValue("@c", currentInputCurrency);
                    cmd.ExecuteNonQuery();

                    string sym = (currentInputCurrency == "IQD") ? "د.ع" : "$";
                    var log = connection.CreateCommand(); log.Transaction = trans;
                    log.CommandText = "INSERT INTO SystemLogs (DebtorId, ActionType, Details) VALUES (@did, @type, @det)";
                    log.Parameters.AddWithValue("@did", currentDebtor.Id);
                    log.Parameters.AddWithValue("@type", wasEdit ? "تعديل" : "إضافة");
                    log.Parameters.AddWithValue("@det", $"{(wasEdit ? "تعديل" : "إضافة")} مادة: {txtInputProduct.Text} بسعر {p:N0} {sym}");
                    log.ExecuteNonQuery();
                    trans.Commit();
                }
            }
            isEditMode = false; txtInputProduct.Clear(); txtInputPrice.Clear(); LoadPurchasedItems();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.DataContext as PurchasedItem;
            if (item == null || MessageBox.Show($"متأكد من حذف {item.ItemName}؟", "تنبيه", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            using (var conn = new SqliteConnection(dbPath))
            {
                conn.Open(); using (var trans = conn.BeginTransaction())
                {
                    var cmd = conn.CreateCommand(); cmd.Transaction = trans;
                    cmd.CommandText = "DELETE FROM PurchasedItems WHERE Id = @id"; cmd.Parameters.AddWithValue("@id", item.Id); cmd.ExecuteNonQuery();
                    var log = conn.CreateCommand(); log.Transaction = trans;
                    log.CommandText = "INSERT INTO SystemLogs (DebtorId, ActionType, Details) VALUES (@did, 'حذف', @det)";
                    log.Parameters.AddWithValue("@did", currentDebtor.Id); log.Parameters.AddWithValue("@det", $"حذف مادة: {item.ItemName} بسعر {item.Price:N0}");
                    log.ExecuteNonQuery(); trans.Commit();
                }
            }
            LoadPurchasedItems();
        }

        // --- السجل والتنسيق ---
        private void ViewLogs_Click(object sender, RoutedEventArgs e) { LoadLogsFromDatabase(); gridLogsOverlay.Visibility = Visibility.Visible; }
        private void CloseLogs_Click(object sender, RoutedEventArgs e) => gridLogsOverlay.Visibility = Visibility.Collapsed;
        private void LoadLogsFromDatabase()
        {
            var logs = new List<object>();
            using (var conn = new SqliteConnection(dbPath))
            {
                conn.Open(); var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT ActionType, Details, datetime(LogDate, 'localtime') FROM SystemLogs WHERE DebtorId = @id ORDER BY LogDate DESC";
                cmd.Parameters.AddWithValue("@id", currentDebtor.Id);
                using (var r = cmd.ExecuteReader()) { while (r.Read()) logs.Add(new { ActionType = r.GetString(0), Details = r.GetString(1), LogDate = r.GetString(2) }); }
            }
            dgvLogs.ItemsSource = logs;
        }
        private void txtPaymentAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox tb) || lblPaymentAmountInWords == null) return;

            // 1. إذا الحقل فارغ حجي، نصفر الليبل ونطلع
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                lblPaymentAmountInWords.Text = "";
                return;
            }

            // 2. صمام أمان: إذا المستخدم ديطبع بوينت حالياً أو أصفار بعد البوينت، 
            // نعوفه يكمل كتابة وما نلمس الـ TextBox حتى لا نخربط عليه المراتب
            if (tb.Text.EndsWith(".") || (tb.Text.Contains(".") && tb.Text.EndsWith("0")))
            {
                // نحدث التفقيط فقط بدون ما نغير النص داخل الـ TextBox
                UpdateWordsOnly(tb.Text);
                return;
            }

            try
            {
                // تنظيف النص من الفوارز للقراءة
                string cleanText = tb.Text.Replace(",", "");

                // استخدام InvariantCulture ضروري جداً حجي لضمان قراءة النقطة صح بكل لغات الويندوز
                if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                {
                    // 3. تحديث التفقيط العربي (استدعاء الماستر)
                    // ملاحظة: دالة ConvertToFullCurrency هي أصلاً تضيف "دينار/دولار" و "فقط لا غير"
                    lblPaymentAmountInWords.Text = ConvertToFullCurrency(amount, currentPaymentCurrency == "IQD");

                    // 4. تنسيق الرقم داخل الـ TextBox (فقط إذا كان رقم صحيح لإضافة فوارص الآلاف)
                    // 4. تنسيق الرقم داخل الـ TextBox (فقط إذا كان رقم صحيح لإضافة فوارص الآلاف)
                    if (!tb.Text.Contains("."))
                    {
                        tb.TextChanged -= txtPaymentAmount_TextChanged;
                        int cursorPosition = tb.SelectionStart;
                        int oldLength = tb.Text.Length;

                        // الحل هنا حجي: غيرنا N3 إلى N0 حتى تروح الأصفار الثلاثة
                        tb.Text = amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

                        int newLength = tb.Text.Length;
                        tb.SelectionStart = Math.Max(0, cursorPosition + (newLength - oldLength));
                        tb.TextChanged += txtPaymentAmount_TextChanged;
                    }
                }
                else
                {
                    lblPaymentAmountInWords.Text = "رقم غير صالح";
                }
            }
            catch { }
        }

        // دالة مساعدة لتحديث التفقيط أثناء طباعة البوينتات بدون لمس الـ TextBox
        private void UpdateWordsOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                lblPaymentAmountInWords.Text = "";
                return;
            }

            string cleanText = text.Replace(",", "");
            if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
            {
                // هنا نمرر حالة العملة الحالية للدالة
                bool isIQD = (currentPaymentCurrency == "IQD");
                lblPaymentAmountInWords.Text = ConvertToFullCurrency(amount, isIQD);
            }
        }
        private void Currency_Changed(object sender, RoutedEventArgs e)
        {
            bool isUsd = btnCurrencyToggle.IsChecked == true;
            currentInputCurrency = isUsd ? "USD" : "IQD";
            txtCurrencyFlag.Text = isUsd ? "🇺🇸" : "🇮🇶";
            txtCurrencyName.Text = isUsd ? "دولار" : "د.عراقي";
            UpdatePriceInWords();
        }
        private void txtInputPrice_TextChanged(object sender, TextChangedEventArgs e) => UpdatePriceInWords();
        private void UpdatePriceInWords()
        {
            if (lblPriceInWords == null) return;
            if (decimal.TryParse(txtInputPrice.Text, out decimal price))
                // هذي الدالة هي اللي تعالج الكسور (البوينتات) وتضيف العملة والـ "فقط لا غير" تلقائياً
                lblPriceInWords.Text = ConvertToFullCurrency(price, currentInputCurrency == "IQD");
            else lblPriceInWords.Text = "صفر";
        }
        public string ConvertToFullCurrency(decimal number, bool isDinar)
        {
            if (number == 0) return "صفر " + (isDinar ? "دينار عراقي" : "دولار أمريكي");
            // فصل الصحيح عن الكسر
            long integralPart = (long)Math.Truncate(number);
            decimal fraction = number - integralPart;
            // للـ IQD نضرب بـ 1000 وللدولار بـ 100
            int decimalPart = isDinar ? (int)(fraction * 1000) : (int)(fraction * 100);
            string result = "";

            // تحويل المبلغ الصحيح
            if (integralPart > 0)
            {
                result = ToArabicWords(integralPart) + (isDinar ? " دينار عراقي" : " دولار أمريكي");
            }

            // تحويل البوينتات (الكسور)
            if (decimalPart > 0)
            {
                if (result != "") result += " و ";
                result += ToArabicWords(decimalPart) + (isDinar ? " فلس" : " سنت");
            }

            return result + " فقط لا غير";
        }
        public void Edit_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.DataContext as PurchasedItem;
            if (item == null) return;
            isEditMode = true; selectedItemId = item.Id;
            txtInputProduct.Text = item.ItemName; txtInputQty.Text = item.Quantity.ToString(); txtInputPrice.Text = item.Price.ToString();
            btnCurrencyToggle.IsChecked = (item.Currency == "USD"); Currency_Changed(null, null);
        }

        // --- دالة التفقيط الخاصة بك (تأكد من وجودها) ---
        public string ToArabicWords(long number)
        {
            if (number == 0) return "صفر";
            if (number < 0) return "سالب " + ToArabicWords(Math.Abs(number));

            string words = "";

            if ((number / 1000000000) >= 1)
            {
                long billions = number / 1000000000;
                words += (billions == 1 ? "مليار" : (billions == 2 ? "ملياران" : ToArabicWords(billions) + " مليارات")) + " ";
                number %= 1000000000;
                if (number > 0) words += " و ";
            }

            if ((number / 1000000) >= 1)
            {
                long millions = number / 1000000;
                words += (millions == 1 ? "مليون" : (millions == 2 ? "مليونان" : ToArabicWords(millions) + " ملايين")) + " ";
                number %= 1000000;
                if (number > 0) words += " و ";
            }

            if ((number / 1000) >= 1)
            {
                long thousands = number / 1000;
                if (thousands == 1) words += "ألف";
                else if (thousands == 2) words += "ألفان";
                else if (thousands >= 3 && thousands <= 10) words += ToArabicWords(thousands) + " آلاف";
                else words += ToArabicWords(thousands) + " ألف";

                number %= 1000;
                if (number > 0) words += " و ";
            }

            if ((number / 100) >= 1)
            {
                string[] hundreds = { "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة" };
                words += hundreds[number / 100];
                number %= 100;
                if (number > 0) words += " و ";
            }

            if (number > 0)
            {
                string[] ones = { "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر" };
                string[] tens = { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };

                if (number < 20) words += ones[number];
                else
                {
                    words += ones[number % 10];
                    if (number % 10 > 0) words += " و ";
                    words += tens[number / 10];
                }
            }

            return words.Trim();
        }
    }
}