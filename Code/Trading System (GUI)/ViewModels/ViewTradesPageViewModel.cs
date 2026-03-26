using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Controls.Documents;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Trading_System.ViewModels;

public partial class ViewTradesPageViewModel : ViewModelBase
{
    public ObservableCollection<BuySellViewModel> guis { get; set; }
    MainWindowViewModel mainWindowViewModel;
    
    [ObservableProperty] private ViewTradesViewModel viewTradesViewModel;

    public ViewTradesPageViewModel(MainWindowViewModel mainWindowViewModel, int port)
    {
        this.mainWindowViewModel = mainWindowViewModel;
        viewTradesViewModel = new ViewTradesViewModel(mainWindowViewModel, port);
        
        guis = new ObservableCollection<BuySellViewModel>(); // Leave empty as we want no columns
    }
}