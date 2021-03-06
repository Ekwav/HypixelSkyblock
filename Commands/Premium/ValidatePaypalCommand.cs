using System;
using System.Runtime.Serialization;
using Coflnet;
using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;

namespace hypixel
{
    public class ValidatePaypalCommand : Command
    {
        static string clientId = SimplerConfig.Config.Instance["PAYPAL_ID"];
        static string clientSecret = SimplerConfig.Config.Instance["PAYPAL_SECRET"];

        private ConcurrentCollections.ConcurrentHashSet<string> UsedIds = new ConcurrentCollections.ConcurrentHashSet<string>();

        public override void Execute(MessageData data)
        {
            var args = data.GetAs<Params>();
            OrdersGetRequest request = new OrdersGetRequest(args.OrderId);
            if (string.IsNullOrEmpty(clientId))
                throw new CoflnetException("unavailable", "checkout via paypal has not yet been enabled, please contact an admin");
            var client = new PayPalHttpClient(new LiveEnvironment(clientId, clientSecret));
            //3. Call PayPal to get the transaction
            PayPalHttp.HttpResponse response;
            try
            {
                response = client.Execute(request).Result;
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "payPalPayment");
                throw new CoflnetException("payment_failed", "The provided orderId has not vaid payment asociated");
            }
            //4. Save the transaction in your database. Implement logic to save transaction to your database for future reference.
            var result = response.Result<Order>();
            Console.WriteLine(JSON.Stringify(result));
            Console.WriteLine("Retrieved Order Status");
            Console.WriteLine("Status: {0}", result.Status);
            Console.WriteLine("Order Id: {0}", result.Id);
            AmountWithBreakdown amount = result.PurchaseUnits[0].AmountWithBreakdown;
            Console.WriteLine("Total Amount: {0} {1}", amount.CurrencyCode, amount.Value);
            if (result.Status != "COMPLETED")
                throw new CoflnetException("order_incomplete", "The order is not yet completed");

            if (UsedIds.Contains(args.OrderId))
                throw new CoflnetException("payment_timeout", "the provied order id was already used");

            if (DateTime.Parse(result.PurchaseUnits[0].Payments.Captures[0].UpdateTime) < DateTime.Now.Subtract(TimeSpan.FromHours(1)))
                throw new CoflnetException("payment_timeout", "the provied order id is too old, please contact support for manual review");
            var user = data.User;
            var days = args.Days;
            var transactionId = result.Id;
            UserService.Instance.SavePurchase(user, days, transactionId);




            UsedIds.Add(args.OrderId);
            FileController.AppendLineAs("purchases", JSON.Stringify(result));
            data.Ok();
        }

        [DataContract]
        public class Params
        {
            [DataMember(Name = "orderId")]
            public string OrderId;
            [DataMember(Name = "days")]
            public int Days;
        }
    }
}
