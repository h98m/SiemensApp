using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Xceed.Words.NET;
using System.IO;
using Xceed.Document.NET; // ضروري جداً للتعرف على Cell و Row و Alignment
using System.Diagnostics;
using SiemensApp.Helpers;
namespace SiemensApp.Views
{
    // الكلاس الخاص ببيانات المديونين
    public class DebtItem
    {
        public int Id { get; set; }
        public string DebtorName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "IQD";
        public string Notes { get; set; } = "";

        // عرض المبالغ بدون أصفار .00 للدينار وبكسور للدولار
        public string DisplayAmount
        {
            get
            {
                if (Currency == "$" || Currency == "USD" || Currency == "دولار أمريكي")
                    return TotalAmount.ToString("N2") + " $";

                return TotalAmount.ToString("N0") + " د.ع";
            }
        }
    }

    public partial class DebtsMeView : UserControl
    {
        private static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private string dbPath = $"Data Source={Path.Combine(currentDirectory, "SiemensData.db")}";
        private bool isEditMode = false;
        private int selectedDebtId = 0;
        private DebtItem selectedDebtForPay;
        public ObservableCollection<DebtItem> DebtList { get; set; } = new ObservableCollection<DebtItem>();

        // متغير تتبع العملة الحالية (دينار = true)
        private bool isIraqiDinar = true;

        public DebtsMeView()
        {
            InitializeComponent();
            CreateDebtsTable();
            LoadDebtsData();
        }

        #region "Database & Summary"
        private void CreateDebtsTable()
        {
            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                try
                {
                    var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE DebtLogs ADD COLUMN LogCurrency TEXT;";
                    alterCmd.ExecuteNonQuery();
                }
                catch
                {
                    // إذا العمود موجود أصلاً راح يطلع خطأ، فنهمشه
                }
                var cmd = connection.CreateCommand();
                // أضف حقل LogCurrency لكود إنشاء الجدول
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS DebtLogs (
    LogId INTEGER PRIMARY KEY AUTOINCREMENT,
    DebtorId INTEGER,
    ActionType TEXT, 
    AmountChanged REAL,
    LogCurrency TEXT, -- ⭐ هذا الحقل الجديد
    LogDate TEXT,
    Details TEXT,
    FOREIGN KEY(DebtorId) REFERENCES DebtsMe(Id) ON DELETE CASCADE);";
                cmd.ExecuteNonQuery();
            }
        }
        private void BtnOpenProfile_Click(object sender, RoutedEventArgs e)
        {
            var selected = (sender as Button).DataContext as DebtItem;
            if (selected == null) return;

            selectedDebtForPay = selected;
            txtProfileTitle.Text = selected.DebtorName;
            lblProfileTotal.Text = selected.DisplayAmount;

            // ⭐ لازم هذي ممسوحة منها التعليق حتى تشتغل
            LoadLogs(selected.Id);

            UserProfilePage.Visibility = Visibility.Visible;
        }
        // 1. زر الرجوع من الملف الشخصي للقائمة الرئيسية
        private void CloseProfile_Click(object sender, RoutedEventArgs e)
        {
            UserProfilePage.Visibility = Visibility.Collapsed;
            LoadDebtsData(); // تحديث القائمة الرئيسية
        }

        // 2. زر تسديد من داخل الملف الشخصي
        private void BtnPayFromProfile_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDebtForPay == null) return;

            // تجهيز بيانات الوصل
            txtReceiptNo.Text = DateTime.Now.Ticks.ToString().Substring(10);
            txtReceiptDate.Text = DateTime.Now.ToString("yyyy/MM/dd");
            txtReceiptTime.Text = DateTime.Now.ToString("HH:mm");

            // عرض الرصيد الحالي قبل التسديد
            txtPrevBalance.Text = selectedDebtForPay.DisplayAmount;
            txtCurrentBalance.Text = selectedDebtForPay.DisplayAmount;

            txtPayAmountInput.Clear();
            txtPayDisplay.Text = "";

            // إظهار كارت التسديد (القديم اللي برمجناه)
            PayDebtCard.Visibility = Visibility.Visible;
        }

        // 3. زر تعديل البيانات من داخل الملف
        private void BtnEditFromProfile_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDebtForPay == null) return;

            isEditMode = true;
            selectedDebtId = selectedDebtForPay.Id;

            // تعبئة الحقول ببيانات الشخص الحالية
            txtDebtorName.Text = selectedDebtForPay.DebtorName;
            txtPhoneNumber.Text = selectedDebtForPay.PhoneNumber;
            txtDebtNote.Text = selectedDebtForPay.Notes;

            // ضبط العملة
            isIraqiDinar = !selectedDebtForPay.Currency.Contains("$");
            btnCurrencyToggle.Content = isIraqiDinar ? "د.ع" : "$";

            // عرض المبلغ (بدون فوارص للتعديل السهل)
            txtInitialAmount.Text = selectedDebtForPay.TotalAmount.ToString("F0");

            // إظهار كارت الإضافة/التعديل
            AddDebtCard.Visibility = Visibility.Visible;
        }

        // 4. زر حذف الحساب من داخل الملف
        private void BtnDeleteFromProfile_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDebtForPay == null) return;

            if (MessageBox.Show($"حجي متأكد تريد تحذف حساب '{selectedDebtForPay.DebtorName}' وكل سجلاته؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM DebtsMe WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", selectedDebtForPay.Id);
                    cmd.ExecuteNonQuery();
                }
                UserProfilePage.Visibility = Visibility.Collapsed;
                LoadDebtsData();
            }
        }
        private void SaveActionLog(int debtorId, string type, decimal amount, string currency, string details)
        {
            if (debtorId <= 0) return;

            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();

                    // ⭐ التصحيح: اسم الجدول لازم يكون DebtsMe مثل ما عرفته أنت
                    var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(1) FROM DebtsMe WHERE Id = @id";
                    checkCmd.Parameters.AddWithValue("@id", debtorId);

                    long exists = (long)checkCmd.ExecuteScalar();
                    if (exists == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"خطأ: المديون رقم {debtorId} غير موجود.");
                        return;
                    }

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"INSERT INTO DebtLogs (DebtorId, ActionType, AmountChanged, LogCurrency, Details, LogDate) 
                               VALUES (@id, @t, @a, @curr, @det, @d)";

                    cmd.Parameters.AddWithValue("@id", debtorId);
                    cmd.Parameters.AddWithValue("@t", type);
                    cmd.Parameters.AddWithValue("@a", amount);
                    cmd.Parameters.AddWithValue("@curr", currency);
                    cmd.Parameters.AddWithValue("@det", details ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("خلل في حفظ السجل: " + ex.Message);
            }
        }

        private void UpdateSummaryTotals()
        {
            if (DebtList == null) return;
            decimal totalIQD = DebtList.Where(x => x.Currency == "د.ع" || x.Currency == "IQD").Sum(x => x.TotalAmount);
            decimal totalUSD = DebtList.Where(x => x.Currency == "$" || x.Currency == "USD" || x.Currency == "دولار أمريكي").Sum(x => x.TotalAmount);

            lblTotalIQD.Text = totalIQD.ToString("N0") + " د.ع";
            lblTotalUSD.Text = totalUSD.ToString("N2") + " $";
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
                    cmd.CommandText = "SELECT Id, DebtorName, PhoneNumber, TotalAmount, Currency, Notes FROM DebtsMe WHERE DebtorName LIKE @p OR PhoneNumber LIKE @p";
                    cmd.Parameters.AddWithValue("@p", "%" + filter + "%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DebtList.Add(new DebtItem
                            {
                                Id = reader.GetInt32(0),
                                DebtorName = reader.GetString(1),
                                PhoneNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                TotalAmount = reader.GetDecimal(3),
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
            catch (Exception ex) { MessageBox.Show("خطأ بالتحميل: " + ex.Message); }
        }
        #endregion

        #region "Actions (Save, Edit, Delete)"
        

        // لازم تكون الدالة بنفس الاسم الموجود بالـ XAML وبالضبط هكذا:
        private void btnCurrencyToggle_Click(object sender, RoutedEventArgs e)
        {
            // تغيير حالة العملة
            isIraqiDinar = !isIraqiDinar;

            // تحديث النص الظاهر على الزر
            if (btnCurrencyToggle != null)
            {
                btnCurrencyToggle.Content = isIraqiDinar ? "د.ع" : "$";
            }

            // تحديث التفقيط (الكتابة بالعربي) فوراً
            UpdateAmountInWords();
        }

        private void txtInitialAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAmountInWords();
        }

        private void UpdateAmountInWords()
        {
            if (txtInitialAmount == null || lblAmountWords == null) return;

            if (decimal.TryParse(txtInitialAmount.Text, out decimal result))
            {
                lblAmountWords.Text = ToArabicWords(result, isIraqiDinar);
            }
            else
            {
                lblAmountWords.Text = "صفر " + (isIraqiDinar ? "دينار عراقي" : "دولار أمريكي");
            }
        }

        public void BtnEditDebt_Click(object sender, RoutedEventArgs e)
        {
            var selected = (sender as Button).DataContext as DebtItem;
            if (selected == null) return;
            isEditMode = true;
            selectedDebtId = selected.Id;
            txtDebtorName.Text = selected.DebtorName;
            txtPhoneNumber.Text = selected.PhoneNumber;

            isIraqiDinar = !selected.Currency.Contains("$");
            btnCurrencyToggle.Content = isIraqiDinar ? "د.ع" : "$";

            txtInitialAmount.Text = isIraqiDinar ? selected.TotalAmount.ToString("F0") : selected.TotalAmount.ToString("F2");
            txtDebtNote.Text = selected.Notes;
            AddDebtCard.Visibility = Visibility.Visible;
        }

        public void BtnDeleteDebt_Click(object sender, RoutedEventArgs e)
        {
            var selected = (sender as Button).DataContext as DebtItem;
            if (selected == null) return;
            if (MessageBox.Show($"حجي متأكد تريد تحذف '{selected.DebtorName}'؟", "تأكيد", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM DebtsMe WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", selected.Id);
                    cmd.ExecuteNonQuery();
                }
                LoadDebtsData();
            }
        }
        #endregion

        #region "Payment & PDF Logic"
        private void txtPayAmountInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (selectedDebtForPay == null) return;

            if (decimal.TryParse(txtPayAmountInput.Text, out decimal amt))
            {
                string curr = (cbPayCurrency.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "د.ع";
                bool isDinarPay = !curr.Contains("$");

                // 1. تحديث التفقيط في الواجهة (للعرض وللطباعة)
                string words = ToArabicWords(amt, isDinarPay);

                // نحدث الليبل (إذا لسه موجود) والتكست بوكس اللي راح ينسحب للطباعة
                if (lblAmountWords != null) lblAmountWords.Text = words;
                if (txtWrittenNumber != null) txtWrittenNumber.Text = words;

                // 2. حسابات سعر الصرف
                if (!decimal.TryParse(txtExchangeRate.Text, out decimal rate) || rate == 0) rate = 150000;

                decimal converted = amt;
                // إذا كان الدين بالدولار والدفع بالدينار
                if (selectedDebtForPay.Currency.Contains("$") && curr.Contains("د.ع"))
                    converted = amt / (rate / 100);
                // إذا كان الدين بالدينار والدفع بالدولار
                else if ((selectedDebtForPay.Currency == "IQD" || selectedDebtForPay.Currency == "د.ع") && curr.Contains("$"))
                    converted = amt * (rate / 100);

                // 3. حساب الرصيد المتبقي
                decimal curBal = selectedDebtForPay.TotalAmount - converted;
                if (curBal < 0) curBal = 0;

                // 4. تحديث نصوص العرض
                txtPayDisplay.Text = amt.ToString(isDinarPay ? "N0" : "N2") + " " + curr;

                string debtSign = selectedDebtForPay.Currency.Contains("$") ? "$" : "د.ع";
                txtCurrentBalance.Text = curBal.ToString(debtSign == "$" ? "N2" : "N0") + " " + debtSign;
            }
            else
            {
                // تصفير الحقول في حال مسح المبلغ
                if (lblAmountWords != null) lblAmountWords.Text = "";
                if (txtWrittenNumber != null) txtWrittenNumber.Text = "";
                txtPayDisplay.Text = "";
                txtCurrentBalance.Text = selectedDebtForPay.DisplayAmount;
            }
        }


        // --- دالة التسديد (تعديل لضمان تحديث الأرقام فوراً) ---
        private void ConfirmAndPrint_Click(object sender, RoutedEventArgs e)
        {
            // 1. التحقق من صحة المبلغ المدخل وسعر الصرف
            if (!decimal.TryParse(txtPayAmountInput.Text, out decimal inputAmount)) return;
            if (!decimal.TryParse(txtExchangeRate.Text, out decimal rate)) rate = 150000;

            // 2. جلب نص العملة المختار من القائمة وتحديد نوعها
            string payCurr = (cbPayCurrency.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "د.ع";
            bool isDinarPay = !payCurr.Contains("$"); // فحص ذكي: إذا لم يحتوي النص على $ فهو دينار

            // 3. حسابات تحويل العملة لإيجاد المبلغ المخصوم من الرصيد الأصلي
            decimal converted = inputAmount;
            // إذا كان حساب الشخص بالدولار والدفع تم بالدينار
            if (selectedDebtForPay.Currency.Contains("$") && payCurr.Contains("د.ع"))
            {
                converted = inputAmount / (rate / 100);
            }
            // إذا كان حساب الشخص بالدينار والدفع تم بالدولار
            else if ((selectedDebtForPay.Currency.Contains("د.ع") || selectedDebtForPay.Currency.Contains("IQD")) && payCurr.Contains("$"))
            {
                converted = inputAmount * (rate / 100);
            }

            // حساب الرصيد الجديد (مع التأكد أنه لا يقل عن صفر)
            decimal curBal = Math.Max(0, selectedDebtForPay.TotalAmount - converted);

            try
            {
                // 4. تحديث الرصيد الجديد في قاعدة البيانات
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "UPDATE DebtsMe SET TotalAmount = @newBal WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@newBal", curBal);
                    cmd.Parameters.AddWithValue("@id", selectedDebtForPay.Id);
                    cmd.ExecuteNonQuery();
                }

                // 5. ⭐ التحديث الفوري للكائن المعروض بالذاكرة لكي يتحدث الرقم بالشاشة فوراً
                selectedDebtForPay.TotalAmount = curBal;

                // 6. تسجيل العملية في جدول السجلات (Logs) بـ 5 متغيرات كاملة
                // نرسل payCurr (الذي يحتوي على العلامة $) لكي تفهمه دالة LoadLogs
                SaveActionLog(
                    selectedDebtForPay.Id,
                    "تسديد مبلغ",
                    inputAmount,
                    payCurr,
                    $"تسديد وصل رقم {txtReceiptNo.Text} - المبلغ: {inputAmount.ToString(isDinarPay ? "N0" : "N2")} {payCurr}"
                );

                // 7. تحديث واجهة الملف الشخصي (الرقم الكبير والجدول السفلي) والقائمة الرئيسية
                lblProfileTotal.Text = selectedDebtForPay.DisplayAmount; // تحديث الرقم الأحمر/الأخضر الكبير
                LoadLogs(selectedDebtForPay.Id); // إنعاش جدول الحركات السفلي
                LoadDebtsData(); // تحديث القائمة الرئيسية في الخلفية

                // 8. تجهيز بيانات الوصل للطباعة
                var printData = new
                {
                    ReceiptNumber = txtReceiptNo.Text,
                    Date = txtReceiptDate.Text,
                    Time = txtReceiptTime.Text,
                    Customer = selectedDebtForPay.DebtorName,
                    AmountNumber = inputAmount.ToString(isDinarPay ? "N0" : "N2") + " " + (isDinarPay ? "د.ع" : "$"),
                    WrittenNumber = ToArabicWords(inputAmount, isDinarPay), // التفقيط حسب نوع العملة
                    DebitAccount = txtDebitAccount.Text,
                    Depositedby = txtDepositedBy.Text,
                    PreBal = txtPrevBalance.Text,
                    CurBal = selectedDebtForPay.DisplayAmount,
                    Notes = txtDebtNote.Text,
                    Signature = "توقيع المحاسب"
                };

                // تنفيذ عملية الطباعة وتحويل PDF
                PrintToWord(printData);

                // 9. إغلاق نافذة التسديد والعودة لصفحة الملف
                PayDebtCard.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ أثناء الحفظ أو الطباعة: " + ex.Message);
            }
        }

        // --- دالة حفظ وتعديل البيانات (تعديل لضمان تحديث الاسم والدين فوراً) ---
        private void SaveDebt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtDebtorName.Text) || !decimal.TryParse(txtInitialAmount.Text, out decimal amt)) return;

            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();

                // 1. فحص تكرار الاسم (فقط عند الإضافة الجديدة)
                if (!isEditMode)
                {
                    var checkNameCmd = connection.CreateCommand();
                    checkNameCmd.CommandText = "SELECT COUNT(1) FROM DebtsMe WHERE DebtorName = @n";
                    checkNameCmd.Parameters.AddWithValue("@n", txtDebtorName.Text.Trim());
                    long nameExists = (long)checkNameCmd.ExecuteScalar();

                    if (nameExists > 0)
                    {
                        MessageBox.Show($"حجي، اسم '{txtDebtorName.Text}' موجود مسبقاً! ضيف اسم الأب أو اللقب.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var cmd = connection.CreateCommand();
                string selectedCurrency = isIraqiDinar ? "د.ع" : "$";

                if (isEditMode)
                {
                    cmd.CommandText = "UPDATE DebtsMe SET DebtorName=@n, PhoneNumber=@p, TotalAmount=@a, Currency=@c, Notes=@note WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", selectedDebtId);
                    cmd.Parameters.AddWithValue("@n", txtDebtorName.Text.Trim());
                    cmd.Parameters.AddWithValue("@p", txtPhoneNumber.Text);
                    cmd.Parameters.AddWithValue("@a", amt);
                    cmd.Parameters.AddWithValue("@c", selectedCurrency);
                    cmd.Parameters.AddWithValue("@note", txtDebtNote.Text);
                    cmd.ExecuteNonQuery();

                    // تحديث بيانات الكائن المفتوح حالياً في الذاكرة
                    if (selectedDebtForPay != null && selectedDebtForPay.Id == selectedDebtId)
                    {
                        selectedDebtForPay.DebtorName = txtDebtorName.Text;
                        selectedDebtForPay.TotalAmount = amt;
                        selectedDebtForPay.Currency = selectedCurrency;
                        selectedDebtForPay.PhoneNumber = txtPhoneNumber.Text;
                        selectedDebtForPay.Notes = txtDebtNote.Text;

                        txtProfileTitle.Text = selectedDebtForPay.DebtorName;
                        lblProfileTotal.Text = selectedDebtForPay.DisplayAmount;
                    }

                    // ⭐ استدعاء واحد فقط ونظيف للسجل
                    SaveActionLog(selectedDebtId, "تعديل بيانات", amt, selectedCurrency, $"تعديل الحساب: {txtDebtorName.Text}");
                }
                else
                {
                    cmd.CommandText = "INSERT INTO DebtsMe (DebtorName, PhoneNumber, TotalAmount, Currency, Notes) VALUES (@n,@p,@a,@c,@note); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@n", txtDebtorName.Text.Trim());
                    cmd.Parameters.AddWithValue("@p", txtPhoneNumber.Text);
                    cmd.Parameters.AddWithValue("@a", amt);
                    cmd.Parameters.AddWithValue("@c", selectedCurrency);
                    cmd.Parameters.AddWithValue("@note", txtDebtNote.Text);

                    int newId = Convert.ToInt32(cmd.ExecuteScalar());
                    SaveActionLog(newId, "إضافة مديون", amt, selectedCurrency, "افتتاح سجل مديونية جديد");
                }
            }

            AddDebtCard.Visibility = Visibility.Collapsed;
            LoadDebtsData();
            if (selectedDebtForPay != null) LoadLogs(selectedDebtForPay.Id);
        }

        // --- دالة تحميل السجلات (تعديل تنسيق العملة حسب نوع الحساب) ---
        private void LoadLogs(int debtorId)
        {
            try
            {
                var logs = new ObservableCollection<dynamic>();
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    // الاستعلام لجلب كافة تفاصيل الحركات
                    cmd.CommandText = "SELECT ActionType, Details, AmountChanged, LogCurrency, LogDate FROM DebtLogs WHERE DebtorId = @id ORDER BY LogId DESC";
                    cmd.Parameters.AddWithValue("@id", debtorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string actionType = reader.GetString(0);
                            string details = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            decimal amt = reader.GetDecimal(2);
                            string logCurr = reader.IsDBNull(3) ? "د.ع" : reader.GetString(3);
                            string date = reader.IsDBNull(4) ? "" : reader.GetString(4);

                            // 1. تنسيق عرض المبلغ حسب العملة المخزونة للعملية
                            string displayAmt = "";
                            if (logCurr.Contains("$") || logCurr.ToUpper().Contains("USD"))
                            {
                                displayAmt = "$ " + amt.ToString("N2");
                            }
                            else
                            {
                                displayAmt = amt.ToString("N0") + " د.ع";
                            }

                            // 2. ⭐ المنطق الجديد: هل هذه العملية عبارة عن فاتورة؟
                            // نستخدم المقارنة مع النص الذي تخزنه عند إنشاء الفاتورة
                            bool isInvoice = actionType == "فاتورة جديدة";

                            // 3. إضافة البيانات للكائن مع خصائص التحكم بالظهور (Visibility)
                            // بداخل دالة LoadLogs
                            logs.Add(new
                            {
                                ActionType = actionType,
                                Details = details,
                                AmountDisplay = displayAmt,
                                LogDate = date,
                                // ⭐ نمرر اسم الزبون المعروض حالياً في الشاشة
                                CustomerName = txtProfileTitle.Text,
                                IsInvoiceButtonVisible = isInvoice ? Visibility.Visible : Visibility.Collapsed,
                                IsNormalTextVisible = isInvoice ? Visibility.Collapsed : Visibility.Visible
                            });
                        }
                    }
                }
                // ربط القائمة المحدثة بالجدول
                dgvLogs.ItemsSource = logs;
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ في تحميل سجل العمليات: " + ex.Message);
            }
        }
        private void BtnOpenInvoiceFromLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                string customerName = btn.Tag?.ToString() ?? ""; // الاسم: "شادي سوريه"
                string details = btn.CommandParameter?.ToString() ?? ""; // التفاصيل: "فاتورة آجل رقم: 184"

                // 1. استخراج رقم الفاتورة
                string invoiceNumber = System.Text.RegularExpressions.Regex.Match(details, @"\d+").Value;
                if (string.IsNullOrEmpty(invoiceNumber) || string.IsNullOrEmpty(customerName)) return;

                // 2. تنظيف اسم الزبون من الرموز (نفس الطريقة اللي خزننا بيها الملف)
                string safeCustomerName = System.Text.RegularExpressions.Regex.Replace(customerName, @"[\\/:*?""<>|]", "_");

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string pdfFolder = Path.Combine(desktopPath, "pdf");

                if (Directory.Exists(pdfFolder))
                {
                    // 3. ⭐ البحث المزدوج: يجب أن يحتوي الاسم على الرقم واسم الزبون
                    var files = Directory.GetFiles(pdfFolder, "*.pdf");

                    var finalFile = files.FirstOrDefault(f => {
                        string fileName = Path.GetFileName(f);
                        // الفحص: هل الاسم يحتوي على الرقم "و" اسم الزبون؟
                        return fileName.Contains(invoiceNumber) && fileName.Contains(safeCustomerName);
                    });

                    if (finalFile != null)
                    {
                        Process.Start(new ProcessStartInfo(finalFile) { UseShellExecute = true });
                    }
                    else
                    {
                        MessageBox.Show($"حجي ما لكيت ملف مطابق للاسم: ({customerName}) ورقم الفاتورة: ({invoiceNumber})");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ أثناء فتح الفاتورة: " + ex.Message);
            }
        }
        private void PrintToWord(dynamic data)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string pdfFolder = Path.Combine(desktopPath, "Receipts_PDF");
                if (!Directory.Exists(pdfFolder)) Directory.CreateDirectory(pdfFolder);

                string templatePath = Path.Combine(currentDirectory, "tsded.docx");
                if (!File.Exists(templatePath)) { MessageBox.Show("حجي قالب tsded.docx مفقود!"); return; }

                string fileName = $"{data.Customer}_{data.ReceiptNumber}";
                string docxPath = Path.Combine(pdfFolder, fileName + ".docx");
                string pdfPath = Path.Combine(pdfFolder, fileName + ".pdf");

                using (DocX doc = DocX.Load(templatePath))
                {
                    // الاستبدالات القديمة
                    doc.ReplaceText("[Receipt Number]", data.ReceiptNumber);
                    doc.ReplaceText("[Date]", data.Date);
                    doc.ReplaceText("[Customer]", data.Customer);
                    doc.ReplaceText("[AmountNumber]", data.AmountNumber);
                    doc.ReplaceText("[WrittenNumber]", data.WrittenNumber);
                    doc.ReplaceText("[PreBal]", data.PreBal);
                    doc.ReplaceText("[CurBal]", data.CurBal);
                    doc.ReplaceText("[Notes]", data.Notes);
                    doc.ReplaceText("[Time]", data.Time);
                    // الاستبدالات الجديدة (تأكد أن الأسماء بين القوسين تطابق ملف الـ Word تماماً)
                    doc.ReplaceText("[DebitAccount]", data.DebitAccount);
                    doc.ReplaceText("[Depositedby]", data.Depositedby);
                    doc.ReplaceText("[Signature]", data.Signature);

                    doc.SaveAs(docxPath);
                }

                ConvertToPdfWithLibreOffice(docxPath, pdfFolder);
                System.Threading.Thread.Sleep(1000);
                if (File.Exists(pdfPath))
                {
                    if (File.Exists(docxPath)) File.Delete(docxPath);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ConvertToPdfWithLibreOffice(string sourceDocx, string outputDirectory)
        {
            DocxHelper.ConvertWordToPdf(sourceDocx);
        }

        public string ToArabicWords(decimal number, bool isDinar)
{
    if (number == 0) return "صفر " + (isDinar ? "دينار عراقي" : "دولار أمريكي");

    // 1. فصل الجزء الصحيح عن الكسر حجي
    long integralPart = (long)Math.Truncate(number);
    
    // حساب الكسر: إذا دينار نضرب بـ 1000 (للفلس)، إذا دولار بـ 100 (للسنت)
    decimal fraction = number - integralPart;
    int decimalPart = isDinar ? (int)Math.Round(fraction * 1000) : (int)Math.Round(fraction * 100);

    string[] ones = { "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر" };
    string[] tens = { "", "عشرة", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
    string[] hundreds = { "", "مئة", "مئتان", "ثلاثمئة", "أربعمئة", "خمسمئة", "ستمئة", "سبعمئة", "ثمانمئة", "تسعمئة" };

    // 2. تحويل الجزء الصحيح (الدنانير أو الدولارات)
    string words = "";
    long tempNumber = integralPart;

    if (tempNumber >= 1000000)
    {
        long mil = tempNumber / 1000000;
        if (mil == 1) words += "مليون";
        else if (mil == 2) words += "مليونان";
        else words += ToArabicWordsBase(mil, ones, tens, hundreds) + " ملايين";
        tempNumber %= 1000000;
    }

    if (tempNumber >= 1000)
    {
        if (words != "") words += " و ";
        long thr = tempNumber / 1000;
        if (thr == 1) words += "ألف";
        else if (thr == 2) words += "ألفان";
        else if (thr >= 3 && thr <= 10) words += ones[thr] + " آلاف";
        else words += ToArabicWordsBase(thr, ones, tens, hundreds) + " ألف";
        tempNumber %= 1000;
    }

    if (tempNumber > 0)
    {
        if (words != "") words += " و ";
        words += ToArabicWordsBase(tempNumber, ones, tens, hundreds);
    }

    // إضافة العملة الأساسية
    if (integralPart > 0)
        words += isDinar ? " دينار عراقي" : " دولار أمريكي";

    // 3. معالجة البوينتات (الفلوس أو السنتات) حجي
    if (decimalPart > 0)
    {
        if (words != "") words += " و ";
        
        string fractionalWords = ToArabicWordsBase(decimalPart, ones, tens, hundreds);
        string fractionalCurrency = isDinar ? " فلس" : " سنت";
        
        words += fractionalWords + fractionalCurrency;
    }

    return words.Trim() + " فقط لا غير";
}
        private void BtnPrintFullStatement_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDebtForPay == null) return;

            try
            {
                // 1. إعداد المسارات (سطح المكتب -> فولدر pdf)
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StatementTemplate.docx");
                if (!File.Exists(templatePath)) { MessageBox.Show("حجي قالب StatementTemplate.docx غير موجود!"); return; }

                string pdfFolderPath = DocxHelper.EnsurePdfFolder();

                string safeFileName = DocxHelper.SanitizeFileName(selectedDebtForPay.DebtorName);
                string fileName = $"كشف_حساب_{safeFileName}_{DateTime.Now:HH-mm}";
                string docxPath = Path.Combine(pdfFolderPath, fileName + ".docx");
                string pdfPath = Path.Combine(pdfFolderPath, fileName + ".pdf");

                // 2. معالجة ملف الوورد
                using (var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (DocX document = DocX.Load(templateStream))
                    {
                        var arabicFormat = new Formatting { FontFamily = new Xceed.Document.NET.Font("Arabic Transparent"), Size = 12 };

                        // تعويض البيانات الأساسية
                        document.ReplaceText("[CustomerName]", selectedDebtForPay.DebtorName);
                        document.ReplaceText("[Date]", DateTime.Now.ToString("yyyy/MM/dd"));
                        document.ReplaceText("[TotalRemaining]", selectedDebtForPay.DisplayAmount);

                        // 3. ملء جدول الحركات (يفترض أنه الجدول الأول في الملف)
                        // 3. ملء جدول الحركات
                        var table = document.Tables[0];
                        var patternRow = table.Rows[1];

                        // --- التعديل هنا حجي ---
                        // جلب البيانات وتحويلها لقائمة ثم عكسها لتصبح من الأقدم للأحدث
                        var logsList = (dgvLogs.ItemsSource as IEnumerable<dynamic>).ToList();
                        logsList.Reverse();

                        int i = 0;
                        foreach (var log in logsList) // نستخدم القائمة المعكوسة هنا
                        {
                            Xceed.Document.NET.Row newRow = (i == 0) ? patternRow : table.InsertRow(patternRow);

                            FillCell(newRow.Cells[3], log.ActionType, 10);
                            FillCell(newRow.Cells[2], log.Details, 9);
                            FillCell(newRow.Cells[1], log.AmountDisplay, 10);
                            FillCell(newRow.Cells[0], log.LogDate, 10);

                            i++;
                        }

                        document.SaveAs(docxPath);
                    }
                }

                // 4. التحويل إلى PDF باستخدام LibreOffice
                ConvertToPdfWithLibreOffice(docxPath, pdfFolderPath);

                System.Threading.Thread.Sleep(1000); // انتظار بسيط لإتمام العملية

                if (File.Exists(pdfPath))
                {
                    if (File.Exists(docxPath)) File.Delete(docxPath);
                    Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ بالطباعة خالي: " + ex.Message);
            }
        }

        private void FillCell(Xceed.Document.NET.Cell cell, string text, double fontSize) => DocxHelper.FillCell(cell, text, fontSize);
        private string ToArabicWordsBase(long n, string[] ones, string[] tens, string[] hundreds)
        {
            string res = "";
            if (n >= 100) { res += hundreds[n / 100]; n %= 100; }
            if (n > 0)
            {
                if (res != "") res += " و ";
                if (n < 20) res += ones[n];
                else
                {
                    if (n % 10 > 0) res += ones[n % 10] + " و ";
                    res += tens[n / 10];
                }
            }
            return res;
        }
        #endregion

        #region "UI Events"
        public void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            selectedDebtForPay = (sender as Button).DataContext as DebtItem;
            if (selectedDebtForPay == null) return;
            txtReceiptNo.Text = DateTime.Now.Ticks.ToString().Substring(10);
            txtReceiptDate.Text = DateTime.Now.ToString("yyyy/MM/dd");
            txtReceiptTime.Text = DateTime.Now.ToString("HH:mm");
            txtPrevBalance.Text = selectedDebtForPay.DisplayAmount;
            txtCurrentBalance.Text = selectedDebtForPay.DisplayAmount;
            txtPayAmountInput.Clear();
            lblAmountWords.Text = "";
            PayDebtCard.Visibility = Visibility.Visible;
        }

        private void ClosePayCard_Click(object sender, RoutedEventArgs e)
        {
            PayDebtCard.Visibility = Visibility.Collapsed;
            // تبقى صفحة السجل مفتوحة بالخلفية تلقائياً
        }

        private void ShowAddDebt_Click(object sender, RoutedEventArgs e)
        {
            isEditMode = false;
            ClearDebtInputs();
            isIraqiDinar = true;
            btnCurrencyToggle.Content = "د.ع";
            AddDebtCard.Visibility = Visibility.Visible;
        }

        private void HideAddDebt_Click(object sender, RoutedEventArgs e)
        {
            AddDebtCard.Visibility = Visibility.Collapsed;

            // حجي هنا الشرط: إذا كنا فاتحين صفحة الملف، لا تخفيها
            if (UserProfilePage.Visibility == Visibility.Visible)
            {
                // نبقى بصفحة السجلات ونحدث بياناتها فقط
                LoadLogs(selectedDebtForPay.Id);
                lblProfileTotal.Text = selectedDebtForPay.DisplayAmount;
            }
        }

        private void txtSearchDebt_TextChanged(object sender, TextChangedEventArgs e) => LoadDebtsData(txtSearchDebt.Text);

        private void ClearDebtInputs()
        {
            txtDebtorName.Clear();
            txtPhoneNumber.Clear();
            txtInitialAmount.Clear();
            txtDebtNote.Clear();
            lblAmountWords.Text = "صفر دينار عراقي";
        }
        #endregion
    }
}