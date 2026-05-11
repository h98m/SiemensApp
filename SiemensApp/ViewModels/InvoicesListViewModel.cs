using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Mvvm;
using SiemensApp.Services;

namespace SiemensApp.ViewModels;

/// <summary>ViewModel لقائمة الفواتير المُنجزة.</summary>
public sealed partial class InvoicesListViewModel : ViewModelBase
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly ILogger<InvoicesListViewModel> _logger;

    public InvoicesListViewModel(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        ILogger<InvoicesListViewModel> logger)
    {
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    public ObservableCollection<InvoiceHeader> Invoices { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => _ = LoadAsync(value);

    [RelayCommand]
    public async Task LoadAsync(string filter = "")
    {
        try
        {
            await _schema.EnsureCreatedAsync().ConfigureAwait(true);

            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT Id, InvoiceNumber, CustomerName, Phone, Date,
                       COALESCE(TotalAmount, 0), COALESCE(TotalAmountDollar, 0),
                       COALESCE(InvoiceType, ''), COALESCE(Currency, '')
                FROM Invoices
                WHERE InvoiceNumber LIKE @p OR CustomerName LIKE @p OR Phone LIKE @p
                ORDER BY Id DESC
                """;
            cmd.Parameters.AddWithValue("@p", "%" + (filter ?? string.Empty) + "%");

            Invoices.Clear();
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            while (await reader.ReadAsync().ConfigureAwait(true))
            {
                Invoices.Add(new InvoiceHeader
                {
                    Id = reader.GetInt32(0),
                    InvoiceNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Date = reader.IsDBNull(4) ? DateTime.MinValue
                        : DateTime.TryParse(reader.GetString(4), out var d) ? d : DateTime.MinValue,
                    TotalAmount = reader.GetDouble(5),
                    TotalAmountDollar = reader.GetDouble(6),
                    InvoiceType = reader.GetString(7),
                    Currency = reader.GetString(8)
                });
            }

            _logger.LogDebug("تم تحميل {Count} فاتورة.", Invoices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل قائمة الفواتير.");
        }
    }
}
