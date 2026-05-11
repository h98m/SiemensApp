using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xceed.Words.NET;
using Xceed.Document.NET;
using System.Windows.Media.Animation;
namespace SiemensApp.Views
{
    public class EditorItem : INotifyPropertyChanged
    {
        private string _productName = "";
        private decimal _qty = 1;
        private decimal _price = 0;
        private string _type = "قطعة";
        private string _notes = "د.ع"; // هذا الحقل هو العملة حجي

        public string ProductName { get => _productName; set { _productName = value; OnPropertyChanged(); } }
        public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }
        public decimal Qty { get => _qty; set { _qty = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }
        public decimal Price { get => _price; set { _price = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }
        public string Notes { get => _notes; set { _notes = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }

        // المجموع ديسيمال لضمان الدقة المالية
        public decimal Total => Qty * Price;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class InvoiceEditorView : UserControl
    {
        // هذا الكود يحدد مسار المجلد اللي يشتغل منه البرنامج حالياً
        private static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        // ندمج مسار المجلد مع اسم قاعدة البيانات
        private string dbPath = $"Data Source={System.IO.Path.Combine(currentDirectory, "SiemensData.db")}"; private string selectedInvoiceType = "وصل المحل";
        private DateTime originalDate = DateTime.Now;
        private int currentInvoiceAutoId = 0;

        public ObservableCollection<EditorItem> Items { get; set; } = new ObservableCollection<EditorItem>();

        public InvoiceEditorView()
        {
            InitializeComponent();
            dgvInvoiceItems.ItemsSource = Items;
            // هذا السطر يراقب الجدول ويحدث المجموع فوراً عند التحميل أو الإضافة
            Items.CollectionChanged += (s, e) => UpdateGrandTotal();
        }

        // --- 1. تحميل بيانات الفاتورة ---

        // --- 1. تحميل بيانات الفاتورة مع الإعدادات حجي ---
        public void LoadInvoiceForEdit(int autoId)
        {
            currentInvoiceAutoId = autoId;
            Items.Clear();
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();

                    // 1. جلب بيانات رأس الفاتورة مع الأعمدة الجديدة
                    var cmdH = connection.CreateCommand();
                    // حجي هنا استعلمنا عن كل الحقول بما فيها حقول الإعدادات
                    cmdH.CommandText = @"SELECT InvoiceNumber, CustomerName, Phone, Address, PaymentStatus, DollarRate, PreviousDebt, 
                                        IsDollarMode, IsHideWriting, IsDefaultMode, InvoiceType 
                                 FROM Invoices WHERE Id = @id";
                    cmdH.Parameters.AddWithValue("@id", autoId);

                    using (var r = cmdH.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            txtInvoiceNumber.Text = r["InvoiceNumber"]?.ToString() ?? "";
                            txtCustomerName.Text = r["CustomerName"]?.ToString() ?? "";
                            txtCustomerPhone.Text = r["Phone"]?.ToString() ?? "";
                            txtCustomerAddress.Text = r["Address"]?.ToString() ?? "";
                            txtDollarRate.Text = r["DollarRate"]?.ToString() ?? "145";

                            // --- استرجاع الإعدادات التاريخية للفاتورة حجي ---

                            // تعطيل الـ Events مؤقتاً لتجنب التضارب أثناء التحميل البرمجي
                            chkDefaultMode.IsChecked = false;
                            chkShowDollar.IsChecked = false;
                            chkShowDinar.IsChecked = false;

                            // قراءة القيم مع التأكد من وجود الأعمدة لتجنب الأخطاء (Safe Reading)
                            try
                            {
                                if (r.GetSchemaTable().Select("ColumnName = 'IsDollarMode'").Length > 0)
                                {
                                    chkShowDollar.IsChecked = r["IsDollarMode"] != DBNull.Value && Convert.ToInt32(r["IsDollarMode"]) == 1;
                                    chkHideWriting.IsChecked = r["IsHideWriting"] != DBNull.Value && Convert.ToInt32(r["IsHideWriting"]) == 1;
                                    chkDefaultMode.IsChecked = r["IsDefaultMode"] != DBNull.Value && Convert.ToInt32(r["IsDefaultMode"]) == 1;
                                }
                                else
                                {
                                    // إذا الأعمدة مو موجودة نخليه على الوضع الافتراضي
                                    chkDefaultMode.IsChecked = true;
                                }
                            }
                            catch { chkDefaultMode.IsChecked = true; }

                            // تحديث نوع الوصل المحفوظ
                            selectedInvoiceType = r["InvoiceType"]?.ToString() ?? "وصل المحل";
                            if (txtCurrentType != null) txtCurrentType.Text = selectedInvoiceType;

                            // معالجة الدين السابق التاريخي
                            decimal prevDebt = r["PreviousDebt"] != DBNull.Value ? Convert.ToDecimal(r["PreviousDebt"]) : 0;

                            // جلب عملة الزبون من جدول الديون لعرض الرمز الصحيح ($ أو د.ع)
                            string dbCurrency = "د.ع";
                            try
                            {
                                var cmdCurr = connection.CreateCommand();
                                cmdCurr.CommandText = "SELECT Currency FROM DebtsMe WHERE DebtorName = @name LIMIT 1";
                                cmdCurr.Parameters.AddWithValue("@name", txtCustomerName.Text);
                                var res = cmdCurr.ExecuteScalar();
                                if (res != null) dbCurrency = res.ToString();
                            }
                            catch { }

                            if (dbCurrency.Contains("دولار") || dbCurrency == "$")
                                txtDebtDollar.Text = prevDebt.ToString("N2") + " $";
                            else
                                txtDebtDollar.Text = prevDebt.ToString("N0") + " د.ع";

                            btnPaymentStatus.IsChecked = (r["PaymentStatus"]?.ToString() == "آجل");
                        }
                    }

                    // 2. تحميل مواد الفاتورة
                    var cmdI = connection.CreateCommand();
                    cmdI.CommandText = "SELECT ProductName, Price, Qty, UnitType, Currency FROM InvoiceDetails WHERE InvoiceId = @id";
                    cmdI.Parameters.AddWithValue("@id", autoId);
                    using (var r = cmdI.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Items.Add(new EditorItem
                            {
                                ProductName = r.GetString(0),
                                Price = r.GetDecimal(1),
                                Qty = r.GetDecimal(2),
                                Type = r.IsDBNull(3) ? "قطعة" : r.GetString(3),
                                Notes = r.IsDBNull(4) ? "د.ع" : r.GetString(4)
                            });
                        }
                    }
                }

                // أهم خطوة حجي: تحديث المجاميع والواجهة بناءً على الخيارات اللي حملناها
                UpdateGrandTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ بالتحميل حجي: " + ex.Message);
            }
        }
        private void BtnSaveInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text)) { ShowToast("حجي، اكتب اسم الزبون!"); return; }

            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                using (var tran = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. جلب بيانات الفاتورة القديمة قبل التعديل (لحساب الفرق المالي بدقة)
                        decimal oldTotalDinar = 0;
                        string oldStatus = "";
                        var cmdOld = connection.CreateCommand();
                        cmdOld.Transaction = tran;
                        cmdOld.CommandText = "SELECT TotalAmount, PaymentStatus FROM Invoices WHERE Id = @id";
                        cmdOld.Parameters.AddWithValue("@id", currentInvoiceAutoId);
                        using (var rOld = cmdOld.ExecuteReader())
                        {
                            if (rOld.Read())
                            {
                                oldTotalDinar = rOld.GetDecimal(0);
                                oldStatus = rOld.GetString(1);
                            }
                        }

                        // 2. الحسابات الجديدة للمواد المعدلة
                        decimal.TryParse(txtDollarRate.Text, out decimal rateInput);
                        decimal exchangeFactor = rateInput * 10;
                        decimal currentItemsDinar = Items.Where(i => i.Notes == "د.ع").Sum(i => i.Total);
                        decimal currentItemsDollar = Items.Where(i => i.Notes == "$").Sum(i => i.Total);

                        decimal newTotalDinar = (currentItemsDollar * exchangeFactor) + currentItemsDinar;
                        decimal newTotalDollar = (exchangeFactor > 0) ? (newTotalDinar / exchangeFactor) : 0;

                        // 3. منطق موازنة الديون
                        bool isAjelNow = btnPaymentStatus.IsChecked == true;
                        decimal diffToApply = 0;

                        if (isAjelNow)
                        {
                            if (oldStatus == "آجل") diffToApply = newTotalDinar - oldTotalDinar;
                            else diffToApply = newTotalDinar;
                        }
                        else if (oldStatus == "آجل" && !isAjelNow)
                        {
                            diffToApply = -oldTotalDinar;
                        }

                        // 4. تحديث سجل الديون والحركات (المقص الذكي حجي)
                        if (diffToApply != 0 || (isAjelNow != (oldStatus == "آجل")))
                        {
                            var cmdCheck = connection.CreateCommand();
                            cmdCheck.Transaction = tran;
                            cmdCheck.CommandText = "SELECT Id, Currency FROM DebtsMe WHERE DebtorName = @name";
                            cmdCheck.Parameters.AddWithValue("@name", txtCustomerName.Text);

                            int debtorId = 0;
                            string customerCurrency = "د.ع";
                            using (var reader = cmdCheck.ExecuteReader())
                            {
                                if (reader.Read()) { debtorId = reader.GetInt32(0); customerCurrency = reader.GetString(1); }
                            }

                            if (debtorId == 0 && isAjelNow)
                            {
                                var cmdInsert = connection.CreateCommand();
                                cmdInsert.Transaction = tran;
                                cmdInsert.CommandText = "INSERT INTO DebtsMe (DebtorName, TotalAmount, Currency, LastTransactionDate) VALUES (@name, 0, 'د.ع', @dt); SELECT last_insert_rowid();";
                                cmdInsert.Parameters.AddWithValue("@name", txtCustomerName.Text);
                                cmdInsert.Parameters.AddWithValue("@dt", DateTime.Now);
                                debtorId = Convert.ToInt32(cmdInsert.ExecuteScalar());
                            }

                            if (debtorId > 0)
                            {
                                // أ. موازنة الحساب الكلي للزبون
                                decimal finalDiff = customerCurrency.Contains("$") ? (diffToApply / (exchangeFactor > 0 ? exchangeFactor : 1)) : diffToApply;
                                var cmdUpdDebt = connection.CreateCommand();
                                cmdUpdDebt.Transaction = tran;
                                cmdUpdDebt.CommandText = "UPDATE DebtsMe SET TotalAmount = TotalAmount + @diff, LastTransactionDate = @dt WHERE Id = @id";
                                cmdUpdDebt.Parameters.AddWithValue("@diff", Math.Round(finalDiff, 2));
                                cmdUpdDebt.Parameters.AddWithValue("@id", debtorId);
                                cmdUpdDebt.Parameters.AddWithValue("@dt", DateTime.Now);
                                cmdUpdDebt.ExecuteNonQuery();

                                // ب. 🔥 الحذف الذكي: نمسح أي حركة قديمة لهاي الفاتورة حتى ما تتكرر الأسطر بالسجل
                                var cmdDelOldLogs = connection.CreateCommand();
                                cmdDelOldLogs.Transaction = tran;
                                cmdDelOldLogs.CommandText = "DELETE FROM DebtLogs WHERE DebtorId = @id AND Details LIKE @desc";
                                cmdDelOldLogs.Parameters.AddWithValue("@id", debtorId);
                                cmdDelOldLogs.Parameters.AddWithValue("@desc", "%رقم: " + txtInvoiceNumber.Text + "%");
                                cmdDelOldLogs.ExecuteNonQuery();

                                // ج. ✨ إضافة السطر الجديد بالقمة (يحتوي على السعر الكلي الجديد)
                                if (isAjelNow)
                                {
                                    decimal amountForLog = customerCurrency.Contains("$") ? newTotalDollar : newTotalDinar;
                                    SaveActionLog(connection, tran, debtorId, "فاتورة جديدة", Math.Round(amountForLog, 2), customerCurrency, $"فاتورة رقم: {txtInvoiceNumber.Text}");
                                }
                            }
                        }

                        // 5. تحديث بيانات الفاتورة في قاعدة البيانات
                        var cmdUpdInv = connection.CreateCommand();
                        cmdUpdInv.Transaction = tran;
                        cmdUpdInv.CommandText = @"UPDATE Invoices SET CustomerName=@n, TotalAmount=@t, TotalAmountDollar=@td, 
                                      DollarRate=@dr, PaymentStatus=@ps WHERE Id=@id";
                        cmdUpdInv.Parameters.AddWithValue("@n", txtCustomerName.Text);
                        cmdUpdInv.Parameters.AddWithValue("@t", Math.Round(newTotalDinar, 0));
                        cmdUpdInv.Parameters.AddWithValue("@td", Math.Round(newTotalDollar, 2));
                        cmdUpdInv.Parameters.AddWithValue("@dr", rateInput);
                        cmdUpdInv.Parameters.AddWithValue("@ps", isAjelNow ? "آجل" : "نقدي");
                        cmdUpdInv.Parameters.AddWithValue("@id", currentInvoiceAutoId);
                        cmdUpdInv.ExecuteNonQuery();

                        // 6. تحديث المواد (حذف وإعادة إضافة)
                        UpdateInvoiceDetails(connection, tran);

                        tran.Commit();
                        ShowToast("تم التعديل وتحديث السجل بنجاح حجي ✅", "#10B981");
                        ExportToWordAndPdf(txtCustomerName.Text, txtInvoiceNumber.Text);
                    }
                    catch (Exception ex)
                    {
                        if (tran.Connection != null) tran.Rollback();
                        MessageBox.Show("خطأ بالتعديل حجي: " + ex.Message);
                    }
                }
            }
        }
        private void SaveActionLog(SqliteConnection conn, SqliteTransaction tran, int debtorId, string type, decimal amount, string currency, string details)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = "INSERT INTO DebtLogs (DebtorId, ActionType, AmountChanged, LogCurrency, Details, LogDate) VALUES (@id, @t, @a, @curr, @det, @d)";
            cmd.Parameters.AddWithValue("@id", debtorId);
            cmd.Parameters.AddWithValue("@t", type);
            cmd.Parameters.AddWithValue("@a", amount);
            cmd.Parameters.AddWithValue("@curr", currency);
            cmd.Parameters.AddWithValue("@det", details);
            cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            cmd.ExecuteNonQuery();
        }
        

        // --- أ. دوال التحكم بلوحة الإعدادات حجي ---
        private string GetCustomerCurrency(SqliteConnection connection, SqliteTransaction tran, string customerName)
        {
            try
            {
                var cmd = connection.CreateCommand();
                cmd.Transaction = tran;
                cmd.CommandText = "SELECT Currency FROM DebtsMe WHERE DebtorName = @name LIMIT 1";
                cmd.Parameters.AddWithValue("@name", customerName);
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? "د.ع"; // إذا مجهول نعتبره دينار
            }
            catch
            {
                return "د.ع";
            }
        }
        private void UpdateInvoiceDetails(SqliteConnection connection, SqliteTransaction tran)
        {
            // حذف المواد القديمة المرتبطة بهاي الفاتورة
            var cmdDel = connection.CreateCommand();
            cmdDel.Transaction = tran;
            cmdDel.CommandText = "DELETE FROM InvoiceDetails WHERE InvoiceId = @id";
            cmdDel.Parameters.AddWithValue("@id", currentInvoiceAutoId);
            cmdDel.ExecuteNonQuery();

            // إضافة المواد الجديدة بعد التعديل
            foreach (var item in Items)
            {
                var cmdD = connection.CreateCommand();
                cmdD.Transaction = tran;
                cmdD.CommandText = @"INSERT INTO InvoiceDetails (InvoiceId, ProductName, Price, Qty, Total, UnitType, Currency) 
                            VALUES (@id, @pn, @pr, @q, @t, @ut, @cur)";
                cmdD.Parameters.AddWithValue("@id", currentInvoiceAutoId);
                cmdD.Parameters.AddWithValue("@pn", item.ProductName);
                cmdD.Parameters.AddWithValue("@pr", item.Price);
                cmdD.Parameters.AddWithValue("@q", item.Qty);
                cmdD.Parameters.AddWithValue("@t", item.Total);
                cmdD.Parameters.AddWithValue("@ut", item.Type);
                cmdD.Parameters.AddWithValue("@cur", item.Notes);
                cmdD.ExecuteNonQuery();
            }
        }
        private void btnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPane.Visibility = Visibility.Visible; // إظهار اللوحة أولاً
            Storyboard sb = (Storyboard)this.Resources["OpenSettingsAnim"];
            sb.Begin();
        }

        private void btnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            
            var anim = new DoubleAnimation(320, TimeSpan.FromSeconds(0.3));
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
            SettingsPaneTransform.BeginAnimation(TranslateTransform.XProperty, anim);

            var opacityAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            opacityAnim.Completed += (s, _) => SettingsPane.Visibility = Visibility.Collapsed;
            SettingsPane.BeginAnimation(OpacityProperty, opacityAnim);
        }

        // --- ب. دوال التحكم بالخيارات (الـ CheckBoxes) حجي ---

        private void chkDefaultMode_Checked(object sender, RoutedEventArgs e)
        {
            if (chkShowDollar == null || chkShowDinar == null) return;
            // إذا تفعل الافتراضي، نلغي بقية الفلاتر
            chkShowDollar.IsChecked = false;
            chkShowDinar.IsChecked = false;
            UpdateGrandTotal();
        }

        private void chkShowDollar_Checked(object sender, RoutedEventArgs e)
        {
            if (chkDefaultMode == null || chkShowDinar == null) return;
            // إذا تفعل الدولار فقط، نلغي الافتراضي والدينار
            chkDefaultMode.IsChecked = false;
            chkShowDinar.IsChecked = false;
            UpdateGrandTotal();
        }

        private void chkShowDinar_Checked(object sender, RoutedEventArgs e)
        {
            if (chkDefaultMode == null || chkShowDollar == null) return;
            // إذا تفعل الدينار فقط، نلغي الافتراضي والدولار
            chkDefaultMode.IsChecked = false;
            chkShowDollar.IsChecked = false;
            UpdateGrandTotal();
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            // أي خيار آخر مثل إخفاء التفقيط، نلغي وضع "الافتراضي الكامل"
            if (chkDefaultMode != null) chkDefaultMode.IsChecked = false;
            UpdateGrandTotal();
        }
        // --- 2. الحسابات الذكية ---
        private void UpdateGrandTotal()
        {
            // فحص الأمان حجي حتى ما يضرب البرنامج
            if (txtDollarRate == null || txtDebtDollar == null || lblTotal == null || lblTotalDollar == null || Items == null)
                return;

            try
            {
                // 1. جلب سعر الصرف (مثلاً 145) وتحويله للمعامل الحقيقي (1450)
                if (!decimal.TryParse(txtDollarRate.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal rate)) rate = 0;
                decimal exchangeFactor = rate * 10;

                // 2. جمع المواد حسب عملة كل مادة بالجدول
                decimal itemsDollar = Items.Where(i => i.Notes == "$").Sum(i => i.Total);
                decimal itemsDinar = Items.Where(i => i.Notes == "د.ع").Sum(i => i.Total);

                // 3. معالجة الدين السابق (ذكاء اصطناعي بسيط حجي)
                string debtText = txtDebtDollar.Text;
                string cleanAmount = Regex.Replace(debtText, @"[^0-9.]", ""); // وخر الرموز وخذ بس الرقم
                if (!decimal.TryParse(cleanAmount, out decimal prevDebtRaw)) prevDebtRaw = 0;

                // إذا النص بيه $ نحسبه دولار، وإذا لا نعتبره دينار
                decimal prevDebtInDinar = debtText.Contains("$") ? (prevDebtRaw * exchangeFactor) : prevDebtRaw;

                // 4. الحسابات النهائية (المجموع الكلي بالدينار)
                decimal finalTotalDinar = (itemsDollar * exchangeFactor) + itemsDinar + prevDebtInDinar;

                // المجموع الكلي بالدولار (نقسم المجموع النهائي على الصرف)
                decimal finalTotalDollar = (exchangeFactor > 0) ? (finalTotalDinar / exchangeFactor) : 0;

                // 5. عرض النتائج بالواجهة
                lblTotal.Text = finalTotalDinar.ToString("N0"); // دينار بدون بوينتات
                lblTotalDollar.Text = finalTotalDollar.ToString("N2"); // دولار مع بوينتين
            }
            catch (Exception ex)
            {
                Debug.WriteLine("خطأ في الحساب حجي: " + ex.Message);
            }
        }

        // --- 3. إدارة الواجهة (Events) ---
        private void UpdateGrandTotal_Event(object sender, TextChangedEventArgs e) => UpdateGrandTotal();

        private void btnPaymentStatus_Checked(object sender, RoutedEventArgs e) => UpdateGrandTotal();

        private void btnPaymentStatus_Unchecked(object sender, RoutedEventArgs e) => UpdateGrandTotal();

        private void btnInvoiceType_Click(object sender, RoutedEventArgs e) => btnInvoiceType.ContextMenu.IsOpen = true;

        private void InvoiceType_Selected(object sender, RoutedEventArgs e)
        {
            selectedInvoiceType = (sender as MenuItem).Header.ToString().Replace("🏠 ", "").Replace("⚠️ ", "");
            txtCurrentType.Text = selectedInvoiceType;
            UpdateGrandTotal();
        }

        private void btnAddRow_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInputProduct.Text)) return;

            // 1. تنظيف الاسم من أي إضافات مثل (متوفر: 12) حجي
            string productName = Regex.Replace(txtInputProduct.Text, @"\s\(.*?\)", "").Trim();

            // 2. قراءة الكمية مع ضمان الكسور (مثل 0.5) حجي
            if (!decimal.TryParse(txtInputQty.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal q))
                q = 1;

            // 3. تنظيف السعر من الفواصل (الجماعة مالت الـ 1,000) قبل التحويل الرقمي
            string priceClean = txtInputPrice.Text.Replace(",", "");
            if (!decimal.TryParse(priceClean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal p))
                p = 0;

            string currentSymbol = btnCurrencyToggle.IsChecked == true ? "$" : "د.ع";

            // 4. التأكد من خزن المادة بالسجل العام إذا جانت جديدة حجي
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmdCheck = connection.CreateCommand();
                    cmdCheck.CommandText = @"
                SELECT (SELECT COUNT(*) FROM InternalStock WHERE ProductName = @pn) + 
                       (SELECT COUNT(*) FROM GlobalStock WHERE ProductName = @pn)";
                    cmdCheck.Parameters.AddWithValue("@pn", productName);
                    long totalExists = (long)cmdCheck.ExecuteScalar();

                    if (totalExists == 0)
                    {
                        var cmdSaveGlobal = connection.CreateCommand();
                        cmdSaveGlobal.CommandText = "INSERT INTO GlobalStock (ProductName, DefaultPrice, Currency) VALUES (@pn, @pr, @cur)";
                        cmdSaveGlobal.Parameters.AddWithValue("@pn", productName);
                        cmdSaveGlobal.Parameters.AddWithValue("@pr", p);
                        cmdSaveGlobal.Parameters.AddWithValue("@cur", (currentSymbol == "$" ? "دولار أمريكي" : "دينار عراقي"));
                        cmdSaveGlobal.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("خطأ بسيط بسجل المواد حجي: " + ex.Message); }

            // 5. إضافة المادة للجدول (EditorItem) وتحديث المجموع
            Items.Add(new EditorItem
            {
                ProductName = productName,
                Price = p,
                Qty = q,
                Type = txtInputType.Text,
                Notes = currentSymbol
            });

            UpdateGrandTotal();

            // 6. تصفير الحقول حجي حتى تبلش بالمادة اللي وراها بسرعة
            txtInputProduct.Clear();
            txtInputPrice.Clear();
            txtInputQty.Text = "1";
            txtInputProduct.Focus();
        }
        private void txtInputPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox tb) || lblPriceInWords == null) return;

            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                lblPriceInWords.Text = "";
                return;
            }

            try
            {
                // 1. إزالة الفواصل الحالية للتنقيط
                string cleanValue = tb.Text.Replace(",", "");
                if (decimal.TryParse(cleanValue, out decimal res))
                {
                    // تعطيل الحدث مؤقتاً لمنع التكرار عند تغيير النص برمجياً
                    tb.TextChanged -= txtInputPrice_TextChanged;

                    int cursorPosition = tb.SelectionStart;
                    int oldLength = tb.Text.Length;

                    // تحديث التنقيط (فواصل الآلاف)
                    tb.Text = res.ToString("N0");

                    // إرجاع الماوس لمكانه
                    int newLength = tb.Text.Length;
                    tb.SelectionStart = Math.Max(0, cursorPosition + (newLength - oldLength));

                    tb.TextChanged += txtInputPrice_TextChanged;

                    // 2. تحديث التفقيط (الكلمات) مع العملة
                    // نتحقق من اسم العملة الموجود داخل ToggleButton
                    // 2. تحديث التفقيط (الكلمات) مع العملة باستخدام المحرك المركزي
                    string cur = (btnCurrencyToggle.IsChecked == true) ? "دولار" : "دينار عراقي";
                    string sub = (btnCurrencyToggle.IsChecked == true) ? "سنت" : "فلس";

                    lblPriceInWords.Text = SiemensApp.Helpers.TafqeetTool.Convert(res, cur, sub);
                }
            }
            catch { }
        }

        // دالة تحويل الرقم لغرض التفقيط
        

        // تحديث الكلمات عند تغيير العملة بالزر
        private void Currency_Changed(object sender, RoutedEventArgs e)
        {
            // نتأكد أن العناصر تم تحميلها أولاً
            if (txtCurrencyFlag == null || txtCurrencyName == null || txtInputPrice == null) return;

            if (btnCurrencyToggle.IsChecked == true)
            {
                txtCurrencyFlag.Text = "🇺🇸";
                txtCurrencyName.Text = "دولار";
            }
            else
            {
                txtCurrencyFlag.Text = "🇮🇶";
                txtCurrencyName.Text = "د.عراقي";
            }

            // أهم خطوة: استدعاء دالة تحديث الكلمات فوراً عند تغيير العملة
            txtInputPrice_TextChanged(txtInputPrice, null);
        }
        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as EditorItem;
            if (item != null)
            {
                txtInputProduct.Text = item.ProductName;

                // حجي هنا السر: نستخدم G29 حتى الـ 0.5 ترجع 0.5 مو 0
                txtInputPrice.Text = item.Price.ToString("G29");
                txtInputQty.Text = item.Qty.ToString("G29");

                txtInputType.Text = item.Type;
                btnCurrencyToggle.IsChecked = (item.Notes == "$");

                Items.Remove(item);
                UpdateGrandTotal();

                // حجي حتى تبلش تعدل فوراً
                txtInputQty.Focus();
                txtInputQty.SelectAll();
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (dgvInvoiceItems.SelectedItem is EditorItem item) { Items.Remove(item); UpdateGrandTotal(); }
        }

       
        private string GetSimpleNextNumber()
        {
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT MAX(Id) FROM Invoices";
                    var result = cmd.ExecuteScalar();
                    int nextId = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return nextId.ToString();
                }
            }
            catch { return "1"; }
        }
        // --- 4. الحفظ والطباعة ---
        
        // حجي هاي الدالة هي اللي تصفي الحساب وتحدث ديون الزبون بدون خبط
        private void UpdateDebtsLogic(SqliteConnection conn, SqliteTransaction tran, string name, decimal dinar, decimal dollar, decimal exchange)
        {
            decimal oldDebtAmount = 0;
            string oldDebtCurrency = "دينار عراقي";

            var cmdGetOld = conn.CreateCommand();
            cmdGetOld.Transaction = tran;
            cmdGetOld.CommandText = "SELECT TotalAmount, Currency FROM DebtsMe WHERE DebtorName = @name";
            cmdGetOld.Parameters.AddWithValue("@name", name);

            using (var reader = cmdGetOld.ExecuteReader())
            {
                if (reader.Read())
                {
                    oldDebtAmount = reader.GetDecimal(0);
                    oldDebtCurrency = reader.GetString(1);
                }
            }

            decimal newTotalToSave = 0;
            // الحسبة تعتمد على عملة الدين القديمة حجي
            if (oldDebtCurrency == "دولار أمريكي" || oldDebtCurrency == "$")
            {
                // إذا دينه القديم دولار، نحول الفاتورة الحالية لصافي دولار ونجمعها
                decimal currentInvoiceInDollar = dollar + (dinar / (exchange > 0 ? exchange : 1));
                newTotalToSave = Math.Round(oldDebtAmount + currentInvoiceInDollar, 2);
            }
            else
            {
                // إذا دينه القديم دينار، نحول الفاتورة الحالية لصافي دينار ونجمعها
                decimal currentInvoiceInDinar = (dollar * exchange) + dinar;
                newTotalToSave = Math.Round(oldDebtAmount + currentInvoiceInDinar, 0);
            }

            var cmdDebt = conn.CreateCommand();
            cmdDebt.Transaction = tran;
            cmdDebt.CommandText = @"INSERT INTO DebtsMe (DebtorName, TotalAmount, Currency, LastTransactionDate) 
                            VALUES (@name, @amt, @cur, @dt) 
                            ON CONFLICT(DebtorName) 
                            DO UPDATE SET TotalAmount = @amt, LastTransactionDate = @dt";

            cmdDebt.Parameters.AddWithValue("@amt", newTotalToSave);
            cmdDebt.Parameters.AddWithValue("@name", name);
            cmdDebt.Parameters.AddWithValue("@cur", oldDebtCurrency);
            cmdDebt.Parameters.AddWithValue("@dt", DateTime.Now);
            cmdDebt.ExecuteNonQuery();
        }
        private void ResetUI()
        {
            txtCustomerName.Clear();
            txtCustomerPhone.Clear();
            txtCustomerAddress.Clear();
            txtDebtDollar.Text = "0"; // تصفير حقل الدين السابق
            Items.Clear(); // تفريغ جدول المواد
            if (lblTotal != null) lblTotal.Text = "0";
            if (lblTotalDollar != null) lblTotalDollar.Text = "0.00";

            // تحديث رقم الوصل للتالي تلقائياً
            if (txtInvoiceNumber != null) txtInvoiceNumber.Text = GetSimpleNextNumber();
        }
        private void UpdateDebtorBalance(SqliteConnection conn, SqliteTransaction tran, string name, decimal dinar, decimal dollar, decimal exchange)
        {
            decimal oldDebtAmount = 0;
            string oldDebtCurrency = "دينار عراقي";

            var cmdGetOld = conn.CreateCommand();
            cmdGetOld.Transaction = tran;
            cmdGetOld.CommandText = "SELECT TotalAmount, Currency FROM DebtsMe WHERE DebtorName = @name";
            cmdGetOld.Parameters.AddWithValue("@name", name);

            using (var reader = cmdGetOld.ExecuteReader())
            {
                if (reader.Read())
                {
                    oldDebtAmount = reader.GetDecimal(0);
                    oldDebtCurrency = reader.GetString(1);
                }
            }

            decimal newTotalToSave = 0;
            // حجي الحسبة تعتمد على عملة الزبون الأصلية بالمحل
            if (oldDebtCurrency == "دولار أمريكي" || oldDebtCurrency == "$")
            {
                // نحول كل الفاتورة الحالية لدولار ونجمعها ويه القديم
                decimal currentInvoiceInDollar = dollar + (dinar / (exchange > 0 ? exchange * 10 : 1));
                newTotalToSave = Math.Round(oldDebtAmount + currentInvoiceInDollar, 2);
            }
            else
            {
                // نحول كل الفاتورة الحالية لدينار ونجمعها ويه القديم
                decimal currentInvoiceInDinar = (dollar * (exchange * 10)) + dinar;
                newTotalToSave = Math.Round(oldDebtAmount + currentInvoiceInDinar, 0);
            }

            var cmdDebt = conn.CreateCommand();
            cmdDebt.Transaction = tran;
            cmdDebt.CommandText = @"INSERT INTO DebtsMe (DebtorName, TotalAmount, Currency, LastTransactionDate) 
                            VALUES (@name, @amt, @cur, @dt) 
                            ON CONFLICT(DebtorName) 
                            DO UPDATE SET TotalAmount = @amt, Currency = @cur, LastTransactionDate = @dt";

            cmdDebt.Parameters.AddWithValue("@amt", newTotalToSave);
            cmdDebt.Parameters.AddWithValue("@name", name);
            cmdDebt.Parameters.AddWithValue("@cur", oldDebtCurrency);
            cmdDebt.Parameters.AddWithValue("@dt", DateTime.Now);
            cmdDebt.ExecuteNonQuery();
        }
        private void ExportToWordAndPdf(string customerName, string invoiceId)
        {
            try
            {
                // 1. تحديد القالب البرمجي (تم التحديث ليشمل كل المحلات حجي)
                string templateFileName = selectedInvoiceType == "وصل محل اجراس" ? "Template1.docx" :
                          selectedInvoiceType == "وصل محل عصام" ? "Template2.docx" :
                          selectedInvoiceType == "وصل محل لمسة التكنلوحيا" ? "Template3.docx" :
                          selectedInvoiceType == "وصل محل المعين" ? "Template4.docx" : "Template.docx";

                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, templateFileName);
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string pdfFolderPath = Path.Combine(desktopPath, "pdf");
                if (!Directory.Exists(pdfFolderPath)) Directory.CreateDirectory(pdfFolderPath);

                // 2. تجهيز اسم الملف مع التوقيت
                string displayInvoiceName = selectedInvoiceType == "وصل محل اجراس" ? "وصل محل اجراس" :
                            selectedInvoiceType == "وصل محل عصام" ? "وصل محل عصام" :
                            selectedInvoiceType == "وصل محل لمسة التكنلوحيا" ? "وصل محل لمسة التكنلوحيا" :
                            selectedInvoiceType == "وصل محل المعين" ? "وصل محل المعين" : selectedInvoiceType;

                string safeFileName = Regex.Replace(customerName, @"[\\/:*?""<>|]", "_");
                string timeStamp = DateTime.Now.ToString("HH-mm");
                string wordOutputPath = Path.Combine(pdfFolderPath, $"{displayInvoiceName}_{safeFileName}_{invoiceId}_{timeStamp}.docx");

                using (var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (DocX document = DocX.Load(templateStream))
                    {
                        // دالة الحذف الذكية
                        void RemoveRowByContent(string searchText)
                        {
                            foreach (var table in document.Tables)
                            {
                                var row = table.Rows.FirstOrDefault(r => r.Cells.Any(c => c.Paragraphs.Any(p => p.Text.Contains(searchText))));
                                if (row != null) { row.Remove(); break; }
                            }
                        }

                        // 3. الحسابات المالية (Decimal)
                        if (!decimal.TryParse(txtDollarRate.Text, out decimal rate)) rate = 0;
                        decimal exchangeFactor = rate * 10;
                        bool isAjel = btnPaymentStatus.IsChecked == true;
                        string currentDebtText = txtDebtDollar?.Text ?? "0";

                        decimal itemsDollar = Items.Where(i => i.Notes == "$").Sum(i => i.Total);
                        decimal itemsDinar = Items.Where(i => i.Notes == "د.ع").Sum(i => i.Total);

                        decimal prevDebtInDollar = 0;
                        if (isAjel && chkHidePreviousDebt.IsChecked == false)
                        {
                            string cleanDebt = Regex.Replace(currentDebtText, @"[^0-9.]", "");
                            if (decimal.TryParse(cleanDebt, out decimal raw))
                                prevDebtInDollar = currentDebtText.Contains("د.ع") ? (exchangeFactor > 0 ? raw / exchangeFactor : 0) : raw;
                        }

                        // حسبة الدينار: نجمع مواد الدينار + مواد الدولار محولة + الدين السابق محول
                        decimal totalInDinars = itemsDinar + (itemsDollar * exchangeFactor);
                        if (isAjel && chkHidePreviousDebt.IsChecked == false)
                        {
                            string cleanDebt = Regex.Replace(currentDebtText, @"[^0-9.]", "");
                            if (decimal.TryParse(cleanDebt, out decimal raw))
                            {
                                // إذا الدين بالدولار نحوله لدينار، وإذا هو أصلاً دينار ننزله كما هو
                                totalInDinars += currentDebtText.Contains("$") ? (raw * exchangeFactor) : raw;
                            }
                        }

                        // حسبة الدولار (للعرض فقط)
                        decimal totalInDollars = (exchangeFactor > 0) ? (totalInDinars / exchangeFactor) : 0;

                        // 4. ملء جدول المواد
                        var mainTable = document.Tables[0];
                        var patternRow = mainTable.Rows[1];
                        for (int i = 0; i < Items.Count; i++)
                        {
                            var item = Items[i];
                            Row newRow = (i == 0) ? patternRow : mainTable.InsertRow(patternRow, i + 1);
                            string format = (item.Notes == "د.ع") ? "N0" : "N2";

                            FillCell(newRow.Cells[4], (i + 1).ToString(), 12);
                            FillCell(newRow.Cells[3], item.Total.ToString(format) + " " + item.Notes, 12);
                            FillCell(newRow.Cells[2], item.ProductName, 11);
                            FillCell(newRow.Cells[1], $"{item.Qty:G29} {item.Type}", 11);
                            FillCell(newRow.Cells[0], item.Price.ToString(format), 11);
                        }

                        var arabicFormat = new Formatting { FontFamily = new Xceed.Document.NET.Font("Arabic Transparent"), Size = 14, Bold = true };

                        // 5. التفقيط الذكي (حسب اختيار العملة)
                        // 5. التفقيط الذكي (استدعاء المحرك المركزي)
                        decimal amountToWord = (chkShowDollar.IsChecked == true) ? totalInDollars : totalInDinars;
                        string currency = (chkShowDollar.IsChecked == true) ? "دولار أمريكي" : "دينار عراقي";
                        string subCurrency = (chkShowDollar.IsChecked == true) ? "سنت" : "فلس";

                        string finalWords = SiemensApp.Helpers.TafqeetTool.Convert(amountToWord, currency, subCurrency);

                        // 6. التعويضات
                        document.ReplaceText("[Total in dollars]", totalInDollars.ToString("N2") + " $", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Total in dinars]", totalInDinars.ToString("N0") + " د.ع", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[CustomerName]", customerName ?? "", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[InvoiceId]", invoiceId ?? "---", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Date]", originalDate.ToString("yyyy/MM/dd"), false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Title]", txtCustomerAddress.Text ?? "/", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Payment]", isAjel ? "آجل" : "نقدي", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[TotalWriting]", finalWords, false, RegexOptions.None, arabicFormat);

                        // 7. تطبيق "المقص" الذكي
                        if (!isAjel || chkHidePreviousDebt.IsChecked == true) RemoveRowByContent("الحساب السابق");
                        else document.ReplaceText("[Previous account]", currentDebtText, false, RegexOptions.None, arabicFormat);

                        if (chkDefaultMode.IsChecked == false)
                        {
                            if (chkShowDinar.IsChecked == true) RemoveRowByContent("المجموع بل دولار");
                            else if (chkShowDollar.IsChecked == true) RemoveRowByContent("المجموع بل دينار");
                        }

                        if (chkHideWriting.IsChecked == true)
                        {
                            foreach (var table in document.Tables)
                            {
                                var rowToDelete = table.Rows.FirstOrDefault(r => r.Cells.Any(c => c.Paragraphs.Any(p => p.Text.Contains(finalWords))));
                                if (rowToDelete != null) { rowToDelete.Remove(); break; }
                                else if (table == document.Tables.Last()) table.Rows.Last().Remove();
                            }
                        }

                        document.SaveAs(wordOutputPath);
                    }
                }

                // 8. التحويل للـ PDF والفتح التلقائي حجي
                string pdfPath = ConvertWordToPdfUsingLibre(wordOutputPath);
                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                    try { if (File.Exists(wordOutputPath)) File.Delete(wordOutputPath); } catch { }
                }
                else
                {
                    Process.Start(new ProcessStartInfo(wordOutputPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                ShowToast("خطأ بالطباعة والتحويل حجي: " + ex.Message);
            }
        }
        private ObservableCollection<EditorItem> GetProductSuggestions(string query)
        {
            var list = new ObservableCollection<EditorItem>();
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                SELECT ProductName, Price, Currency, Barcode, Quantity, 'Internal' as Source FROM InternalStock 
                WHERE ProductName LIKE @p OR Barcode = @exactP OR BrandName LIKE @p
                UNION ALL
                SELECT ProductName, DefaultPrice, Currency, '' as Barcode, 0 as Quantity, 'Global' as Source FROM GlobalStock 
                WHERE ProductName LIKE @p
                LIMIT 15";

                    cmd.Parameters.AddWithValue("@p", "%" + query + "%");
                    cmd.Parameters.AddWithValue("@exactP", query);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int availableQty = reader.GetInt32(4);
                            string source = reader.GetString(5);
                            string qtyText = source == "Internal" ? $" (متوفر: {availableQty})" : " (خارجي)";

                            list.Add(new EditorItem
                            {
                                ProductName = reader.GetString(0) + qtyText,
                                // حجي التعديل هنا: نستخدم GetDecimal بدلاً من GetDouble
                                Price = reader.GetDecimal(1),
                                Notes = reader.GetString(2),
                                Type = reader.IsDBNull(3) ? "" : reader.GetString(3)
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }
        private void txtInputProduct_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtInputProduct.Text;

            // حجي إذا فرغت الحقل أو كتبت حرف واحد نسد القائمة
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                popSearch.IsOpen = false;
                return;
            }

            // استدعاء دالة البحث اللي تجيب من المخزنين
            var suggestions = GetProductSuggestions(query);

            if (suggestions.Any())
            {
                lstSearchSuggestions.ItemsSource = suggestions;
                popSearch.IsOpen = true;
            }
            else
            {
                popSearch.IsOpen = false;
            }
        }
        private void FillCell(Cell cell, string text, int fontSize)
        {
            if (cell.Paragraphs.Count > 0)
            {
                cell.Paragraphs[0].RemoveText(0);
                cell.Paragraphs[0].Append(text).Font("Arial").FontSize(fontSize).Bold().Alignment = Alignment.center;
            }
        }
        // --- 5. البحث والتنقل ---
        private void txtCustomerName_TextChanged(object sender, TextChangedEventArgs e) { /* كود البحث */ }

        private void txtCustomerName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && popCustomerSearch.IsOpen) { /* اختيار */ }
        }

        private void lstCustomerSuggestions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstCustomerSuggestions.SelectedItem is DebtItem s)
            {
                txtCustomerName.Text = s.DebtorName; popCustomerSearch.IsOpen = false;
            }
        }
        

        private void txtInputProduct_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (popSearch.IsOpen && lstSearchSuggestions.SelectedIndex != -1) SelectSuggestion();
                else txtInputQty.Focus();
            }
        }

        private void lstSearchSuggestions_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SelectSuggestion(); }
        private void lstSearchSuggestions_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SelectSuggestion();

        private void SelectSuggestion()
        {
            if (lstSearchSuggestions.SelectedItem is EditorItem s)
            {
                // حجي هنا السر: نستخدم Regex للتنظيف فوراً عند الاختيار
                string cleanName = Regex.Replace(s.ProductName, @"\s\(.*?\)", "").Trim();

                txtInputProduct.Text = cleanName;
                txtInputPrice.Text = s.Price.ToString();
                popSearch.IsOpen = false;
                txtInputQty.Focus();
            }
        }

        private void BackToArchive_Click(object sender, RoutedEventArgs e)
        {
            // الرجوع لأرشيف الفواتير عبر خدمة التنقّل المُحقَنة
            try
            {
                var listView = App.Host.Services.GetService(typeof(InvoicesListView)) as InvoicesListView;
                var nav = App.Host.Services.GetService(typeof(Services.INavigationService)) as Services.INavigationService;
                if (nav != null && listView != null) { nav.NavigateTo(listView); return; }
                if (Window.GetWindow(this) is MainWindow mw && mw.MainContentFrame != null && listView != null)
                    mw.MainContentFrame.Content = listView;
            }
            catch { /* تجاهل الفشل في التنقّل */ }
        }

        private void ShowToast(string message, string colorHex = "#EF4444")
        {
            Window t = new Window { WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, SizeToContent = SizeToContent.WidthAndHeight, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            t.Content = new System.Windows.Controls.Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)), CornerRadius = new CornerRadius(10), Padding = new Thickness(20), Child = new TextBlock { Text = message, Foreground = Brushes.White, FontWeight = FontWeights.Bold } };
            t.Show(); var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            tm.Tick += (s, ev) => { t.Close(); tm.Stop(); }; tm.Start();
        }

        private void txtInvoiceNumber_TextChanged(object sender, TextChangedEventArgs e) { }
        // --- محرك التحويل الصامت حجي ---
        private string ConvertWordToPdfUsingLibre(string docxPath)
        {
            string sofficePath = FindLibreOfficePath();
            if (string.IsNullOrEmpty(sofficePath)) return "";

            string pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            var psi = new ProcessStartInfo
            {
                FileName = sofficePath,
                Arguments = $"--headless --nologo --nodefault --convert-to pdf --outdir \"{Path.GetDirectoryName(pdfPath)}\" \"{docxPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using (var p = Process.Start(psi)) { p?.WaitForExit(20000); }
                return pdfPath;
            }
            catch { return ""; }
        }

        // --- دالة البحث عن المحرك اللي خليته بصف البرنامج حجي ---
        private string FindLibreOfficePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string portable = Path.Combine(baseDir, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(portable)) return portable;

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe"))
                {
                    if (key != null) return key.GetValue("")?.ToString();
                }
            }
            catch { }

            string[] common = { @"C:\Program Files\LibreOffice\program\soffice.exe", @"C:\Program Files (x86)\LibreOffice\program\soffice.exe" };
            return common.FirstOrDefault(File.Exists);
        }
    }
}