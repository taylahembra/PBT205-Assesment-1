using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace Trading_System.ViewModels;

public partial class BuySellViewModel : ViewModelBase
{
    [ObservableProperty] private string type; // Buy or sell
    [ObservableProperty] private string makeTradeText;

    [ObservableProperty] private int stock = 0;
    [ObservableProperty] private int quantity = 0;
    [ObservableProperty] private int price = 0;

    private string username;
    private int port;
    
    private MainWindowViewModel mainWindowViewModel;

    public ObservableCollection<Item> OfferedTrades { get; set; } = new ObservableCollection<Item>();// username, stockname, stockID, quantity, price
    private bool OfferedTradesEditLock = false;

    public BuySellViewModel(string type, MainWindowViewModel mainWindowViewModel, string username, int port)
    {
        this.username = username;
        this.port = port;
        
        this.mainWindowViewModel = mainWindowViewModel;
        this.type = type;
        makeTradeText = $"{type} stock:";
    }

    [RelayCommand]
    public void SubmitTrade(int ID)
    {
        // Get values
        var trade = OfferedTrades[ID];
        
        string newType = (this.Type.ToLower() == "sell") ? "buy" : "sell";
        
        try
        {
            var t = new Thread(() => StartAsync(username, port, newType, trade.quantity, trade.price, trade.stockID));
            t.Start();
        }
        catch
        { Console.WriteLine($"Connection could not be make with RabbitMQ. Check it is running, and port is correct."); }
    }

    [RelayCommand]
    public void SubmitNewTrade()
    {
        try
        {
            var t = new Thread(() => StartAsync(username, port, type.ToLower(), quantity, price, stock));
            t.Start();
        }
        catch
        { Console.WriteLine($"Connection could not be make with RabbitMQ. Check it is running, and port is correct."); }
    }
    
    private async Task StartAsync(string username, int port, string action, int quantity, int price, int ID)
    {
        var factory = new ConnectionFactory();
        factory.Port = port;
        factory.AutomaticRecoveryEnabled = true;

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: "Orders", durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { { "x-queue-type", "quorum" } });
        
        var message = $"{action} {username} {quantity} {price} {ID}";
        
        var encodedMessage = Encoding.UTF8.GetBytes(message);
        
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "Orders", body: encodedMessage);

        Console.WriteLine($"Order placed for {username} to {action} {quantity} units for ${price}.");

    }
}