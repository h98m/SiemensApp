using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Image;
using Microsoft.Data.Sqlite;
using Org.BouncyCastle.Utilities.Encoders;
using System;
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
using Xceed.Document.NET;
using Xceed.Words.NET;
using System.Windows.Media.Animation;
namespace SiemensApp.Views
{
    public class InvoiceItem : INotifyPropertyChanged
    {
        private string _productName = "";
        private decimal _qty = 1;
        private decimal _price = 0;
        private string _notes = "د.ع"; // هذي اللي تمثل العملة حجي

        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); }
        }

        public decimal Qty
        {
            get => _qty;
            set { _qty = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
        }

        public decimal Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
        }

        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public string Type { get; set; } = "قطعة";

        // الإجمالي يتحدث تلقائياً عند تغيير السعر أو الكمية
        public decimal Total => Qty * Price;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public partial class InvoiceView : UserControl
    {
        // هذا الكود يحدد مسار المجلد اللي يشتغل منه البرنامج حالياً
        private static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        // ندمج مسار المجلد مع اسم قاعدة البيانات
        private string dbPath = $"Data Source={System.IO.Path.Combine(currentDirectory, "SiemensData.db")}"; private string selectedInvoiceType = "وصل المحل"; // القيمة الافتراضية
        public ObservableCollection<InvoiceItem> Items { get; set; } = new ObservableCollection<InvoiceItem>();

        public InvoiceView()
        {
            InitializeComponent();
            InitializeDatabase();
            dgvInvoiceItems.ItemsSource = Items;

            // حجي هنا نجيب الحقل ونحط بيه الرقم (1 أو 2 أو 3...)
            var txtInv = this.FindName("txtInvoiceNumber") as TextBox;
            if (txtInv != null)
            {
                txtInv.Text = GetSimpleNextNumber();
            }
        }
        public void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                // جدول سجل الأسماء العام - حجي هذا ما له علاقة بالديون
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS CustomersList (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerName TEXT UNIQUE,
    Phone TEXT,
    Address TEXT
);";
                cmd.ExecuteNonQuery();
                // 1. جدول الفواتير
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS AppSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT
);";
                cmd.ExecuteNonQuery();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Invoices (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, 
    InvoiceNumber TEXT, 
    CustomerName TEXT, 
    Phone TEXT, 
    Address TEXT, 
    Date DATETIME, 
    TotalAmount NUMERIC,     -- تمام
    TotalAmountDollar NUMERIC, -- ضيفه إذا تحتاجه بدقة
    PaymentStatus TEXT, 
    InvoiceType TEXT, 
    DollarRate NUMERIC,      -- حوله NUMERIC
    PreviousDebt NUMERIC,    -- حوله NUMERIC (كلش مهم)
    DebtInIQD NUMERIC,       -- حوله NUMERIC
    Currency TEXT
);";
                cmd.ExecuteNonQuery();

                // 2. جدول المخزن الداخلي
                cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS InternalStock (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductName TEXT UNIQUE,
            Quantity INTEGER DEFAULT 0,
            Price REAL,
            Currency TEXT,
            Barcode TEXT,
            BrandName TEXT
        );";
                cmd.ExecuteNonQuery();

                // 3. جدول المخزن الخارجي (GlobalStock)
                cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS GlobalStock (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductName TEXT UNIQUE,
            DefaultPrice REAL,
            Currency TEXT DEFAULT 'دينار عراقي',
            Category TEXT
        );";
                cmd.ExecuteNonQuery();

                // 4. جدول تفاصيل الفاتورة
                cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS InvoiceDetails (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            InvoiceId INTEGER,
            ProductName TEXT,
            Price REAL,
            Qty REAL,
            Total REAL,
            UnitType TEXT,
            Currency TEXT,
            FOREIGN KEY(InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
        );";
                cmd.ExecuteNonQuery();

                // 5. جدول الديون
                cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS DebtsMe (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DebtorName TEXT UNIQUE,
            TotalAmount REAL DEFAULT 0,
            Currency TEXT DEFAULT 'دينار عراقي',
            LastTransactionDate DATETIME
        );";
                cmd.ExecuteNonQuery();

                // حجي هذا السطر هو "التأمين" - يضمن وجود الـ UNIQUE على اسم الزبون لحل مشكلة الـ Conflict
                cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_debtor_name_unique ON DebtsMe (DebtorName);";
                cmd.ExecuteNonQuery();

                // 6. جدول تاريخ أسعار الزبائن
                cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS CustomerPriceHistory (
            CustomerName TEXT,
            ProductName TEXT,
            LastPrice REAL,
            PRIMARY KEY (CustomerName, ProductName)
        );";
                cmd.ExecuteNonQuery();

                // تحديث الأعمدة لضمان التوافق
                // تحديث الأعمدة لضمان التوافق حجي - حولناهن NUMERIC للدقة
                TryAddColumn(connection, "Invoices", "InvoiceNumber", "TEXT");
                TryAddColumn(connection, "Invoices", "Address", "TEXT");
                TryAddColumn(connection, "Invoices", "PaymentStatus", "TEXT");
                TryAddColumn(connection, "Invoices", "DollarRate", "NUMERIC"); // تعديل هنا
                TryAddColumn(connection, "Invoices", "PreviousDebt", "NUMERIC"); // وتعديل هنا
                TryAddColumn(connection, "Invoices", "Currency", "TEXT");
                TryAddColumn(connection, "GlobalStock", "Currency", "TEXT");
                TryAddColumn(connection, "DebtsMe", "LastTransactionDate", "DATETIME");
                // حجي أضف هاي الأسطر حتى يحل مشكلة العمود المفقود
                TryAddColumn(connection, "Invoices", "IsDollarMode", "INTEGER DEFAULT 0");
                TryAddColumn(connection, "Invoices", "IsHideWriting", "INTEGER DEFAULT 0");
                TryAddColumn(connection, "Invoices", "IsDefaultMode", "INTEGER DEFAULT 1");
            }
        }

        // دالة مساعدة حجي حتى ما يضرب البرنامج إذا العمود موجود اصلاً
        private void TryAddColumn(SqliteConnection conn, string tableName, string columnName, string type)
        {
            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {type};";
                cmd.ExecuteNonQuery();
            }
            catch { /* إذا العمود موجود راح يطلع خطأ فنسوي له تجاهل */ }
        }

        



        // حجي هاي تخليها مرة وحدة بس بنهاية الكلاس
        // قمنا بتغيير Cell إلى Xceed.Document.NET.Cell لفض الاشتباك
        private void FillCell(Xceed.Document.NET.Cell cell, string text, int fontSize)
        {
            if (cell.Paragraphs.Count > 0)
            {
                var p = cell.Paragraphs[0];
                p.RemoveText(0, p.Text.Length);
                p.Append(text).Font("Arial").FontSize(fontSize).Bold().Alignment = Alignment.center;

                // الحل هنا: نكتب المسار الكامل للـ Enum حتى لا يختلط الأمر على المترجم
                cell.VerticalAlignment = Xceed.Document.NET.VerticalAlignment.Center;
            }
        }
        // حجي غيرنا النوع من void إلى string حتى نكدر نستخدم المسار بتحويل الـ PDF
        private string ExportToWord(string customerName, string invoiceId)
        {
            try
            {
                // 1. تحديد المسارات والقوالب
                string templateFileName = selectedInvoiceType == "وصل محل اجراس" ? "Template1.docx" :
                          selectedInvoiceType == "وصل محل عصام" ? "Template2.docx" :
                          selectedInvoiceType == "وصل محل لمسة التكنلوحيا" ? "Template3.docx" : 
                          selectedInvoiceType == "وصل محل المعين" ? "Template4.docx" : "Template.docx";// هنا تنتهي الجملة

                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, templateFileName);
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string pdfFolderPath = Path.Combine(desktopPath, "pdf");
                if (!Directory.Exists(pdfFolderPath)) Directory.CreateDirectory(pdfFolderPath);

                // 2. تحديد الاسم الذي سيظهر في الملف المحفوظ
                
                string displayInvoiceName = selectedInvoiceType == "وصل محل اجراس" ? "وصل محل اجراس" :
                            selectedInvoiceType == "وصل محل عصام" ? "وصل محل عصام" :
                            selectedInvoiceType == "وصل محل لمسة التكنلوحيا" ? "وصل محل لمسة التكنلوحيا" :
                            selectedInvoiceType == "وصل محل المعين" ? "وصل محل المعين" : selectedInvoiceType;
                string safeFileName = Regex.Replace(customerName, @"[\\/:*?""<>|]", "_");
                string timeStamp = DateTime.Now.ToString("HH-mm");
                string fileName = $"{displayInvoiceName}_{safeFileName}_{invoiceId}_{timeStamp}.docx";
                string finalOutputPath = Path.Combine(pdfFolderPath, fileName);

                using (var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (DocX document = DocX.Load(templateStream))
                    {
                        // دالة الحذف بالبحث عن النص لضمان القص الدقيق
                        void RemoveRowByContent(string searchText)
                        {
                            foreach (var table in document.Tables)
                            {
                                var row = table.Rows.FirstOrDefault(r => r.Cells.Any(c => c.Paragraphs.Any(p => p.Text.Contains(searchText))));
                                if (row != null) { row.Remove(); break; }
                            }
                        }

                        // 2. الحسابات المالية
                        decimal.TryParse(txtDollarRate.Text, out decimal rate);
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

                        // 3. ملء جدول المواد
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

                        // 4. تحضير نص التفقيط (استخدام دالتك حجي)
                        decimal amountToWord = (chkShowDollar.IsChecked == true) ? totalInDollars : totalInDinars;
                        string currency = (chkShowDollar.IsChecked == true) ? "دولار أمريكي" : "دينار عراقي";
                        string subCurrency = (chkShowDollar.IsChecked == true) ? "سنت" : "فلس";

                        // استدعاء المحرك الجديد مع معالجة الكسور
                        string finalWords = SiemensApp.Helpers.TafqeetTool.Convert(amountToWord, currency, subCurrency);

                        // 5. التعويض (Replace) في المستند
                        document.ReplaceText("[Total in dollars]", totalInDollars.ToString("N2") + " $", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Total in dinars]", totalInDinars.ToString("N0") + " د.ع", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[CustomerName]", customerName ?? "", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[InvoiceId]", invoiceId?.Trim() ?? "---", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Date]", DateTime.Now.ToString("yyyy/MM/dd"), false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Title]", txtCustomerAddress.Text ?? "/", false, RegexOptions.None, arabicFormat);
                        document.ReplaceText("[Payment]", isAjel ? "آجل" : "نقدي", false, RegexOptions.None, arabicFormat);

                        // تعويض كلمة التفقيط حجي
                        document.ReplaceText("[TotalWriting]", finalWords, false, RegexOptions.None, arabicFormat);

                        // 6. تطبيق "المقص" (الحذف)

                        // أ. الحساب السابق
                        if (!isAjel || chkHidePreviousDebt.IsChecked == true)
                        {
                            RemoveRowByContent("الحساب السابق");
                        }
                        else
                        {
                            document.ReplaceText("[Previous account]", currentDebtText, false, RegexOptions.None, arabicFormat);
                        }

                        // ب. فرز العملات
                        if (chkDefaultMode.IsChecked == false)
                        {
                            if (chkShowDinar.IsChecked == true) RemoveRowByContent("المجموع بل دولار");
                            else if (chkShowDollar.IsChecked == true) RemoveRowByContent("المجموع بل دينار");
                        }

                        // ج. حذف سطر التفقيط إذا تم اختيار "إخفاء"
                        if (chkHideWriting.IsChecked == true)
                        {
                            // نبحث عن السطر الذي يحتوي على النص الذي عوضناه ونحذفه
                            foreach (var table in document.Tables)
                            {
                                var rowToDelete = table.Rows.FirstOrDefault(r => r.Cells.Any(c => c.Paragraphs.Any(p => p.Text.Contains(finalWords))));
                                if (rowToDelete != null) { rowToDelete.Remove(); break; }
                                // إذا لم يجد النص (احتياطاً) يحذف السطر الأخير
                                else if (table == document.Tables.Last()) table.Rows.Last().Remove();
                            }
                        }

                        document.SaveAs(finalOutputPath);
                    }
                }
                return finalOutputPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ بالطباعة حجي: " + ex.Message);
                return "";
            }
        }
        
        private void btnPaymentStatus_Checked(object sender, RoutedEventArgs e)
        {
 
        }
       
        private void btnPaymentStatus_Unchecked(object sender, RoutedEventArgs e)
        {

        }
        private void ShowToast(string message, string colorHex = "#EF4444")
        {
            Window toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(25, 12, 25, 12),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 15, Opacity = 0.2 },
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    }
                }
            };
            toast.Show();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, e) => { toast.Close(); timer.Stop(); };
            timer.Start();
        }

        private void txtInputProduct_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtInputProduct.Text;
            if (query.Length > 1)
            {
                var suggestions = GetProductSuggestions(query);
                if (suggestions.Any())
                {
                    lstSearchSuggestions.ItemsSource = suggestions;
                    popSearch.IsOpen = true;
                }
                else { popSearch.IsOpen = false; }
            }
            else { popSearch.IsOpen = false; }
        }
        private ObservableCollection<DebtItem> GetCustomerSuggestions(string query)
        {
            var list = new ObservableCollection<DebtItem>();
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();

                    // حجي هنا نستخدم UNION للبحث في الجدولين سوا بدون تكرار الأسماء
                    cmd.CommandText = @"
                SELECT CustomerName FROM CustomersList WHERE CustomerName LIKE @p
                UNION
                SELECT DebtorName FROM DebtsMe WHERE DebtorName LIKE @p
                LIMIT 15";

                    cmd.Parameters.AddWithValue("@p", "%" + query + "%");

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new DebtItem { DebtorName = reader.GetString(0) });
                        }
                    }
                }
            }
            catch { }
            return list;
        }
        private void SelectCustomerSuggestion()
        {
            if (lstCustomerSuggestions.SelectedItem is DebtItem selected)
            {
                txtCustomerName.Text = selected.DebtorName;
                popCustomerSearch.IsOpen = false;

                // جلب الهاتف والعنوان من السجل العام
                FillCustomerDetails(selected.DebtorName);

                // جلب الدين من جدول الديون
                GetCustomerDebtFromDb(selected.DebtorName);

                txtInputProduct.Focus();
            }
        }
        private void FillCustomerDetails(string name)
        {
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT Phone, Address FROM CustomersList WHERE CustomerName = @n";
                    cmd.Parameters.AddWithValue("@n", name);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtCustomerPhone.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            txtCustomerAddress.Text = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        }
                    }
                }
            }
            catch { }
        }
        private void lstCustomerSuggestions_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SelectCustomerSuggestion();
        private void txtCustomerName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && popCustomerSearch.IsOpen) { lstCustomerSuggestions.Focus(); }
            if (e.Key == Key.Enter && popCustomerSearch.IsOpen) { SelectCustomerSuggestion(); }
        }
        private ObservableCollection<InvoiceItem> GetProductSuggestions(string query)
        {
            var list = new ObservableCollection<InvoiceItem>();
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();

                    // حجي هنا الاستعلام يبحث بالجدولين ويجيب البيانات سوا
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
                            string source = reader.GetString(5); // نعرف المادة جاية من أي مخزن

                            // تنبيه بسيط حجي: إذا المادة من المخزن الخارجي نعطي إشارة
                            string qtyText = source == "Internal" ? $" (متوفر: {availableQty})" : " (خارجي)";

                            // داخل دالة GetProductSuggestions
                            // السطر 507 تقريباً
                            list.Add(new InvoiceItem
                            {
                                ProductName = reader.GetString(0),
                                Price = reader.GetDecimal(1), // تعديل من GetDouble إلى GetDecimal
                                Notes = reader.GetString(2),
                                Type = availableQty > 0 ? $" (متوفر: {availableQty})" : ""
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }
        private void SelectSuggestion()
        {
            if (lstSearchSuggestions.SelectedItem is InvoiceItem selected)
            {
                txtInputProduct.Text = selected.ProductName;

                // جلب آخر سعر للزبون أو السعر الافتراضي
                decimal lastPrice = GetLastPriceForCustomer(txtCustomerName.Text, selected.ProductName);
                txtInputPrice.Text = (lastPrice > 0 ? lastPrice : selected.Price).ToString("N0");
                // تحويل العملة تلقائياً بناءً على بيانات المخزن
                if (selected.Notes == "دولار أمريكي" || selected.Notes == "$")
                    btnCurrencyToggle.IsChecked = true;
                else
                    btnCurrencyToggle.IsChecked = false;

                Currency_Changed(null, null);

                popSearch.IsOpen = false;
                txtInputQty.Focus();
                txtInputQty.SelectAll(); // حتى تكتب الكمية مباشرة فوق الـ 1
            }
        }
        private decimal GetLastPriceForCustomer(string customerName, string productName) // غيرنا النوع هنا
        {
            decimal price = 0; // غيرنا النوع هنا
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT LastPrice FROM CustomerPriceHistory WHERE CustomerName = @cn AND ProductName = @pn";
                    cmd.Parameters.AddWithValue("@cn", customerName);
                    cmd.Parameters.AddWithValue("@pn", productName);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        price = Convert.ToDecimal(result); // تحويل لديسيمال
                }
            }
            catch { }
            return price;
        }



        private void BtnSaveInvoice_Click(object sender, RoutedEventArgs e)
        {
            // 1. جلب العناصر والتأكد من البيانات
            var txtInv = this.FindName("txtInvoiceNumber") as TextBox;
            var txtName = this.FindName("txtCustomerName") as TextBox;
            var txtRate = this.FindName("txtDollarRate") as TextBox;
            var txtPhone = this.FindName("txtCustomerPhone") as TextBox;
            var txtAddress = this.FindName("txtCustomerAddress") as TextBox;
            var txtPrevDebtField = this.FindName("txtDebtDollar") as TextBox;

            if (string.IsNullOrWhiteSpace(txtName?.Text)) { ShowToast("حجي، اكتب اسم الزبون!"); return; }
            if (Items == null || Items.Count == 0) { ShowToast("الفاتورة فارغة حجي!"); return; }

            // تحديث المجاميع قبل البدء لضمان دقة الأرقام
            UpdateGrandTotal();

            string invNum = !string.IsNullOrWhiteSpace(txtInv?.Text) ? txtInv.Text : GetSimpleNextNumber();
            string debtText = txtPrevDebtField?.Text ?? "0";
            string cleanDebt = Regex.Replace(debtText, @"[^0-9.]", "");
            if (!decimal.TryParse(cleanDebt, out decimal previousDebtValue)) previousDebtValue = 0;

            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                using (var tran = connection.BeginTransaction())
                {
                    try
                    {
                        decimal.TryParse(txtRate?.Text ?? "0", out decimal rateInput);
                        decimal exchangeFactor = rateInput * 10;

                        // حساب صافي الفاتورة (بدون ديون سابقة)
                        decimal itemsDinar = Items.Where(i => i.Notes == "د.ع").Sum(i => i.Total);
                        decimal itemsDollar = Items.Where(i => i.Notes == "$").Sum(i => i.Total);
                        decimal totalInvoiceDinar = itemsDinar + (itemsDollar * exchangeFactor);
                        decimal totalInvoiceDollar = (exchangeFactor > 0) ? totalInvoiceDinar / exchangeFactor : 0;

                        // 2. حفظ رأس الفاتورة
                        var cmd = connection.CreateCommand();
                        cmd.Transaction = tran;
                        cmd.CommandText = @"INSERT INTO Invoices 
                (InvoiceNumber, CustomerName, Phone, Address, Date, TotalAmount, TotalAmountDollar, PaymentStatus, InvoiceType, DollarRate, PreviousDebt, Currency, IsDollarMode, IsHideWriting, IsDefaultMode) 
                VALUES (@inv, @n, @p, @addr, @d, @t, @td, @ps, @it, @dr, @prev, 'دينار عراقي', @idm, @ihw, @idfm);
                SELECT last_insert_rowid();";

                        cmd.Parameters.AddWithValue("@inv", invNum);
                        cmd.Parameters.AddWithValue("@n", txtName.Text);
                        cmd.Parameters.AddWithValue("@p", txtPhone?.Text ?? "");
                        cmd.Parameters.AddWithValue("@addr", txtAddress?.Text ?? "");
                        cmd.Parameters.AddWithValue("@d", DateTime.Now);
                        cmd.Parameters.AddWithValue("@t", Math.Round(totalInvoiceDinar, 0));
                        cmd.Parameters.AddWithValue("@td", Math.Round(totalInvoiceDollar, 2));
                        cmd.Parameters.AddWithValue("@ps", (btnPaymentStatus?.IsChecked == true) ? "آجل" : "نقدي");
                        cmd.Parameters.AddWithValue("@it", selectedInvoiceType ?? "وصل المحل");
                        cmd.Parameters.AddWithValue("@dr", rateInput);
                        cmd.Parameters.AddWithValue("@prev", previousDebtValue);
                        cmd.Parameters.AddWithValue("@idm", chkShowDollar.IsChecked == true ? 1 : 0);
                        cmd.Parameters.AddWithValue("@ihw", chkHideWriting.IsChecked == true ? 1 : 0);
                        cmd.Parameters.AddWithValue("@idfm", chkDefaultMode.IsChecked == true ? 1 : 0);

                        long invId = Convert.ToInt64(cmd.ExecuteScalar());

                        // 3. حفظ التفاصيل وخصم المخزن (المرحلة المهمة حجي)
                        foreach (var item in Items)
                        {
                            // حفظ المادة
                            var cmdD = connection.CreateCommand();
                            cmdD.Transaction = tran;
                            cmdD.CommandText = @"INSERT INTO InvoiceDetails (InvoiceId, ProductName, Price, Qty, Total, UnitType, Currency) 
                                        VALUES (@id, @pn, @pr, @q, @t, @ut, @cur)";
                            cmdD.Parameters.AddWithValue("@id", invId);
                            cmdD.Parameters.AddWithValue("@pn", item.ProductName);
                            cmdD.Parameters.AddWithValue("@pr", item.Price);
                            cmdD.Parameters.AddWithValue("@q", item.Qty);
                            cmdD.Parameters.AddWithValue("@t", item.Total);
                            cmdD.Parameters.AddWithValue("@ut", item.Type);
                            cmdD.Parameters.AddWithValue("@cur", item.Notes);
                            cmdD.ExecuteNonQuery();

                            // خصم من المخزن الداخلي حجي
                            var cmdStock = connection.CreateCommand();
                            cmdStock.Transaction = tran;
                            cmdStock.CommandText = "UPDATE InternalStock SET Quantity = Quantity - @q WHERE ProductName = @pn";
                            cmdStock.Parameters.AddWithValue("@q", item.Qty);
                            cmdStock.Parameters.AddWithValue("@pn", item.ProductName);
                            cmdStock.ExecuteNonQuery();
                        }

                        // 4. تحديث مديونية الزبون
                        if (btnPaymentStatus.IsChecked == true)
                        {
                            // ابحث عن هذا السطر داخل BtnSaveInvoice_Click وغيره:
                            UpdateDebtsLogic(connection, tran, txtName.Text, totalInvoiceDinar, totalInvoiceDollar, rateInput, invNum);
                        }

                        // 5. حفظ إعدادات البرنامج العامة
                        SaveAppSetting(connection, tran, "LastRate", txtRate.Text);

                        tran.Commit();

                        // 6. الطباعة والتحويل للـ PDF
                        string wordPath = ExportToWord(txtName.Text, invNum);
                        if (!string.IsNullOrEmpty(wordPath))
                        {
                            string pdfPath = ConvertWordToPdfUsingLibre(wordPath);
                            if (File.Exists(pdfPath))
                            {
                                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                            }
                            else
                            {
                                Process.Start(new ProcessStartInfo(wordPath) { UseShellExecute = true });
                            }
                        }

                        ShowToast("تم الحفظ والطباعة بنجاح حجي ✅", "#10B981");
                        ResetUI();
                    }
                    catch (Exception ex)
                    {
                        if (tran.Connection != null) tran.Rollback();
                        MessageBox.Show("صار خطأ حجي: " + ex.Message);
                    }
                }
            }
        }

        // دالة مساعدة لحفظ الإعداد بجدول الـ Settings
        private void SaveAppSetting(SqliteConnection conn, SqliteTransaction tran, string key, string value)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = "INSERT OR REPLACE INTO AppSettings (SettingKey, SettingValue) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value ?? "");
            cmd.ExecuteNonQuery();
        }

        // دالة مساعدة لفصل منطق الديون حجي حتى الكود يبقى نظيف
        private void UpdateDebtsLogic(SqliteConnection conn, SqliteTransaction tran, string name, decimal invoiceDinar, decimal invoiceDollar, decimal exchange, string invNum)
        {
            decimal oldDebtFromDB = 0;
            string currentCurrency = "د.ع";
            int debtorId = 0;

            // 1. جلب البيانات الحالية (الرصيد والـ ID والعملة)
            var cmdGet = conn.CreateCommand();
            cmdGet.Transaction = tran;
            cmdGet.CommandText = "SELECT Id, TotalAmount, Currency FROM DebtsMe WHERE DebtorName = @name";
            cmdGet.Parameters.AddWithValue("@name", name);

            using (var reader = cmdGet.ExecuteReader())
            {
                if (reader.Read())
                {
                    debtorId = reader.GetInt32(0);
                    oldDebtFromDB = reader.GetDecimal(1);
                    currentCurrency = reader.GetString(2);
                }
            }

            // 2. حسبة الرصيد الجديد
            decimal finalToSave = 0;
            decimal amountToLog = 0; // المبلغ اللي راح يتسجل بالسجل
            string logCurrency = currentCurrency;

            if (currentCurrency.Contains("$"))
            {
                finalToSave = Math.Round(oldDebtFromDB + invoiceDollar, 2);
                amountToLog = invoiceDollar;
            }
            else
            {
                finalToSave = Math.Round(oldDebtFromDB + invoiceDinar, 0);
                amountToLog = invoiceDinar;
            }

            // 3. تحديث أو إضافة المديون
            var cmdUpdate = conn.CreateCommand();
            cmdUpdate.Transaction = tran;
            cmdUpdate.CommandText = @"
        INSERT INTO DebtsMe (DebtorName, TotalAmount, Currency, LastTransactionDate) 
        VALUES (@name, @amt, @cur, @dt) 
        ON CONFLICT(DebtorName) 
        DO UPDATE SET TotalAmount = @amt, LastTransactionDate = @dt;";

            cmdUpdate.Parameters.AddWithValue("@amt", finalToSave);
            cmdUpdate.Parameters.AddWithValue("@name", name);
            cmdUpdate.Parameters.AddWithValue("@cur", currentCurrency);
            cmdUpdate.Parameters.AddWithValue("@dt", DateTime.Now);
            cmdUpdate.ExecuteNonQuery();

            // 4. ⭐ إذا المديون جديد، نحتاج نجيب الـ ID ماله حتى نسجل الحركة
            if (debtorId == 0)
            {
                var cmdGetNewId = conn.CreateCommand();
                cmdGetNewId.Transaction = tran;
                cmdGetNewId.CommandText = "SELECT Id FROM DebtsMe WHERE DebtorName = @name";
                cmdGetNewId.Parameters.AddWithValue("@name", name);
                debtorId = Convert.ToInt32(cmdGetNewId.ExecuteScalar());
            }

            // 5. ⭐ تسجيل العملية في جدول الحركات (التلقائي)
            SaveActionLog(conn, tran, debtorId, "فاتورة جديدة", amountToLog, logCurrency, $"فاتورة آجل رقم: {invNum}");
        }
        private void txtInputProduct_KeyDown(object sender, KeyEventArgs e)
        {
            // حجي هذا الكود يخليك تنتقل بين الاقتراحات بالأسهم وتختار بالـ Enter
            if (popSearch.IsOpen && lstSearchSuggestions.Items.Count > 0)
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    int nextIndex = lstSearchSuggestions.SelectedIndex + 1;
                    if (nextIndex < lstSearchSuggestions.Items.Count)
                    {
                        lstSearchSuggestions.SelectedIndex = nextIndex;
                        lstSearchSuggestions.ScrollIntoView(lstSearchSuggestions.SelectedItem);
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    int prevIndex = lstSearchSuggestions.SelectedIndex - 1;
                    if (prevIndex >= 0)
                    {
                        lstSearchSuggestions.SelectedIndex = prevIndex;
                        lstSearchSuggestions.ScrollIntoView(lstSearchSuggestions.SelectedItem);
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    if (lstSearchSuggestions.SelectedIndex != -1) { e.Handled = true; SelectSuggestion(); }
                }
            }
            else if (e.Key == Key.Enter)
            {
                // إذا الاقتراحات مسدودة، ينقلك للحقل اللي وراه
                e.Handled = true;
                MoveToNextField_KeyDown(sender, e);
            }
        }

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            // حجي هنا التعديل: حولنا النوع إلى InvoiceItem حتى يطابق القائمة مالتك
            var item = (sender as Button).DataContext as InvoiceItem;

            if (item != null)
            {
                // 1. ترجيع البيانات لحقول الإدخال فوق حتى تعدل عليها
                txtInputProduct.Text = item.ProductName;
                txtInputPrice.Text = item.Price.ToString();
                // بدل ToString العادية، نستخدم G29
                txtInputQty.Text = item.Qty.ToString("G29");
                txtInputType.Text = item.Type;

                // 2. تحديث حالة زر العملة (دولار أو دينار)
                // حجي هنا فحصنا إذا الرمز هو $ نفتح زر الدولار
                btnCurrencyToggle.IsChecked = (item.Notes == "$");

                // استدعاء دالة تغيير العملة برمجياً لتحديث الألوان والرموز بالواجهة
                Currency_Changed(null, null);

                // 3. حذف المادة من الجدول مؤقتاً لحد ما تضغط "إضافة" مرة ثانية بعد التعديل
                Items.Remove(item);

                // 4. تحديث المجموع النهائي للوصل
                UpdateGrandTotal();

                // 5. حجي نخلي التركيز على حقل الكمية فوراً حتى يسهل التعديل
                txtInputQty.Focus();
                txtInputQty.SelectAll();
            }
        }

        private void btnAddRow_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrEmpty(txtInputProduct.Text)) return;
            // حجي نقفل سعر الصرف بمجرد إضافة أول مادة
            txtDollarRate.IsEnabled = false;
            string productName = Regex.Replace(txtInputProduct.Text, @"\s\(المتوفر:.*?\)", "").Trim();

            // استخدام InvariantCulture لضمان قراءة الكسور مثل 1.5 بشكل صحيح
            if (!decimal.TryParse(txtInputQty.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal q)) q = 1;

            // تنظيف النص من الفواصل العادية (فاصلة الآلاف) مع إبقاء النقطة العشرية
            // حجي أنت مستخدم هذا الكود وهو صحيح جداً للفواصل
            string priceText = txtInputPrice.Text.Replace(",", ""); if (!decimal.TryParse(priceText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price)) price = 0;

            string currentSymbol = btnCurrencyToggle.IsChecked == true ? "$" : "د.ع";

            using (var connection = new SqliteConnection(dbPath))
            {
                connection.Open();
                var cmdCheck = connection.CreateCommand();
                cmdCheck.CommandText = @"SELECT (SELECT COUNT(*) FROM InternalStock WHERE ProductName = @pn) + 
                                       (SELECT COUNT(*) FROM GlobalStock WHERE ProductName = @pn)";
                cmdCheck.Parameters.AddWithValue("@pn", productName);
                long totalExists = (long)cmdCheck.ExecuteScalar();

                if (totalExists == 0)
                {
                    var cmdSaveGlobal = connection.CreateCommand();
                    cmdSaveGlobal.CommandText = "INSERT INTO GlobalStock (ProductName, DefaultPrice, Currency) VALUES (@pn, @pr, @cur)";
                    cmdSaveGlobal.Parameters.AddWithValue("@pn", productName);
                    cmdSaveGlobal.Parameters.AddWithValue("@pr", price);
                    cmdSaveGlobal.Parameters.AddWithValue("@cur", (currentSymbol == "$" ? "دولار أمريكي" : "دينار عراقي"));
                    cmdSaveGlobal.ExecuteNonQuery();
                }

                var cmdInternal = connection.CreateCommand();
                cmdInternal.CommandText = "SELECT Quantity FROM InternalStock WHERE ProductName = @pn";
                cmdInternal.Parameters.AddWithValue("@pn", productName);
                var stockObj = cmdInternal.ExecuteScalar();

                if (stockObj != null && stockObj != DBNull.Value)
                {
                    decimal available = Convert.ToDecimal(stockObj); // تحويل لديسيمال
                    if (q > available)
                    {
                        var result = MessageBox.Show($"حجي المتوفر بالمخزن الداخلي هو ({available}) قطعة فقط.\n\n" +
                            $"هل تريد بيع الكمية المطلوبة كاملة ({q}) وتجهيز النقص من المخزن الخارجي؟",
                            "تنبيه المخزن", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.No)
                        {
                            q = available;
                            txtInputQty.Text = available.ToString();
                        }
                    }
                }
            }

            var existingItem = Items.FirstOrDefault(i => i.ProductName == productName && i.Notes == currentSymbol);
            if (existingItem != null)
            {
                existingItem.Qty += q;
                dgvInvoiceItems.Items.Refresh();
            }
            else
            {
                Items.Add(new InvoiceItem
                {
                    ProductName = productName,
                    Price = price,
                    Qty = q,
                    Notes = currentSymbol,
                    Type = txtInputType.Text
                });
            }

            txtInputProduct.Text = "";
            txtInputPrice.Text = "";
            txtInputQty.Text = "1";
            txtInputProduct.Focus();
            UpdateGrandTotal();
        }
        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (dgvInvoiceItems.SelectedItem is InvoiceItem item)
            {
                Items.Remove(item);
                UpdateGrandTotal();

                // إذا فرغت السلة، نرجع نفتح سعر الصرف حجي
                if (Items.Count == 0) txtDollarRate.IsEnabled = true;
            }
        }

        private void UpdateGrandTotal()
        {
            if (txtDollarRate == null || txtDebtDollar == null || lblTotal == null || lblTotalDollar == null || Items == null)
                return;

            try
            {
                if (!decimal.TryParse(txtDollarRate.Text, out decimal rate)) rate = 0;
                decimal exchangeFactor = rate * 10;

                // 1. حساب مجموع المواد الحالية فقط
                decimal itemsDollar = Items.Where(i => i.Notes == "$").Sum(i => i.Total);
                decimal itemsDinar = Items.Where(i => i.Notes == "د.ع" || i.Notes == "IQ د.عراقي").Sum(i => i.Total);

                // 2. معالجة الدين السابق (الشرط هنا حجي)
                decimal prevDebtInDinar = 0;

                // نشيك إذا الزر مال الحالة "آجل" مفعل، يلا نجمع الدين السابق
                if (btnPaymentStatus != null && btnPaymentStatus.IsChecked == true)
                {
                    string debtText = txtDebtDollar.Text;
                    string cleanAmount = Regex.Replace(debtText, @"[^0-9.]", "");
                    if (decimal.TryParse(cleanAmount, out decimal prevDebtRaw))
                    {
                        prevDebtInDinar = debtText.Contains("$") ? (prevDebtRaw * exchangeFactor) : prevDebtRaw;
                    }
                }

                // 3. الحسابات النهائية
                // المجموع الكلي = (مواد بالدولار محولة للدينار) + مواد بالدينار + الدين السابق (إذا كان آجل)
                decimal finalTotalDinar = (itemsDollar * exchangeFactor) + itemsDinar + prevDebtInDinar;

                // التقريب لضمان عدم نقص الدينار (الحل اللي سويناه قبل شوية)
                finalTotalDinar = Math.Round(finalTotalDinar, 0, MidpointRounding.AwayFromZero);

                decimal finalTotalDollar = (exchangeFactor > 0) ? (finalTotalDinar / exchangeFactor) : 0;
                finalTotalDollar = Math.Round(finalTotalDollar, 2, MidpointRounding.AwayFromZero);

                // 4. تحديث الواجهة
                lblTotal.Text = finalTotalDinar.ToString("N0");
                lblTotalDollar.Text = finalTotalDollar.ToString("N2");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("خطأ في الحساب حجي: " + ex.Message);
            }
        }
        private void MoveToNextField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                (sender as UIElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
        // حجي تأكد أن الدالة "public" أو "private" وموجودة بداخل كلاس InvoiceView
        // حجي غير النوع هنا إلى RoutedEventArgs حتى يقبله الـ CheckBox والـ TextBox سوة
        private void UpdateGrandTotal_Event(object sender, RoutedEventArgs e)
        {
            // نتأكد أن العناصر محملة حتى ما يضرب البرنامج أول ما يفتح
            if (txtDollarRate == null || txtDebtDollar == null) return;

            UpdateGrandTotal();
        }



        private string FindLibreOfficePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // 1. النسخة المحمولة (تأكد أن المجلد بهذا الاسم بالضبط)
            string portable = Path.Combine(baseDir, "LibreOffice", "program", "soffice.exe");

            if (File.Exists(portable)) return portable;

            // إذا ما لكاه، نطلع مسج تنبيهي حتى نعرف وين الخلل
            MessageBox.Show($"أستاذ عقيل، ملف التشغيل غير موجود في:\n{portable}", "تنبيه المسار");

            // 2. البحث في السجل (Registry) - يحتاج أحياناً صلاحيات مسؤول
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe"))
                {
                    if (key != null) return key.GetValue("")?.ToString();
                }
            }
            catch { }

            // 3. المسارات الافتراضية
            string[] common = {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    };
            return common.FirstOrDefault(File.Exists);
        }

        // دالة مساعدة لتنظيف الواجهة
        private void ResetUI()
        {
            // 1. تصفير قائمة المواد
            Items.Clear();

            // 2. تنظيف بيانات الزبون
            txtCustomerName.Clear();
            txtCustomerPhone.Clear();
            txtCustomerAddress.Clear();
            txtDebtDollar.Text = "0"; // تصفير حقل الدين السابق

            // 3. تنظيف حقول إدخال المواد
            txtInputProduct.Clear();
            txtInputPrice.Clear();
            txtInputQty.Text = "1"; // نرجع الكمية للافتراضي
            txtInputType.Text = "قطعة";
            lblPriceInWords.Text = "";

            // 4. إعادة الخيارات للوضع الافتراضي
            btnCurrencyToggle.IsChecked = false; // نرجع للدينار العراقي
            chkDefaultMode.IsChecked = true;
            chkShowDollar.IsChecked = false;
            chkShowDinar.IsChecked = false;
            chkHideWriting.IsChecked = false;
            chkHidePreviousDebt.IsChecked = false;
            btnPaymentStatus.IsChecked = false; // نرجعه "نقدي"

            // 5. تحديث الأرقام النهائية (المجاميع)
            UpdateGrandTotal();
            txtDollarRate.IsEnabled = true;
            txtCustomerName.Focus();
            // 6. جلب رقم الفاتورة التالي تلقائياً
            var txtInv = this.FindName("txtInvoiceNumber") as TextBox;
            if (txtInv != null) txtInv.Text = GetSimpleNextNumber();

            // 7. وضع الماوس على اسم الزبون للبدء من جديد
            txtCustomerName.Focus();
        }
        private string GetSimpleNextNumber()
        {
            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    // حجي هنا نجيب أكبر رقم محول إلى عدد صحيح
                    cmd.CommandText = "SELECT IFNULL(MAX(CAST(InvoiceNumber AS INTEGER)), 0) FROM Invoices";
                    var result = cmd.ExecuteScalar();
                    int lastNum = Convert.ToInt32(result);
                    return (lastNum + 1).ToString();
                }
            }
            catch { return "1"; } // إذا أول مرة نرجع 1
        }
        private void GetCustomerDebtFromDb(string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName)) return;

            try
            {
                using (var connection = new SqliteConnection(dbPath))
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();

                    // نجلب المبلغ والعملة من جدول الديون
                    cmd.CommandText = "SELECT TotalAmount, Currency FROM DebtsMe WHERE DebtorName = @name LIMIT 1";
                    cmd.Parameters.AddWithValue("@name", customerName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            double amount = reader.GetDouble(0);
                            string currency = reader.GetString(1);

                            // حجي هنا الفرز اللي ردته:
                            if (currency == "IQD" || currency == "د.ع" || currency == "دينار عراقي")
                            {
                                // إذا دينار: ينزل الرقم كما هو ونضيف علامة د.ع
                                txtDebtDollar.Text = amount.ToString("N0") + " د.ع";
                            }
                            else
                            {
                                // إذا دولار: ينزل الرقم كما هو ونضيف علامة $
                                txtDebtDollar.Text = amount.ToString("N2") + " $";
                            }
                        }
                        else
                        {
                            txtDebtDollar.Text = "0";
                        }

                        // استدعاء الحسبة الكلية لتحديث المجموع النهائي للوصل
                        UpdateGrandTotal();
                    }
                }
            }
            catch { }
        }

        // دالة فتح الإعدادات
        private void btnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPane.Visibility = Visibility.Visible;
            Storyboard sb = (Storyboard)this.Resources["OpenSettingsAnim"];
            sb.Begin();
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SettingsPane.Visibility == Visibility.Visible)
            {
                SettingsPane.Visibility = Visibility.Collapsed;
            }
        }

        // دالة غلق الإعدادات حجي - هاي اللي تخفيه
        private void btnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            DoubleAnimation anim = new DoubleAnimation(320, TimeSpan.FromSeconds(0.3));
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };

            // عند انتهاء الحركة نخفي اللوحة تماماً
            anim.Completed += (s, ev) => SettingsPane.Visibility = Visibility.Collapsed;

            SettingsPaneTransform.BeginAnimation(TranslateTransform.XProperty, anim);
            SettingsPane.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
        }
        private void txtCustomerName_LostFocus(object sender, RoutedEventArgs e)
        {
            // أول ما تخلص كتابة الاسم وتنتقل للحقل اللي وراه، يجيب الدين
            GetCustomerDebtFromDb(txtCustomerName.Text);
        }
        private void txtCustomerName_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtCustomerName.Text;

            // إذا كانت الخانة فارغة، نغلق القائمة فوراً
            if (string.IsNullOrEmpty(query))
            {
                popCustomerSearch.IsOpen = false;
                return;
            }

            // البحث في قاعدة البيانات
            var suggestions = GetCustomerSuggestions(query);

            if (suggestions != null && suggestions.Any())
            {
                lstCustomerSuggestions.ItemsSource = suggestions;

                // نفتح القائمة فقط إذا لم تكن مفتوحة
                if (!popCustomerSearch.IsOpen)
                {
                    popCustomerSearch.IsOpen = true;
                }

                // أهم سطر: نرجع التركيز للـ TextBox حتى تكمل كتابة الرموز براحتك
                txtCustomerName.Focus();
            }
            else
            {
                popCustomerSearch.IsOpen = false;
            }
        }
        private void TxtCustomerPhone_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = new Regex("[^0-9+]+").IsMatch(e.Text);
        private void lstSearchSuggestions_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SelectSuggestion();
        private void lstSearchSuggestions_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; SelectSuggestion(); } }

        // 1. تعريف المتغير لحفظ نوع الوصل (اختياري)
        private void txtInputPrice_KeyDown(object sender, KeyEventArgs e)
        {
            // السماح بالأرقام
            bool isNumber = (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9);

            // السماح بالنقطة حجي
            bool isDecimal = (e.Key == Key.Decimal || e.Key == Key.OemPeriod);

            // السماح بالتحكم والإنتر
            bool isControlKeys = (e.Key == Key.Back || e.Key == Key.Tab || e.Key == Key.Enter);

            if (!isNumber && !isDecimal && !isControlKeys)
            {
                e.Handled = true;
            }

            // منع تكرار النقطة
            if (isDecimal && (sender as TextBox).Text.Contains("."))
            {
                e.Handled = true;
            }
        }
        // حجي هاي الدالة هي المحرك اللي يحول الوورد إلى PDF
        private string ExportDocxToPdf(string docxPath)
        {
            string pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            string soffice = FindLibreOfficePath();

            if (soffice == null)
                throw new InvalidOperationException("حجي ليبر أوفيس ما موجود! لازم تثبته أو تخليه بمجلد البرنامج.");

            var psi = new ProcessStartInfo
            {
                FileName = soffice,
                Arguments = $"--headless --convert-to pdf --outdir \"{Path.GetDirectoryName(pdfPath)}\" \"{docxPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
            }

            return pdfPath;
        }

        
        // 2. دالة فتح القائمة عند ضغط الزر
        private void btnInvoiceType_Click(object sender, RoutedEventArgs e)
        {
            if (btnInvoiceType.ContextMenu != null)
            {
                btnInvoiceType.ContextMenu.IsOpen = true;
            }
        }

        // 3. دالة معالجة الاختيار من القائمة
        private void InvoiceType_Selected(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                // 1. تنظيف النص من الإيموجي حتى المقارنة تصير صحيحة
                // "🏠 وصل المحل" يصير "وصل المحل"
                string cleanHeader = menuItem.Header.ToString()
                                    .Replace("🏠 ", "")
                                    .Replace("⚠️ ", "");

                // 2. تحديث المتغير العام للطباعة
                selectedInvoiceType = cleanHeader;

                // 3. تحديث محتوى الزر (إضافة السهم ضرورية للـ Trigger بالـ XAML)
                btnInvoiceType.Content = selectedInvoiceType + " ▾";

                // 4. تغيير لون الخط برمجياً وتنبيه المستخدم
                if (selectedInvoiceType.Contains("وهمي"))
                {
                    btnInvoiceType.Foreground = Brushes.Red;
                    ShowToast($"⚠️ انتبه حجي: {selectedInvoiceType}", "#E11D48");
                }
                else
                {
                    // لون Slate الاحترافي
                    btnInvoiceType.Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    ShowToast("🏠 تم الرجوع للوصل الرسمي", "#10B981");
                }
            }
        }
        private void cbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        
        // 1. إذا ضغطت على "الافتراضي" يفرغ البقية حجي ويرجع الوصل كامل
        private void chkDefaultMode_Checked(object sender, RoutedEventArgs e)
        {
            if (chkShowDollar == null) return;
            // حجي من تفعل الافتراضي، نطفي "فقط" حتى ما يصير تضارب
            chkShowDollar.IsChecked = false;
            chkShowDinar.IsChecked = false;
        }

        // 2. إذا اختار "دولار فقط" يطفي الدينار والافتراضي
        private void chkShowDollar_Checked(object sender, RoutedEventArgs e)
{
    if (chkShowDinar != null) chkShowDinar.IsChecked = false;
    if (chkDefaultMode != null) chkDefaultMode.IsChecked = false;
}

        // 3. إذا اختار "دينار فقط" يطفي الدولار والافتراضي
        private void chkShowDinar_Checked(object sender, RoutedEventArgs e)
        {
            if (chkShowDollar != null) chkShowDollar.IsChecked = false;
            if (chkDefaultMode != null) chkDefaultMode.IsChecked = false;
        }

        // 4. لأي خيار إخفاء مستقل (مثل إخفاء التفقيط)
        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (chkDefaultMode != null) chkDefaultMode.IsChecked = false;
        }
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
        private string ConvertWordToPdfUsingLibre(string docxPath)
        {
            string sofficePath = FindLibreOfficePath();
            if (string.IsNullOrEmpty(sofficePath)) return "";

            string pdfPath = Path.ChangeExtension(docxPath, ".pdf");

            var psi = new ProcessStartInfo
            {
                FileName = sofficePath,
                // أوامر التشغيل الصامت حتى ما يظهر البرنامج للمستخدم
                Arguments = $"--headless --nologo --nodefault --convert-to pdf --outdir \"{Path.GetDirectoryName(pdfPath)}\" \"{docxPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(20000); // ننتظر 20 ثانية كحد أقصى
                }
                return File.Exists(pdfPath) ? pdfPath : "";
            }
            catch { return ""; }
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
        // حجي هاي النسخة الوحيدة اللي تخليها بالملف
        private void txtInputPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox tb) || lblPriceInWords == null) return;

            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                lblPriceInWords.Text = "";
                return;
            }

            // حجي إذا النص ينتهي بنقطة، نعوفه وما نعدل شي حتى لا يمسح البوينت وأنت تكتب
            if (tb.Text.EndsWith(".")) return;

            try
            {
                string cleanValue = tb.Text.Replace(",", "");
                // استخدام InvariantCulture لضمان فهم النقطة كفاصلة عشرية حجي
                if (decimal.TryParse(cleanValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal res))
                {
                    tb.TextChanged -= txtInputPrice_TextChanged;

                    int cursorPosition = tb.SelectionStart;
                    int oldLength = tb.Text.Length;

                    // اللعبة هنا حجي: إذا اكو كسر نستخدم N2، إذا ماكو نستخدم N0
                    string format = (res % 1 != 0) ? "N2" : "N0";
                    tb.Text = res.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

                    int newLength = tb.Text.Length;
                    tb.SelectionStart = Math.Max(0, cursorPosition + (newLength - oldLength));

                    tb.TextChanged += txtInputPrice_TextChanged;

                    // تحديث التفقيط
                    // نمرر الرقم بالكامل للتفقيط إذا جان يحتاج كسور، أو نحوله long للأرقام الصحيحة
                    string cur = (btnCurrencyToggle.IsChecked == true) ? "دولار" : "دينار عراقي";
                    string sub = (btnCurrencyToggle.IsChecked == true) ? "سنت" : "فلس";

                    lblPriceInWords.Text = SiemensApp.Helpers.TafqeetTool.Convert(res, cur, sub);
                }
            }
            catch { }
        }
    }
}