# SiemensApp

> تطبيق إدارة محل/مخزن حديث (WPF + .NET 8) — إصدار فواتير، إدارة مخزون، تتبّع ديون، تقارير، وطباعة احترافية بدعم كامل للعربية.

[![Build (Windows)](https://github.com/h98m/SiemensApp/actions/workflows/ci.yml/badge.svg?event=push&branch=main)](https://github.com/h98m/SiemensApp/actions/workflows/ci.yml)
[![Tests (Linux)](https://github.com/h98m/SiemensApp/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/h98m/SiemensApp/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?logo=windows)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

---

## 📋 جدول المحتويات

- [نظرة عامة](#-نظرة-عامة)
- [المزايا الأساسية](#-المزايا-الأساسية)
- [متطلبات النظام](#-متطلبات-النظام)
- [التشغيل المحلي](#-التشغيل-المحلي)
- [البناء والاختبار](#-البناء-والاختبار)
- [بنية المشروع](#-بنية-المشروع)
- [الإعدادات (`appsettings.json`)](#️-الإعدادات-appsettingsjson)
- [النشر (Publish)](#-النشر-publish)
- [الإسهام](#-الإسهام)
- [الرخصة](#-الرخصة)

---

## 🎯 نظرة عامة

**SiemensApp** هو تطبيق سطح مكتب لـ Windows مُصمَّم خصّيصاً للمحلات التجارية الصغيرة والمتوسطة في المنطقة العربية. مبني على .NET 8 + WPF مع بنية حديثة (Dependency Injection، Hosting، Configuration، Serilog، CommunityToolkit.Mvvm).

- 🇮🇶 **دعم كامل للعربية** (RTL، تفقيط الأرقام، خطوط عربية).
- 💵 **تعدد العملات** (دينار عراقي / دولار أمريكي بسعر صرف قابل للتعديل).
- 🗃️ **قاعدة بيانات محلية** (SQLite عبر Entity Framework Core).
- 📄 **توليد فواتير Word/PDF** من قوالب `.docx` (DocX + iText 7).
- 🔒 **مصادقة آمنة** (PBKDF2 + Salt + قفل تلقائي بعد محاولات فاشلة).
- 📝 **تسجيل أحداث متقدم** (Serilog مع ملفات يومية ودوّارة).

---

## ✨ المزايا الأساسية

| الميزة | الوصف |
| --- | --- |
| 🧾 **إصدار الفواتير** | فاتورة بنود متعدّدة، خصومات، ضرائب، تفقيط تلقائي، طباعة بقوالب متعدّدة. |
| 📦 **إدارة المخزن** | مخزن عام (`GlobalStock`) ومخزن داخلي (`InternalStock`) مع تتبّع كميات. |
| 💰 **إدارة الديون** | "ديون لي" و"ديون عليّ" مع سجل عمليات FIFO وتسديد جزئي/كامل. |
| 👥 **قاعدة عملاء** | إضافة عملاء، تتبّع تاريخ معاملاتهم، حساب الرصيد المتبقي. |
| 🖨️ **طباعة احترافية** | تحويل قالب Word إلى DOCX ثم PDF عبر LibreOffice. |
| 🔐 **حماية كلمة المرور** | PBKDF2 بـ 100,000 تكرار + Salt عشوائي 16 بايت + حماية ضد هجمات التوقيت. |
| 🌍 **عربية كاملة** | واجهة RTL، تفقيط احترافي، تنسيق أرقام عربي. |

---

## 🛠️ متطلبات النظام

### للتشغيل (Users)
- Windows 10 / 11 (نسخة 64-bit).
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
- [LibreOffice](https://www.libreoffice.org/download/download/) **(اختياري)** — مطلوب لتحويل المستندات إلى PDF.

### للتطوير (Developers)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) أو أحدث.
- Visual Studio 2022 (17.8+) أو JetBrains Rider أو VS Code + C# Dev Kit.
- Git.

> **ملاحظة لمطوّري Linux/macOS**: المشروع `net8.0-windows` (WPF)، لذا لا يمكن **تشغيله** على غير Windows. لكنّ مشروع `SiemensApp.Tests` يستهدف `net8.0` عام، فيمكن تشغيل الاختبارات وبناء WPF (بدون تشغيل) من Linux عبر `dotnet build -p:EnableWindowsTargeting=true`.

---

## 🚀 التشغيل المحلي

```bash
# 1) استنساخ الريبو
git clone https://github.com/h98m/SiemensApp.git
cd SiemensApp

# 2) استعادة الحزم
dotnet restore

# 3) البناء (Windows)
dotnet build SiemensApp.sln -c Debug

# 3-بديل) البناء من Linux/macOS (للتحقق من التركيب فقط، بدون تشغيل)
dotnet build SiemensApp/SiemensApp.csproj -p:EnableWindowsTargeting=true

# 4) تشغيل التطبيق (Windows فقط)
dotnet run --project SiemensApp/SiemensApp.csproj
```

**أول تشغيل**: سيُطلب منك تعيين كلمة مرور المسؤول. (في الإصدارات القديمة كانت `199426` افتراضياً — تمّ إلغاء هذا السلوك في PR-4 من خارطة الطريق).

---

## 🧪 البناء والاختبار

```bash
# تشغيل كل اختبارات الوحدة (cross-platform — يعمل على Linux/macOS/Windows)
dotnet test SiemensApp.Tests/SiemensApp.Tests.csproj

# مع تغطية الكود
dotnet test --collect:"XPlat Code Coverage"

# تنسيق الكود
dotnet format SiemensApp.sln
```

تغطّي الاختبارات حالياً (28 اختبار): `TafqeetTool` (تفقيط الأرقام)، `PasswordHasher` (تشفير كلمة المرور)، `AuthService` (مصادقة + Lockout).

---

## 📂 بنية المشروع

```
SiemensApp/
├── SiemensApp/                          # المشروع الرئيسي (WPF)
│   ├── App.xaml(.cs)                    # نقطة البداية + Host + DI
│   ├── Configuration/AppOptions.cs      # الإعدادات الـ Strongly-Typed
│   ├── Data/AppDbContext.cs             # EF Core DbContext
│   ├── Helpers/TafqeetTool.cs           # تفقيط الأرقام بالعربية
│   ├── Migrations/                      # ميجريشن EF Core
│   ├── Models/                          # كيانات البيانات
│   ├── Mvvm/ViewModelBase.cs            # أساس الـ ViewModels
│   ├── Services/                        # خدمات DI (Auth, Dialog, Navigation, ...)
│   ├── ViewModels/                      # ViewModels لكل شاشة
│   ├── Views/                           # نوافذ وشاشات WPF
│   ├── Resources/                       # موارد (صور، شعارات)
│   ├── appsettings.json                 # الإعدادات الافتراضية
│   └── appsettings.Development.json     # تجاوزات بيئة التطوير
├── SiemensApp.Tests/                    # اختبارات xUnit (net8.0)
├── .github/workflows/                   # تكامل مستمر (CI/CD)
├── SiemensApp.sln                       # ملف الحل
└── README.md
```

---

## ⚙️ الإعدادات (`appsettings.json`)

كل الإعدادات الحساسة قابلة للتعديل من `appsettings.json` بدون إعادة بناء:

```jsonc
{
  "App": {
    "Title": "SiemensApp",
    "Version": "2.0.0",
    "DefaultDollarRate": 150     // سعر صرف الدولار الافتراضي (بالدينار)
  },
  "Database": {
    "FileName": "SiemensData.db",
    "EnableSensitiveDataLogging": false
  },
  "Auth": {
    "MaxFailedAttempts": 5,      // عدد المحاولات الفاشلة قبل الحظر
    "LockoutMinutes": 5          // مدة الحظر بالدقائق
  }
}
```

يمكن أيضاً تجاوز أي قيمة عبر متغيرات بيئة بادئتها `SIEMENSAPP_` (مثلاً `SIEMENSAPP_App__DefaultDollarRate=160`).

**ملاحظة أمنية**: كلمة مرور المسؤول لا تُحفظ في `appsettings.json` — تُحفظ كـ PBKDF2 hash في `%APPDATA%/SiemensApp/user-settings.json`.

---

## 📦 النشر (Publish)

لإنتاج نسخة قابلة للتوزيع:

```bash
dotnet publish SiemensApp/SiemensApp.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true
```

تجد المخرجات في `SiemensApp/bin/Release/net8.0-windows/win-x64/publish/`.

**خطوات النشر اليدوي**:
1. انسخ مجلد `publish/` كاملاً إلى الجهاز الهدف.
2. تأكّد من تثبيت [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
3. إن أردت توليد PDF: ثبّت [LibreOffice](https://www.libreoffice.org/).
4. شغّل `SiemensApp.exe`.

---

## 🤝 الإسهام

نرحب بالمساهمات! الخطوات:

1. Fork الريبو وأنشئ فرعاً: `git checkout -b feature/my-feature`.
2. التزم بـ [Conventional Commits](https://www.conventionalcommits.org/) في رسائل الـ commit (مثال: `feat: add dark mode toggle`).
3. تأكّد أن كل الاختبارات تمر: `dotnet test`.
4. تأكّد أن البناء ناجح: `dotnet build -p:EnableWindowsTargeting=true`.
5. افتح PR ووصِف التغييرات والاختبارات.

### إرشادات الكود
- اتّبع نمط MVVM الموجود (`ViewModel` يرث `ObservableObject`، Commands عبر `[RelayCommand]`).
- لا تستخدم `MessageBox.Show` مباشرة — استعمل `IDialogService`.
- لا تكتب SQL مباشرة في الـ View — استعمل Repository (انظر `Services/Repositories/`).
- استخدم `async/await` لكل عمليات DB.

---

## 📜 الرخصة

هذا المشروع مرخّص بـ [MIT License](LICENSE) — انظر ملف `LICENSE` للتفاصيل.

---

## 🙏 شكر وامتنان

- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — لنمط MVVM.
- [DocX](https://github.com/xceedsoftware/DocX) — لتوليد ملفات Word.
- [iText 7](https://itextpdf.com/) — لتوليد PDF.
- [Serilog](https://serilog.net/) — للتسجيل المتقدم.
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) — للوصول إلى البيانات.

---

**صُمّم في العراق 🇮🇶 بحب للمحلات الصغيرة.**
