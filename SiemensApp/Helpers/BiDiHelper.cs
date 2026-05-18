using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SiemensApp.Helpers
{
    /// <summary>
    /// معالجة النصوص ثنائية الاتجاه (عربي + إنجليزي)
    /// يحل مشكلة عرض النص المختلط مثل "تيرمل 4x4" بشكل صحيح
    /// </summary>
    public static class BiDiHelper
    {
        // حرف تحكم Unicode: علامة من اليمين لليسار
        private const char RLM = '\u200F';
        // حرف تحكم Unicode: علامة من اليسار لليمين
        private const char LRM = '\u200E';

        /// <summary>
        /// تحديد اتجاه النص تلقائياً بناءً على أول حرف فعلي
        /// </summary>
        public static FlowDirection DetectDirection(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return FlowDirection.RightToLeft;

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c))
                    continue;

                var category = CharUnicodeInfo.GetUnicodeCategory(c);

                // الحروف العربية والعبرية وباقي الاتجاه من اليمين
                if (IsRtlChar(c))
                    return FlowDirection.RightToLeft;

                // الحروف اللاتينية
                if (char.IsLetter(c))
                    return FlowDirection.LeftToRight;
            }

            return FlowDirection.RightToLeft;
        }

        /// <summary>
        /// فحص إذا كان الحرف من حروف RTL (عربي/عبري/فارسي)
        /// </summary>
        private static bool IsRtlChar(char c)
        {
            // نطاقات Unicode للحروف العربية والعبرية
            return (c >= 0x0600 && c <= 0x06FF) || // Arabic
                   (c >= 0x0750 && c <= 0x077F) || // Arabic Supplement
                   (c >= 0x08A0 && c <= 0x08FF) || // Arabic Extended-A
                   (c >= 0xFB50 && c <= 0xFDFF) || // Arabic Presentation Forms-A
                   (c >= 0xFE70 && c <= 0xFEFF) || // Arabic Presentation Forms-B
                   (c >= 0x0590 && c <= 0x05FF);   // Hebrew
        }

        /// <summary>
        /// تحديث اتجاه TextBox تلقائياً عند تغير النص
        /// استدعي هذه الدالة من حدث TextChanged
        /// </summary>
        public static void UpdateTextBoxDirection(TextBox textBox)
        {
            if (textBox == null) return;
            textBox.FlowDirection = DetectDirection(textBox.Text);
        }

        /// <summary>
        /// ربط حدث TextChanged لعدة حقول TextBox دفعة واحدة
        /// استدعي هذه الدالة مرة واحدة في المُنشئ (Constructor)
        /// </summary>
        public static void AttachBiDi(params TextBox[] textBoxes)
        {
            foreach (var tb in textBoxes)
            {
                if (tb != null)
                {
                    tb.TextChanged += (s, e) => UpdateTextBoxDirection((TextBox)s);
                }
            }
        }

        /// <summary>
        /// إضافة علامات اتجاه Unicode للنص المختلط قبل الحفظ أو العرض
        /// يحل مشكلة ترتيب الأرقام والحروف اللاتينية في النص العربي
        /// مثال: "تيرمل 4x4" يصبح "تيرمل ‎4x4‏" بتضمين علامات الاتجاه
        /// </summary>
        public static string FixMixedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = new System.Text.StringBuilder();
            bool lastWasRtl = true;

            foreach (char c in text)
            {
                if (char.IsDigit(c) || IsLatinChar(c))
                {
                    if (lastWasRtl)
                    {
                        result.Append(LRM);
                        lastWasRtl = false;
                    }
                }
                else if (IsRtlChar(c))
                {
                    if (!lastWasRtl)
                    {
                        result.Append(RLM);
                        lastWasRtl = true;
                    }
                }

                result.Append(c);
            }

            // إضافة RLM في النهاية لضمان العودة للاتجاه العربي
            if (!lastWasRtl)
                result.Append(RLM);

            return result.ToString();
        }

        /// <summary>
        /// فحص إذا كان الحرف لاتيني (إنجليزي)
        /// </summary>
        private static bool IsLatinChar(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }
    }
}
