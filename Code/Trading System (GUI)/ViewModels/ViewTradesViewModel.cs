using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RxFramework;
using SkiaSharp;

namespace Trading_System.ViewModels;

public class ViewTradesViewModel
{
    public ObservableCollection<Tuple<string, double>> Trades { get; set; }

    MainWindowViewModel mainWindowViewModel;
    public int port;

    public ViewTradesViewModel(MainWindowViewModel mainWindowViewModel, int port)
    {
        this.port = port;
        this.mainWindowViewModel = mainWindowViewModel;
        Trades = new ObservableCollection<Tuple<string, double>>();

        Thread thread = new Thread(GetMessageTradesAsync);
        thread.Start();
    }
    
    public async void GetMessageTradesAsync()
    {
        var factory = new ConnectionFactory();
        factory.Port = port;
        
        try {
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var args = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };
            args.Add("x-message-ttl", 1000); // Make messages only last 1 second
        
            await channel.QueueDeclareAsync(queue: "Trades", durable: true, exclusive: false, autoDelete: false,
                arguments: args);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                if (message != null && message.Length > 0)
                {
                    UpdatePrices(message);
                }
                else
                {
                    Console.WriteLine($"Null message received");
                }
                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync("Trades", autoAck: true, consumer: consumer);
        
            Console.ReadLine();

        } catch (Exception e) {
            Console.WriteLine($"Something went wrong");
        }
    }

    private void UpdatePrices(string recievedMessage)
    {
        Trades.Clear();

        recievedMessage = recievedMessage.TrimEnd(';');
        string[] trades = recievedMessage.Split(';');
        

        foreach (var trade in trades)
        {
            string[] tradeSplit = trade.Split(",");
            Trades.Add(new Tuple<string, double>($"{tradeSplit[0]} ({tradeSplit[1]})", double.Parse(tradeSplit[2])));
        }
        mainWindowViewModel.PropertyChanged();
    }
}