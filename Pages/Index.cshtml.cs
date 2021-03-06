﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MemoryCache.Pages
{
    public static class CacheKey
    {
        public const string CacheEntry = "cacheEntry";
        public const string CacheEntryBackup = "cacheEntryBackup";
    }

    public class PageContent
    {
        public DateTime timestamp { get; set; }

        public string content { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _clientFactory;

        public PageContent Content;

        public IndexModel(IMemoryCache memoryCache, IHttpClientFactory clientFactory, ILogger<IndexModel> logger)
        {
            _cache = memoryCache;
            _clientFactory = clientFactory;
            _logger = logger;
        }

        public async Task OnGet()
        {
            var result = await _cache.GetOrCreateAsync<PageContent>(CacheKey.CacheEntry, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpiration = DateTime.Now.AddMinutes(1);

                var pageContent = await fetchContentAsync();

                if (!String.IsNullOrEmpty(pageContent.content))
                {
                    _logger.LogInformation("Response returned from server, update cache");

                    _cache.Set(CacheKey.CacheEntryBackup, pageContent);
                    return pageContent;
                }

                _logger.LogInformation("No response from server, revert to backup");
                return _cache.Get<PageContent>(CacheKey.CacheEntryBackup);
            });

            Content = result;
        }

        private async Task<PageContent> fetchContentAsync()
        {
            try
            {
                using (var client = _clientFactory.CreateClient())
                {
                    _logger.LogInformation("Fetching content");

                    HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, "https://data.nsw.gov.au/data/api/3/action/package_show?id=0a52e6c1-bc0b-48af-8b45-d791a6d8e289");
                    var response = await client.SendAsync(message);
                    response.EnsureSuccessStatusCode();

                    return new PageContent { timestamp = DateTime.Now, content = await response.Content.ReadAsStringAsync() };
                }
            }
            catch(HttpRequestException ex)
            {
                _logger.LogError($"Failed to fetch content: {ex.Message}");
                return null;
            }
        }
    }
}
