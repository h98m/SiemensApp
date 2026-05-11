using System.Windows.Controls;
using SiemensApp.ViewModels;

namespace SiemensApp.Views;

/// <summary>
/// عرض المخزن الداخلي. كامل المنطق في <see cref="InternalStorageViewModel"/>؛
/// الـ code-behind يقتصر على ربط الـ DataContext ومنح الـ DataGrid مرجع القائمة.
/// </summary>
public partial class InternalStorageView : UserControl
{
    private readonly InternalStorageViewModel _viewModel;

    public InternalStorageView(InternalStorageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        dgvInternalStorage.ItemsSource = _viewModel.Items;
        Loaded += async (_, _) => await _viewModel.LoadAsync().ConfigureAwait(true);
    }

    /// <summary>تحديث البحث — نمرّر النص للـ ViewModel.</summary>
    private void txtSearchInternal_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is InternalStorageViewModel vm && sender is TextBox tb)
            vm.SearchText = tb.Text;
    }
}
