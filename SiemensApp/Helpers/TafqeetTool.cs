using System;

namespace SiemensApp.Helpers
{
    public static class TafqeetTool
    {
        private static string[] ones = { "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر" };
        private static string[] tens = { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
        private static string[] hundreds = { "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة" };

        public static string Convert(decimal number, string currency = "دينار عراقي", string subCurrency = "فلس")
        {
            // 1. تقريب الرقم لمرتبتين عشرية قبل أي عملية حتى نتخلص من الـ 0.999999
            number = Math.Round(number, 2);

            if (number == 0) return "صفر " + currency;

            string prefix = "";
            if (number < 0) { prefix = "سالب "; number = Math.Abs(number); }

            // 2. استخراج الجزء الصحيح والكسر بعد التقريب
            long mainUnit = (long)Math.Truncate(number);
            int fraction = (int)Math.Round((number - mainUnit) * 100);

            // إذا صار الكسر 100 بسبب التقريب، نزيده ع الصحيح ونصفر الكسر
            if (fraction == 100) { mainUnit++; fraction = 0; }

            string result = prefix + ProcessGroup(mainUnit) + " " + currency;

            if (fraction > 0)
            {
                result += " و " + ProcessGroup(fraction) + " " + subCurrency;
            }

            return result + " فقط لا غير";
        }

        private static string ProcessGroup(long number)
        {
            if (number == 0) return "";
            if (number < 0) return "سالب " + ProcessGroup(Math.Abs(number));

            string words = "";

            // مليارات
            if ((number / 1000000000) >= 1)
            {
                long b = number / 1000000000;
                if (b == 1) words += "مليار ";
                else if (b == 2) words += "ملياران ";
                else if (b <= 10) words += ProcessGroup(b) + " مليارات ";
                else words += ProcessGroup(b) + " مليار ";
                number %= 1000000000;
                if (number > 0) words += " و ";
            }

            // ملايين
            if ((number / 1000000) >= 1)
            {
                long m = number / 1000000;
                if (m == 1) words += "مليون ";
                else if (m == 2) words += "مليونان ";
                else if (m <= 10) words += ProcessGroup(m) + " ملايين ";
                else words += ProcessGroup(m) + " مليون ";
                number %= 1000000;
                if (number > 0) words += " و ";
            }

            // آلاف
            if ((number / 1000) >= 1)
            {
                long t = number / 1000;
                if (t == 1) words += "ألف ";
                else if (t == 2) words += "ألفان ";
                else if (t <= 10) words += ProcessGroup(t) + " آلاف ";
                else words += ProcessGroup(t) + " ألف ";
                number %= 1000;
                if (number > 0) words += " و ";
            }

            // مئات وعشرات وآحاد
            if (number >= 100)
            {
                words += hundreds[number / 100] + " ";
                number %= 100;
                if (number > 0) words += " و ";
            }

            if (number > 0)
            {
                if (number < 20) words += ones[number];
                else
                {
                    if (number % 10 > 0) words += ones[number % 10] + " و ";
                    words += tens[number / 10];
                }
            }

            return words.Trim();
        }
    }
}