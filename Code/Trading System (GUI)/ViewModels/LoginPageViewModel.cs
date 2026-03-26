using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RabbitMQ.Client;

namespace Trading_System.ViewModels;

public partial class LoginPageViewModel : ViewModelBase
{
    [ObservableProperty] private string username = "";
    [ObservableProperty] private int port = 5672;
    private MainWindowViewModel mainWindowViewModel;
    [ObservableProperty] private string errormessage;

    public LoginPageViewModel(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
        errormessage = "";
    }

    [RelayCommand]
    public async Task LoginPressed()
    {
        try
        {
            var factory = new ConnectionFactory();
            factory.Port = port;

            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();
            
            await channel.CloseAsync();
            await connection.CloseAsync();
            
            if (username != "")
            { mainWindowViewModel.LoggedIn(username, port);}
        }
        catch (Exception e)
        {
            errormessage = "Cannot connect to RabbitMQ";
            OnPropertyChanged(nameof(errormessage));
        }
        
    }

    [RelayCommand]
    public async Task ViewPrices()
    {
        try
        {
            var factory = new ConnectionFactory();
            factory.Port = port;

            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();
            
            await channel.CloseAsync();
            await connection.CloseAsync();
            
            mainWindowViewModel.ViewPrices(port);
        }
        catch (Exception e)
        {
            errormessage = "Cannot connect to RabbitMQ";
            OnPropertyChanged(nameof(errormessage));
            mainWindowViewModel.PropertyChanged();
        }
    }
}