using CommunityToolkit.Mvvm.ComponentModel;

namespace SiemensApp.Mvvm;

/// <summary>
/// أساس الـ ViewModels — يرث من <see cref="ObservableObject"/> الخاص بـ CommunityToolkit.Mvvm.
/// يمنح خصائص <see cref="ObservableObject.SetProperty{T}"/> و<c>OnPropertyChanged</c>.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
