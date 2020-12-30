using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using static hypixel.FullSearchCommand;

namespace hypixel
{
    public class SearchService
    {
        const int targetAmount = 5;

        ConcurrentDictionary<string, CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();
        ConcurrentQueue<string> cacheKeyUpdate = new ConcurrentQueue<string>();

        private int updateCount = 0;
        public static SearchService Instance { get; private set; }

        internal List<SearchResultItem> Search(string search)
        {
            if (search.Length > 40)
                return null;
            if (!cache.TryGetValue(search, out CacheItem result))
            {
                result = CreateAndCache(search);
            }
            result.hitCount++;
            return result.response;
        }

        private CacheItem CreateAndCache(string search)
        {
            CacheItem result;
            var response = CreateResponse(search);
            result = new CacheItem(response);
            cache.AddOrUpdate(search, result, (key, old) => result);
            return result;
        }

        static SearchService()
        {
            Instance = new SearchService();
        }

        private void Work()
        {
            using(var context = new HypixelContext())
            {

                if (updateCount % 1000 == 500)
                    ShrinkHits(context);
                if (updateCount % 6 == 5)
                    PartialUpdateCache(context);
                ItemDetails.Instance.SaveHits(context);
                PlayerSearch.Instance.SaveHits(context);
                context.SaveChanges();
            }
            updateCount++;
        }

        private void PartialUpdateCache(HypixelContext context, int maxUpdateCount = 10)
        {
            var maxAge = DateTime.Now - TimeSpan.FromHours(1);
            var toDelete = cache.Where(item => item.Value.hitCount < 2 &&
                    item.Key.Length > 2 &&
                    item.Value.created < maxAge)
                .Select(item => item.Key).ToList();

            foreach (var item in toDelete)
            {
                cache.TryRemove(item, out CacheItem element);
            }
            var update = cache.Where(el => el.Value.hitCount > 1)
                .OrderBy(el => el.Value.created)
                .OrderByDescending(el=>el.Value.hitCount)
                .Select(el => el.Key)
                .Take(maxUpdateCount)
                .ToList();
            foreach (var item in update)
            {
                CreateAndCache(item);
            }
            if (update.Any())
                Console.WriteLine($"cached search for {update.First()} ");
        }

        private void ShrinkHits(HypixelContext context)
        {
            Console.WriteLine("shrinking hits !!");
            ShrinkHitsType(context, context.Players);
            ShrinkHitsType(context, context.Items);
        }

        private static void ShrinkHitsType(HypixelContext context, IEnumerable<IHitCount> source)
        {
            var res = source.Where(p => p.HitCount > 0);
            foreach (var player in res)
            {
                player.HitCount = player.HitCount * 9 / 10; // - 1; players that were searched once will be prefered forever
                context.Update(player);
            }
        }

        internal void RunForEver()
        {
            Task.Run(() =>
            {
                PopulateCache();
                while (true)
                {
                    try
                    {
                        Work();
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error("Searchserive got an error " + e.Message + e.StackTrace);
                    }
                    Thread.Sleep(5000);

                }
            });
        }

        private void PopulateCache()
        {
            var letters = "abcdefghijklmnopqrstuvwxyz1234567890_";

            foreach (var letter in letters)
            {
                CreateAndCache(letter.ToString());
                Thread.Sleep(100);
            }
            CreateAndCache("");
            Console.WriteLine("populated Cache");
        }

        private static List<SearchResultItem> CreateResponse(string search)
        {
            var result = new List<SearchResultItem>();

            var items = ItemDetails.Instance.Search(search, 20);
            var players = PlayerSearch.Instance.Search(search, targetAmount, false);

            result.AddRange(items.Select(item => new SearchResultItem(item)));
            result.AddRange(players.Select(player => new SearchResultItem(player)));


            return result.OrderBy(r => r.Name?.Length - r.HitCount - (r.Name?.ToLower() == search.ToLower() ? 10000000 : 0))
                .Take(targetAmount).ToList();
        }

        class CacheItem
        {
            public List<SearchResultItem> response;
            public int hitCount;
            public DateTime created;

            public CacheItem(List<SearchResultItem> response)
            {
                this.response = response;
                this.created = DateTime.Now;
                this.hitCount = 0;
            }
        }

        [MessagePackObject]
        public class SearchResultItem
        {
            [Key("name")]
            public string Name;
            [Key("id")]
            public string Id;
            [Key("type")]
            public string Type;
            [Key("iconUrl")]
            public string IconUrl;
            //[IgnoreMember]
            [Key("hits")]
            public int HitCount;

            public SearchResultItem() { }

            public SearchResultItem(ItemDetails.ItemSearchResult item)
            {
                this.Name = item.Name;
                this.Id = item.Tag;
                this.Type = "item";
                if (!item.Tag.StartsWith("POTION") && !item.Tag.StartsWith("PET") && !item.Tag.StartsWith("RUNE"))
                    IconUrl = "https://sky.lea.moe/item/" + item.Tag;
                else
                    this.IconUrl = item.IconUrl;
                this.HitCount = item.HitCount;
            }

            public SearchResultItem(PlayerResult player)
            {
                this.Name = player.Name;
                this.Id = player.UUid;
                this.IconUrl = "https://crafatar.com/avatars/" + player.UUid;
                this.Type = "player";
                this.HitCount = player.HitCount;
            }
        }
    }

    public interface IHitCount
    {
        int HitCount { get; set; }
    }
}