using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SiemensApp.Configuration;
using SiemensApp.Data;
using SiemensApp.Services;
using SiemensApp.ViewModels;
using SiemensApp.Views;

namespace SiemensApp;

/// <summary>
/// نقطة بداية التطبيق. تُهيّئ الـ Host (DI + Configuration + Logging) ثم تفتح نافذة تسجيل الدخول.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>الـ Host العام لـ DI (يستخدمه View Locator).</summary>
    public static IHost Host => ((App)Current)._host
        ?? throw new InvalidOperationException("الـ Host لم يُهيّأ بعد.");

    /// <summary>الحصول على خدمة من حاوية الـ DI.</summary>
    public static T GetRequiredService<T>() where T : notnull =>
        Host.Services.GetRequiredService<T>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            // 1) بناء الـ Configuration
            string appBase = AppContext.BaseDirectory;
            var configuration = new ConfigurationBuilder()
                .SetBasePath(appBase)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "SIEMENSAPP_")
                .Build();

            // 2) إنشاء مجلد السجلات قبل تهيئة Serilog
            Directory.CreateDirectory(Path.Combine(appBase, "Logs"));

            // 3) تهيئة Serilog من الـ Configuration
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            Log.Information("بدء تشغيل SiemensApp v{Version}", configuration["App:Version"]);

            // 4) بناء الـ Host
            _host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration(c =>
                {
                    c.Sources.Clear();
                    c.AddConfiguration(configuration);
                })
                .ConfigureServices((ctx, services) => ConfigureServices(services, ctx.Configuration))
                .Build();

            await _host.StartAsync().ConfigureAwait(true);

            // 5) ضمان وجود الجداول الإضافية في قاعدة البيانات
            try
            {
                using var scope = _host.Services.CreateScope();
                var schemaInit = scope.ServiceProvider.GetRequiredService<IInvoiceSchemaInitializer>();
                await schemaInit.EnsureCreatedAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "فشل تهيئة سكيمة الجداول الإضافية (سيتم المتابعة).");
            }

            // 6) فتح نافذة تسجيل الدخول
            var login = _host.Services.GetRequiredService<LoginWindow>();
            login.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "فشل بدء التشغيل.");
            MessageBox.Show($"فشل بدء التشغيل:\n{ex.Message}",
                "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // الإعدادات
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        // قاعدة البيانات (DbContextFactory هو الأفضل لتطبيقات WPF حتى لا يُشارَك السياق بين النوافذ)
        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlite(dbOptions.GetConnectionString());
            if (dbOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });

        // الخدمات الأساسية
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IUserSettingsStore, UserSettingsStore>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IInvoiceSchemaInitializer, InvoiceSchemaInitializer>();

        // التنقل (يُسجَّل أعلاه بعد إنشاء MainWindow)
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        // الـ ViewModels (Transient حتى تُنشأ نسخة جديدة لكل عرض)
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<InternalStorageViewModel>();
        services.AddTransient<AddProductViewModel>();
        services.AddTransient<StorageViewModel>();
        services.AddTransient<InvoicesListViewModel>();

        // الـ Views — تُحقن في الـ DI لتُستلم الـ ViewModels تلقائياً
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<InternalStorageView>();
        services.AddTransient<AddProductView>();
        services.AddTransient<StorageView>();
        services.AddTransient<InvoiceView>();
        services.AddTransient<InvoiceEditorView>();
        services.AddTransient<InvoicesListView>();
        services.AddTransient<DebtsMeView>();
        services.AddTransient<DebtsThemView>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            CreateDailyBackup();

            if (_host is not null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "خطأ أثناء إيقاف التطبيق.");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    /// <summary>الناتج الذي كان موجوداً سابقاً — نسخة احتياطية يومية لقاعدة البيانات.</summary>
    private static void CreateDailyBackup()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string originalDb = Path.Combine(baseDir, "SiemensData.db");

            string backupFolder = Path.Combine(baseDir, "Backups");
            Directory.CreateDirectory(backupFolder);

            string backupFileName = $"Backup_{DateTime.Now:yyyy_MM_dd}.db";
            string destFile = Path.Combine(backupFolder, backupFileName);

            if (File.Exists(originalDb))
                File.Copy(originalDb, destFile, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "فشل إنشاء النسخة الاحتياطية اليومية.");
        }
    }
}
