using System.Collections.Generic;
using System.Linq;

namespace hypixel
{
    public class AuctionSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var auctions = data.GetAs<List<AuctionSync>>();
            using (var context = new HypixelContext())
            {


                List<string> incomplete = new List<string>();

                foreach (var auction in auctions)
                {
                    var a = context.Auctions.Where(p => p.Uuid == auction.Id).Select(a => new { a.Id, a.HighestBidAmount }).FirstOrDefault();
                    if (a.HighestBidAmount == auction.HighestBid)
                        continue;
                    incomplete.Add(auction.Id);
                }
            }
        }
    }
}
