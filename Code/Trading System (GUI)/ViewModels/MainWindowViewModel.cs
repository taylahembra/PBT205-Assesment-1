namespace Trading_System.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ViewModelBase ViewModel { get; set; }

    public MainWindowViewModel()
    {
        ViewModel = new LoginPageViewModel(this);
    }

    public void LoggedIn(string username, int port)
    {
        ViewModel = new DoTradesPageViewModel(this, username, port);
        OnPropertyChanged(nameof(ViewModel));
    }

    public void ViewPrices(int port)
    {
        ViewModel = new ViewTradesPageViewModel(this, port);
        OnPropertyChanged(nameof(ViewModel));
    }

    public void PropertyChanged()
    {
        this.OnPropertyChanged(nameof(ViewModel));
    }

    public void Exit()
    {
        
    }
}