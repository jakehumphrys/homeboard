using HomeBoard.Models;

namespace HomeBoard.Services;
using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;

public class RssService(HttpClient http, IMemoryCache cache)
{
    public async Task<List<FeedItem>> GetFeedAsync(string url, string source, int max = 20)
    {
        if (cache.TryGetValue(url, out List<FeedItem>? cached) && cached is not null)
            return cached;

        try
        {
            await using var stream = await http.GetStreamAsync(url);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);

            var items = feed.Items
                .Select(i => new FeedItem(
                    i.Title?.Text ?? "(no title)",
                    i.Links.FirstOrDefault()?.Uri.ToString() ?? "", source,
                    (i.PublishDate != default ? i.PublishDate : i.LastUpdatedTime).LocalDateTime))
                .DistinctBy(i => i.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(i => i.Date)
                .Take(max)
                .ToList();

            cache.Set(url, items, TimeSpan.FromMinutes(15));
            return items;
        }
        catch (Exception)
        {
            return []; // add logging here later
        }
    }
}