using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace SiemensApp.Helpers
{
    /// <summary>
    /// دوال مشتركة للتعامل مع ملفات Word (DocX) والتحويل إلى PDF
    /// </summary>
    public static class DocxHelper
    {
        /// <summary>
        /// تعبئة خلية في جدول Word بنص منسق
        /// </summary>
        public static void FillCell(Cell cell, string text, double fontSize,
            string fontName = "Arabic Transparent", bool bold = false, bool verticalCenter = false)
        {
            try
            {
                if (cell.Paragraphs.Count > 0)
                {
                    cell.Paragraphs[0].ReplaceText(cell.Paragraphs[0].Text, "");
                    var p = cell.Paragraphs[0].Append(text ?? "");
                    p.Font(new Xceed.Document.NET.Font(fontName));
                    p.FontSize(fontSize);
                    if (bold) p.Bold();
                    p.Alignment = Alignment.center;
                }
                else
                {
                    var p = cell.InsertParagraph(text ?? "");
                    p.Font(new Xceed.Document.NET.Font(fontName));
                    p.FontSize(fontSize);
                    if (bold) p.Bold();
                    p.Alignment = Alignment.center;
                }

                if (verticalCenter)
                    cell.VerticalAlignment = Xceed.Document.NET.VerticalAlignment.Center;
            }
            catch { }
        }

        /// <summary>
        /// البحث عن مسار LibreOffice (المحمول أو المثبت)
        /// </summary>
        public static string FindLibreOfficePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. النسخة المحمولة
            string portable = Path.Combine(baseDir, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(portable)) return portable;

            // 2. البحث في السجل (Registry)
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe"))
                {
                    if (key != null) return key.GetValue("")?.ToString();
                }
            }
            catch { }

            // 3. المسارات الافتراضية
            string[] common =
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            };
            return common.FirstOrDefault(File.Exists) ?? "";
        }

        /// <summary>
        /// تحويل ملف Word إلى PDF باستخدام LibreOffice
        /// </summary>
        public static string ConvertWordToPdf(string docxPath)
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
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(20000);
                }
                return File.Exists(pdfPath) ? pdfPath : "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// تحديد اسم ملف القالب حسب نوع الفاتورة
        /// </summary>
        public static string GetTemplateFileName(string invoiceType)
        {
            return invoiceType switch
            {
                "وصل محل اجراس" => "Template1.docx",
                "وصل محل عصام" => "Template2.docx",
                "وصل محل لمسة التكنولوجيا" => "Template3.docx",
                "وصل محل المعين" => "Template4.docx",
                _ => "Template.docx"
            };
        }

        /// <summary>
        /// تحديد اسم العرض حسب نوع الفاتورة
        /// </summary>
        public static string GetDisplayInvoiceName(string invoiceType)
        {
            return invoiceType switch
            {
                "وصل محل اجراس" => "وصل محل اجراس",
                "وصل محل عصام" => "وصل محل عصام",
                "وصل محل لمسة التكنولوجيا" => "وصل محل لمسة التكنولوجيا",
                "وصل محل المعين" => "وصل محل المعين",
                _ => invoiceType
            };
        }

        /// <summary>
        /// إنشاء مسار مجلد PDF على سطح المكتب
        /// </summary>
        public static string EnsurePdfFolder()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string pdfFolderPath = Path.Combine(desktopPath, "pdf");
            if (!Directory.Exists(pdfFolderPath))
                Directory.CreateDirectory(pdfFolderPath);
            return pdfFolderPath;
        }

        /// <summary>
        /// تنظيف اسم الملف من الحروف الممنوعة
        /// </summary>
        public static string SanitizeFileName(string name)
        {
            return System.Text.RegularExpressions.Regex.Replace(name, @"[\\/:*?""<>|]", "_");
        }
    }
}
