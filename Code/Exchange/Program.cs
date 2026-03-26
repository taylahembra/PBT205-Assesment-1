// See https://aka.ms/new-console-template for more information

using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Exchange;

public class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 1)
        {
            int port; 
            bool portInt = Int32.TryParse(args[0], out port);

            if (portInt)
            {
                try
                { Exchange consumer = new Exchange(port); }
                catch (Exception e)
                { Console.WriteLine(
                        $"Connection could not be make with RabbitMQ. Check it is running, and port is correct."); }
            }
            else
            {
                Console.WriteLine($"Port is not an integer.");
            }
        }
        else
        { Console.WriteLine("Usage: Exchange <port>"); }
    }
}

public class Exchange
{
    public Data data;
    
    public Exchange(int port)
    {
        if (File.Exists("data.json"))
        {
            data = JsonConvert.DeserializeObject<Data>(File.ReadAllText("data.json"));
        }
        else
        {
            data = new Data();  
            
            // Add starting data
            data.stocks.Add(new Stock(1, "Tim's Books"));
            data.stocks.Add(new Stock(2, "Computer Parts and Co"));
            data.stocks.Add(new Stock(3, "MiningIsUs"));
            data.stocks.FirstOrDefault(stock => stock.ID == 1).stockOwnership.Add(new Tuple<string, int>("user2", 100));
            data.stocks.FirstOrDefault(stock => stock.ID == 2).stockOwnership.Add(new Tuple<string, int>("user1", 100));
            data.stocks.FirstOrDefault(stock => stock.ID == 3).stockOwnership.Add(new Tuple<string, int>("user3", 100));
            data.tradeHistory.Add(new Tuple<Stock, int, int>(data.stocks.FirstOrDefault(stock => stock.ID == 1), 50, 100));
            data.tradeHistory.Add(new Tuple<Stock, int, int>(data.stocks.FirstOrDefault(stock => stock.ID == 2), 100, 100));
            data.tradeHistory.Add(new Tuple<Stock, int, int>(data.stocks.FirstOrDefault(stock => stock.ID == 3), 200, 100));
            data.sellOrders.Add(new Order("user3", 100, 100, data.stocks.FirstOrDefault(stock => stock.ID == 1), "sell"));
        }
        
        StartAsync(port).GetAwaiter().GetResult();
    }

    public async Task StartAsync(int port)
    {
        GetMessageAsync(port);
        while (true)
        {
            await UpdateTrades(port);
            await UpdateOffered(port);
            Thread.Sleep(1000);
        }
    }

    public async Task GetMessageAsync(int port)
    {
        var factory = new ConnectionFactory();
        factory.Port = port;
        factory.AutomaticRecoveryEnabled = true;

        try {
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "Orders", durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?> { { "x-queue-type", "quorum" } });

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                if (message != null && message.Length > 0)
                {
                    string[] list = message.Split(" ");
                    ProcessOrder(list[0], list[1], Int32.Parse(list[2]), Int32.Parse(list[3]), Int32.Parse(list[4]));
                }
                else
                {
                    Console.WriteLine("Blank message received");
                }
                return  Task.CompletedTask;
            };

            await channel.BasicConsumeAsync("Orders", autoAck: true, consumer: consumer);
            Console.Read();
        } catch (Exception e) {
            Console.WriteLine($"Something went wrong");
        }
    }

    public void ProcessOrder(string action, string username, int quantity, int price, int ID)
    {
        // Check if there is a corrosponding order, and if so then do something
        // Otherwise add to orders
        
        // Find the stock
        Stock stock = data.stocks.FirstOrDefault(stock => stock.ID == ID);

        if (stock != null)
        {
            Order order = new Order(username, quantity, price, stock, action);
            Order? otherOrder = data.CheckCorrospondingOrder(order);

            if (otherOrder == null)
            {
                Console.WriteLine($"Order received with no corresponding order: {order.ToString()}");
                // No order, create but don't transfer
                if (action == "sell")
                { data.sellOrders.Add(order); }
                else if (action == "buy")
                { data.buyOrders.Add(order);}
            }

            else
            {
                // Order found, do the swap
                Console.WriteLine($"Order received with corresponding order: {order.ToString()} and {otherOrder.ToString()}");
                data.DoSwap(order, otherOrder);
            }
        
            // Otherwise add to orders
        }
        else
        {
            Console.WriteLine($"Stock ID {ID} not found");
        }
        
        SaveToFile();
    }

    public void SaveToFile()
    {
        string dataText = JsonConvert.SerializeObject(data);
        File.WriteAllText("data.json", dataText);
    }

    public async Task UpdateTrades(int port)
    {
        var factory = new ConnectionFactory();
        factory.AutomaticRecoveryEnabled = true;
        factory.Port = port;

        try
        {
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var args = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };
            args.Add("x-message-ttl", 1000); // Make messages only last 1 second
        
            await channel.QueueDeclareAsync(queue: "Trades", durable: true, exclusive: false, autoDelete: false,
                arguments: args);
        
            var message = "";
            foreach (var stock in data.stocks)
            {
                var history = data.tradeHistory.LastOrDefault(deal => deal.Item1.ID == stock.ID);

                if (history != null)
                {
                    double pricePerUnit = Math.Round((double)((double)history.Item2 / (double)history.Item3), 2,
                        MidpointRounding.AwayFromZero);
                    message += $"{stock.name},{stock.ID},{pricePerUnit};"; // broadcast stockID and last price per unit   
                }
            }
            var encodedMessage = Encoding.UTF8.GetBytes(message);
        
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "Trades", body: encodedMessage);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public async Task UpdateOffered(int port)
    {
        var factory = new ConnectionFactory();
        factory.Port = port;
        factory.AutomaticRecoveryEnabled = true;
        
        try
        {
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var args = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };
            args.Add("x-message-ttl", 1000); // Make messages only last 1 second
        
            await channel.QueueDeclareAsync(queue: "Offered", durable: true, exclusive: false, autoDelete: false,
                arguments: args);
        
            var message = "";
            foreach (var trade in data.buyOrders)
            {
                message += $"{trade.action},{trade.username},{trade.stock.name},{trade.stock.ID},{trade.quantity},{trade.price};"; // broadcast stockID and last price per unit   
            }
            foreach (var trade in data.sellOrders)
            {
                message += $"{trade.action},{trade.username},{trade.stock.name},{trade.stock.ID},{trade.quantity},{trade.price};"; // broadcast stockID and last price per unit   
            }
            var encodedMessage = Encoding.UTF8.GetBytes(message);
        
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "Offered", body: encodedMessage);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

public class Stock
{
    public int ID;
    public string name;
    public List<Tuple<string, int>> stockOwnership = new List<Tuple<string, int>>();

    public Stock(int ID, string name)
    {
        this.ID = ID;
        this.name = name;
    }

    public int Sell(string buyer, string seller, int quantity)
    {
        if (stockOwnership.FirstOrDefault(deal => deal.Item1 == seller) != null)
        {
            stockOwnership.Remove(stockOwnership.Where(deal => deal.Item1 == seller).FirstOrDefault());
            stockOwnership.Add(new Tuple<string, int>(buyer, quantity));
            return 0;
        }
        else if (stockOwnership.Where(deal => deal.Item1 == "").FirstOrDefault() != null)
        {
            stockOwnership.Remove(stockOwnership.Where(deal => deal.Item1 == "").FirstOrDefault());
            stockOwnership.Add(new Tuple<string, int>(buyer, quantity));
            return 0;
        }
        else
        {
            return 1; // Indicates there is no stock ownership
        }
    }
}

public class Order
{
    public string username;
    public int quantity;
    public int price;
    public Stock stock;
    public string action;

    public Order(string username, int quantity, int price, Stock stock, string action)
    {
        this.username = username;
        this.quantity = quantity;
        this.price = price;
        this.stock = stock;
        this.action = action;
    }

    public string ToString()
    {
        return $"{username} to {action} {quantity} units for ${price}";
    }
}

public class Data
{
    public List<Stock> stocks;
    public List<Order> sellOrders;
    public List<Order> buyOrders;
    public List<Tuple<Stock, int, int>> tradeHistory; // Stock, quantity, price

    public Data()
    {
        stocks = new List<Stock>();
        sellOrders = new List<Order>();
        buyOrders = new List<Order>();
        tradeHistory = new List<Tuple<Stock, int, int>>();    // Stores the history of trades
    }

    public int AddStock(string name)
    {
        stocks.Add(new Stock(stocks.Count + 1, name));
        return stocks.Last().ID;
    }
    
    public Order CheckCorrospondingOrder(Order order)
    {
        Order? otherOrder;
        switch (order.action)
        {
            case  "sell":
                otherOrder = buyOrders.FirstOrDefault(o => o.price == order.price && o.quantity == order.quantity && o.stock.ID == order.stock.ID);
                break;
            case  "buy":
                otherOrder = sellOrders.FirstOrDefault(o => o.price == order.price && o.quantity == order.quantity && o.stock.ID == order.stock.ID);
                break;
            default:
                throw new Exception($"Unknown action {order.action}");
        }
        return otherOrder;
    }

    public void DoSwap(Order order1, Order order2)
    {
        // Add to history
        Order sell = order1.action == "sell" ? order1 : order2;
        Order buy = order1.action == "buy" ? order1 : order2;

        tradeHistory.Add(new Tuple<Stock, int, int>(sell.stock, sell.quantity, sell.price));
        // send out message updating
        
        // Do the swap
        sell.stock.Sell(buy.username, sell.username, buy.quantity);
        
        // Remove orders from list
        if (sellOrders.Contains(sell)) { sellOrders.Remove(sell); Console.WriteLine("Order removed as it is completed.");}
        if (buyOrders.Contains(buy)) { buyOrders.Remove(buy); Console.WriteLine("Order removed as it is completed.");}
    }
}