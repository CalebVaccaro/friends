// See https://aka.ms/new-console-template for more information

using friends;
using Microsoft.Extensions.Configuration;
using Stripe;

Console.WriteLine("Hello, World!");

var configuration = ConfigurationLoader.LoadConfiguration();
StripeConfiguration.ApiKey = configuration.GetConnectionString("StripeApiKey");

// var optionsProduct = new ProductCreateOptions()
// {
//     Name = "Friend Rental",
//     Description = "Rent a friend for a day"
// };
//
// var serviceProduct = new ProductService();
// Product product = serviceProduct.Create(optionsProduct);
// Console.WriteLine("Product created: " + product.Id);
//
// var optionsPrice = new PriceCreateOptions()
// {
//     Product = product.Id,
//     UnitAmount = 100,
//     Currency = "usd",
// };
//
// var servicePrice = new PriceService();
// Price price = servicePrice.Create(optionsPrice);
// Console.WriteLine("Price created: " + price.Id);

//prod_RugpykGd7V9c03
ExecuteSell("prod_RugpykGd7V9c03", 100, "usd");

PaymentIntent ExecuteSell(string productId, long amount, string currency)
{
    var options = new PaymentIntentCreateOptions
    {
        Amount = amount,
        Currency = currency,
        PaymentMethodTypes = ["card"],
        Metadata = new Dictionary<string, string>
        {
            { "product_id", productId }
        }
    };

    var service = new PaymentIntentService();
    PaymentIntent paymentIntent = service.Create(options);
    return paymentIntent;
}