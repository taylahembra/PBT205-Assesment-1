using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Trading_System.ViewModels;

public partial class DoTradesPageViewModel : ViewModelBase
{
    public ObservableCollection<BuySellViewModel> guis { get; set; }
    MainWindowViewModel mainWindowViewModel;
    private int port;
    
    [ObservableProperty] private ViewTradesViewModel viewTradesViewModel;

    public DoTradesPageViewModel(MainWindowViewModel mainWindowViewModel, string username, int port)
    {
        this.port = port;
        this.mainWindowViewModel = mainWindowViewModel;
        viewTradesViewModel = new ViewTradesViewModel(mainWindowViewModel, port);
        
        guis = new ObservableCollection<BuySellViewModel>();
        if (username != "")
        {
            guis.Add(new BuySellViewModel("Buy", mainWindowViewModel, username, port));
            guis.Add(new BuySellViewModel("Sell", mainWindowViewModel, username, port));
        }
        
        Thread thread = new Thread(GetMessageOfferedAsync);
        thread.Start();
    }
    
    public async void GetMessageOfferedAsync()
    {
        var factory = new ConnectionFactory();
        factory.Port = port;
        factory.AutomaticRecoveryEnabled = true;

        try {
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var args = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };
            args.Add("x-message-ttl", 1000); // Make messages only last 1 second
        
            await channel.QueueDeclareAsync(queue: "Offered", durable: true, exclusive: false, autoDelete: false,
                arguments: args);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                UpdateOffered(message);
                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync("Offered", autoAck: true, consumer: consumer);
        
            Console.ReadLine();

        } catch (Exception e) {
            Console.WriteLine($"Something went wrong");
        }
    }
    
    private void UpdateOffered(string recievedMessage)
    {
        recievedMessage = recievedMessage.TrimEnd(';');
        string[] trades = recievedMessage.Split(';');

        foreach (var gui in guis)
        {
            gui.OfferedTrades.Clear();
        }
        
        foreach (var trade in trades)
        {
            string[] tradeSplit = trade.Split(",");
            if (tradeSplit[0] == "buy")
            {
                var item = new Item(
                    tradeSplit[1], tradeSplit[2], Int32.Parse(tradeSplit[3]), 
                    tradeSplit[0], Int32.Parse(tradeSplit[4]), Int32.Parse(tradeSplit[5]), 
                    guis[0].OfferedTrades.Count, guis[0]);
                guis[0].OfferedTrades.Add(item);
            }
            else if (tradeSplit[0] == "sell")
            {
                var item = new Item(
                    tradeSplit[1], tradeSplit[2], Int32.Parse(tradeSplit[3]), 
                    tradeSplit[0], Int32.Parse(tradeSplit[4]), Int32.Parse(tradeSplit[5]), 
                    guis[1].OfferedTrades.Count, guis[1]);
                guis[1].OfferedTrades.Add(item);
            }
            
            mainWindowViewModel.PropertyChanged();
        }
    }
}