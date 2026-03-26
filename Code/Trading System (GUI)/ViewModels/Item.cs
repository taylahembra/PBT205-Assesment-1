using CommunityToolkit.Mvvm.ComponentModel;

namespace Trading_System.ViewModels;

public partial class Item : ViewModelBase
{
    [ObservableProperty] private string text;
    [ObservableProperty] private int id;

    public string username;
    public string stockname;
    public int stockID;
    public string type;
    public int quantity;
    public int price;
    
    public bool _lock = false;

    private BuySellViewModel model;

    public Item(string username, string stockname, int stockID,  string type, int quantity, int price, int id, BuySellViewModel model)
    {
        this.username = username;
        this.stockname = stockname;
        this.stockID = stockID;
        this.type = type;
        this.quantity = quantity;
        this.price = price;
        this.id = id;
        this.model = model;
        
        this.text = $"{username}: {quantity} units of {stockname} ({stockID}) for ${price}";
    }

    public void SubmitTrade()
    {
        model.SubmitTrade(id);
    }
}