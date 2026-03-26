// See https://aka.ms/new-console-template for more information

using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SendOrder;

public class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 6)
        {
            string username = args[0];
            int port; 
            bool portInt = Int32.TryParse(args[1], out port);
            string action = args[2];
            int quantity; 
            bool quantityInt = Int32.TryParse(args[3], out quantity);
            int price; 
            bool priceInt = Int32.TryParse(args[4], out price);
            int stockID; 
            bool stockIDInt = Int32.TryParse(args[5], out stockID);

            if (portInt && quantityInt && priceInt && stockIDInt)
            {
                if (action.ToLower() == "sell" || action.ToLower() == "buy")
                {
                    Producer producer = new Producer();
                    try
                    { producer.StartAsync(username, port, action, quantity, price, stockID).GetAwaiter().GetResult(); }
                    catch
                    { Console.WriteLine($"Connection could not be make with RabbitMQ. Check it is running, and port is correct."); }
                }
                else
                {
                    Console.WriteLine("Invalid action. Action must be buy or sell.");
                }
            }
            else
            {
                if (!portInt) { Console.WriteLine($"Port {args[1]} is not an integer."); }
                if (!quantityInt) { Console.WriteLine($"Quantity {args[3]} is not an integer."); }
                if (!priceInt) { Console.WriteLine($"Price {args[4]} is not an integer."); }
                if (!stockIDInt) { Console.WriteLine($"Stock ID {args[5]} is not an integer."); }
            }
        }
        else
        {
            Console.WriteLine("Usage: SendOrder <username> <port> <buy/sell> <quantity> <price> <stock ID>");
        }
    }
}

public class Producer
{
    public async Task StartAsync(string username, int port, string action, int quantity, int price, int ID)
    {
        var factory = new ConnectionFactory();
        factory.Port = port;

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