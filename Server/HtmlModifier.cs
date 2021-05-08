using System;
using System.Text;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using static hypixel.ItemPrices;

namespace hypixel
{
    public class HtmlModifier
    {
        public static async Task<string> ModifyContent(string path, byte[] contents)
        {
            var defaultText = "Browse over 100 million auctions, and the bazzar of Hypixel SkyBlock";
            var defaultTitle = "Skyblock Auction House History";
            string parameter = "";
            var urlParts = path.Split('/', '?', '#');
            if (urlParts.Length > 2)
                parameter = urlParts[2];
            string description = defaultText;
            string longDescription = null;
            string title = defaultTitle;
            string imageUrl = "https://sky.coflnet.com/logo192.png";
            string keyword = "";

            string html = Encoding.UTF8.GetString(contents);

            // try to fill in title
            if (path.Contains("auction/") || path.Contains("a/"))
            {
                // is an auction
                using (var context = new HypixelContext())
                {
                    var result = context.Auctions.Where(a => a.Uuid == parameter)
                            .Select(a => new { a.Tag, a.AuctioneerId, a.ItemName, a.End, bidCount = a.Bids.Count, a.Tier, a.Category }).FirstOrDefault();
                    if (result != null)
                    {
                        var playerName = PlayerSearch.Instance.GetNameWithCache(result.AuctioneerId);
                        title = $"Auction for {result.ItemName} by {playerName}";
                        description = $"{title} ended on {result.End} with {result.bidCount} bids, Category: {result.Category}, {result.Tier}.";
                        longDescription = description 
                            + $"<ul><li> <a href=\"/player/{result.AuctioneerId}/{playerName}\"> other auctions by {playerName} </a></li>"
                            + $" <li><a href=\"/item/{result.Tag}/{result.ItemName}\"> more auctions for {result.ItemName} </a></li></ul>";
                        keyword = $"{result.ItemName},{playerName}";

                        if (!string.IsNullOrEmpty(result.Tag))
                            imageUrl = "https://sky.lea.moe/item/" + result.Tag;
                        else
                            imageUrl = "https://crafatar.com/avatars/" + result.AuctioneerId;

                    }
                }
            }
            if (path.Contains("player/") || path.Contains("p/"))
            {
                if (parameter.Length < 30)
                {
                    var uuid = PlayerSearch.Instance.GetIdForName(parameter);
                    return Redirect(uuid, "player", parameter);
                }
                keyword = PlayerSearch.Instance.GetNameWithCache(parameter);
                if (urlParts.Length <= 3)
                    path += $"/{keyword}";
                title = $"{keyword} Auctions and bids";
                description = $"Auctions and bids for {keyword} in hypixel skyblock.";

                string auctionAndBids = await GetAuctionAndBids(parameter);

                longDescription = description + auctionAndBids;
                imageUrl = "https://crafatar.com/avatars/" + parameter;
            }

            if (path.Contains("item/") || path.Contains("i/"))
            {
                if (path.Contains("i/"))
                    return AddItemRedirect(parameter, keyword);
                if (parameter.ToUpper() != parameter && !parameter.StartsWith("POTION"))
                {
                    // likely not a tag
                    parameter = HttpUtility.UrlDecode(parameter);
                    var thread = ItemDetails.Instance.Search(parameter, 1);
                    thread.Wait();
                    var item = thread.Result.FirstOrDefault();
                    keyword = item?.Name;
                    parameter = item?.Tag;
                    return AddItemRedirect(parameter, keyword);
                }
                else
                {
                    keyword = ItemDetails.TagToName(parameter);
                }

                var i = ItemDetails.Instance.GetDetailsWithCache(parameter);
                path = CreateCanoicalPath(urlParts, i);

                title = $"{keyword} price ";
                description = $"Price for item {keyword} in hypixel SkyBlock";
                longDescription = description
                + AddAlternativeNames(i);

                longDescription += await GetRecentAuctions(i.Tag);
                imageUrl = "https://sky.lea.moe/item/" + parameter;
            }
            title += " | Hypixel SkyBlock Auction house history tracker";
            if (longDescription == null)
                longDescription = description;
            // shrink to fit
            while (title.Length > 65)
            {
                title = title.Substring(0, title.LastIndexOf(' '));
            }
            if (path == "/index.html")
            {
                path = "";
            }

            var newHtml = html
                        .Replace(defaultText, description)
                        .Replace(defaultTitle, title)
                        .Replace("</title>", $"</title><meta property=\"keywords\" content=\"{keyword},hypixel,skyblock,auction,history,bazaar,tracker\" /><meta property=\"og:image\" content=\"{imageUrl}\" />"
                            + $"<link rel=\"canonical\" href=\"https://sky.coflnet.com/{path}\" />")
                        .Replace("</body>", PopularPages(title, longDescription) + "</body>");
            return newHtml;
        }

        private static string CreateCanoicalPath(string[] urlParts, DBItem i)
        {
            return $"/item/{i.Tag}" + (urlParts.Length > 3 ? $"/{ItemReferences.RemoveReforgesAndLevel(HttpUtility.UrlDecode(urlParts[3])) }" : "");
        }

        private static async Task<string> GetAuctionAndBids(string parameter)
        {
            var bidsTask =  Server.ExecuteCommandWithCache<
            PaginatedRequestCommand<PlayerBidsCommand.BidResult>.Request,
            List<PlayerBidsCommand.BidResult>>("playerBids", new PaginatedRequestCommand<PlayerBidsCommand.BidResult>
            .Request()
            { Amount = 20, Offset = 0, Uuid = parameter });

            var auctions = await Server.ExecuteCommandWithCache<
            PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>.Request,
            List<PlayerAuctionsCommand.AuctionResult>>("playerBids", new PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>
            .Request()
            { Amount = 20, Offset = 0, Uuid = parameter });

            var sb = new StringBuilder();

            sb.Append("Auctions: <ul>");
            var bids = await bidsTask; 
            foreach (var item in bids)
            {
                sb.Append($"<li><a href=\"/auction/{item.AuctionId}\">{item.ItemName}</a></li>");
            }
            sb.Append("</ul>");

            sb.Append("Bids: <ul>");
            foreach (var item in bids)
            {
                sb.Append($"<li><a href=\"/auction/{item.AuctionId}\">{item.ItemName}</a></li>");
            }
            sb.Append("</ul>");

            var auctionAndBids = sb.ToString();
            return auctionAndBids;
        }

        private static async Task<string> GetRecentAuctions(string tag)
        {
            var result = await Server.ExecuteCommandWithCache<ItemSearchQuery,IEnumerable<AuctionPreview>>("recentAuctions",new ItemSearchQuery(){
                name = tag,
                Start = DateTime.Now - TimeSpan.FromHours(1)
            });
            var sb = new StringBuilder(200);
            sb.Append("Newest auctions: <ul>");
            foreach (var item in result)
            {
                sb.Append($"<li><a href=\"/auction/{item.Uuid}\">auction by {PlayerSearch.Instance.GetNameWithCache(item.Seller)}</a></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string AddAlternativeNames(DBItem i)
        {
            if (i.Names == null || i.Names.Count == 0)
                return "";
            return ". Found this item with the following names: " + i.Names.Select(n => n.Name).Aggregate((a, b) => $"{a}, {b}").TrimEnd(' ', ',')
            + ". This are all names under wich we found auctins for this item in the ah. It may be historical names or names in a different language.";
        }

        private static string AddItemRedirect(string parameter, string name)
        {
            return Redirect(parameter, "item", name);
        }

        private static string Redirect(string parameter, string type, string seoTerm = null)
        {
            return $"https://sky.coflnet.com/{type}/{parameter}" + seoTerm == null ? "" : $"/{seoTerm}";
        }

        private static string PopularPages(string title, string description)
        {
            var r = new Random();
            var recentSearches = SearchService.Instance.GetPopularSites().OrderBy(x => r.Next());
            var body = $@"<details style=""padding:10%;""><sumary>Description without javascript.</sumary>
                    This only updates when you reload the page so don't get confused :).
                    <h1>{title}</h1><p>{description}</p><p>View, search, browse, and filter by reforge or enchantment. "
                    + "You can find all current and historic prices for the auction house and bazaar on this web tracker. "
                    + "We are tracking about 175 million auctions. "
                    + "Saved more than 230 million bazaar prices in intervalls of 10 seconds. "
                    + "Furthermore there are over two million <a href=\"/players\"> skyblock players</a> that you can search by name and browse through the auctions they made over the past two years. "
                    + "The autocomplete search is ranked by popularity and allows you to find whatever <a href=\"/items\">item</a> you want faster. "
                    + "New Items are added automatically and available within two miniutes after the first auction is startet. "
                    + "We allow you to subscribe to auctions, item prices and being outbid with more to come. "
                    + "Quick urls allow you to link to specific sites. /p/Steve or /i/Oak allow you to create a link without visiting the site first. "
                    + "Please use the contact on the Feedback site to send us suggestions or bug reports. ";
            if (recentSearches.Any())
                body += "<h2>Other Players and item auctions:</h2>"
                    + recentSearches
                    .Take(8)
                .Select(p => $"<a href=\"https://sky.coflnet.com/{p.Url}\">{p.Title} </a>")
                .Aggregate((a, b) => a + b);
            return body + "</details>";
        }
    }
}