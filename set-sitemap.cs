using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Sitemap
{
    public static class SetSitemap
    {
        public class SitemapData
        {
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public int UrlCount { get; set; }
            public int UniqueUrlCount { get; set; }
            public int BlogUrlCount { get; set; }
            public int ResourcesUrlCount { get; set; }
            public int SamplesUrlCount { get; set; }
            public int TemplatesUrlCount { get; set; }
            public int UpdatesUrlCount { get; set; }
            public int VideoUrlCount { get; set; }
        }

        [FunctionName("set-sitemap")]
        [return: Table("sitemapint")]
        public static SitemapData Run(
            [TimerTrigger("0 */20 * * * *")]TimerInfo myTimer,
            ILogger log)
        {
            var pageNum = 1;
            var completed = false;
            var baseUrl = "https://azure.microsoft.com/robotsitemap/en-us/{0}/";
            List<string> urls = new List<string>();

            while (!completed)
            {
                try
                {
                    WebRequest request = WebRequest.Create(string.Format(baseUrl, pageNum));
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);
                    string payload = reader.ReadToEnd();
                    log.LogInformation($"Processing page {pageNum}...");

                    var matches = Regex.Matches(payload, @"<loc>(.*?)<\/loc>");
                    if (matches.Count > 0)
                    {
                        foreach (Match match in matches)
                        {
                            urls.Add(match.Groups[1].Value);
                        }
                        log.LogInformation($"found {matches.Count} URLs.\n");
                    }
                    else
                    {
                        completed = true;
                        log.LogInformation($"found 0 URLs -- completed!\n");
                    }

                    response.Close();
                }
                catch (WebException e)
                {
                }

                pageNum++;
            }

            log.LogInformation($"Found {urls.Count()} URLs, {urls.Distinct().Count()} unique.");
            log.LogInformation($"Writing to Storage.");

            var distinctUrls = urls.Distinct();

            return new SitemapData
            {
                PartitionKey = "Map",
                RowKey = Guid.NewGuid().ToString(),
                UrlCount = urls.Count(),
                UniqueUrlCount = distinctUrls.Count(),
                BlogUrlCount = distinctUrls.Where(x => Regex.IsMatch(x, @"https:\/\/azure\.microsoft\.com\/en-us\/blog\/(.*?)\/")).Count(),
                ResourcesUrlCount = distinctUrls.Where(x => Regex.IsMatch(x, @"https:\/\/azure\.microsoft\.com\/en-us\/resources\/(.*?)\/([a-z]{2}-[a-z]{2})\/")).Count(),
                SamplesUrlCount = distinctUrls.Where(x => Regex.IsMatch(x, @"https:\/\/azure\.microsoft\.com\/en-us\/resources\/samples\/(.*?)\/")).Count(),
                TemplatesUrlCount = distinctUrls.Where(x => Regex.IsMatch(x, @"https:\/\/azure\.microsoft\.com\/en-us\/resources\/templates\/(.*?)\/")).Count(),
                UpdatesUrlCount = distinctUrls.Where(x => Regex.IsMatch(x, @"https:\/\/azure\.microsoft\.com\/en-us\/updates\/(.*?)\/")).Count(),
                VideoUrlCount = distinctUrls.Where(x => Regex.IsMatch(x, @"https:\/\/azure\.microsoft\.com\/en-us\/resources\/videos\/(.*?)\/")).Count()
            };
        }
    }
}
