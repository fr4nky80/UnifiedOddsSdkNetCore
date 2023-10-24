﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dawn;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Sportradar.OddsFeed.SDK.Api.Internal.ApiAccess;
using Sportradar.OddsFeed.SDK.Api.Internal.Caching;
using Sportradar.OddsFeed.SDK.Common;
using Sportradar.OddsFeed.SDK.Common.Exceptions;
using Sportradar.OddsFeed.SDK.Common.Internal;
using Sportradar.OddsFeed.SDK.Common.Internal.Extensions;
using Sportradar.OddsFeed.SDK.Entities.Rest.Internal.Caching.Events;
using Sportradar.OddsFeed.SDK.Entities.Rest.Internal.Dto;
using Sportradar.OddsFeed.SDK.Entities.Rest.Internal.Enums;
using Sportradar.OddsFeed.SDK.Entities.Rest.Internal.InternalEntities;

namespace Sportradar.OddsFeed.SDK.Entities.Rest.Internal.MarketNames
{
    /// <summary>
    /// A <see cref="IVariantDescriptionsCache" /> implementation used to store <see cref="IVariantDescription"/> for variant markets (static)
    /// </summary>
    /// <seealso cref="IVariantDescriptionsCache" />
    internal class VariantDescriptionListCache : SdkCache, IVariantDescriptionsCache
    {
        /// <summary>
        /// A <see cref="ICacheStore{T}"/> used to store variant descriptions
        /// </summary>
        private readonly ICacheStore<string> _cache;

        /// <summary>
        /// The <see cref="IDataRouterManager"/> used to obtain data via REST request
        /// </summary>
        private readonly IDataRouterManager _dataRouterManager;

        /// <summary>
        /// The <see cref="IMappingValidatorFactory"/> used to construct <see cref="IMappingValidator"/> instances for market mappings
        /// </summary>
        private readonly IMappingValidatorFactory _mappingValidatorFactory;

        /// <summary>
        /// The <see cref="ISdkTimer"/> instance used to periodically fetch market descriptors
        /// </summary>
        private readonly ISdkTimer _timer;

        /// <summary>
        /// A <see cref="IReadOnlyCollection{CultureInfo}"/> specifying the languages for which the data should be pre-fetched
        /// </summary>
        private readonly IReadOnlyCollection<CultureInfo> _prefetchLanguages;

        /// <summary>
        /// A <see cref="IList{CultureInfo}"/> used to store languages for which the data was already fetched (al least once)
        /// </summary>
        private readonly IList<CultureInfo> _fetchedLanguages = new List<CultureInfo>();

        /// <summary>
        /// A <see cref="SemaphoreSlim"/> instance to synchronize access from multiple threads
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        /// <summary>
        /// A <see cref="SemaphoreSlim"/> used to synchronize merging on cache item
        /// </summary>
        private readonly SemaphoreSlim _semaphoreCacheMerge = new SemaphoreSlim(1);

        /// <summary>
        /// Value indicating whether at least one fetch caused by the timer was done.
        /// </summary>
        private bool _hasTimerElapsedOnce;

        /// <summary>
        /// Value indicating whether the current instance was already disposed
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvariantMarketDescriptionCache"/> class
        /// </summary>
        /// <param name="cache">A <see cref="ICacheStore{T}"/> used to store market descriptors</param>
        /// <param name="dataRouterManager">A <see cref="IDataRouterManager"/> used to fetch data</param>
        /// <param name="mappingValidatorFactory">A <see cref="IMappingValidatorFactory"/> used to construct <see cref="IMappingValidator"/> instances for market mappings</param>
        /// <param name="timer">The <see cref="ISdkTimer"/> instance used to periodically fetch market descriptors</param>
        /// <param name="prefetchLanguages">A <see cref="IReadOnlyCollection{CultureInfo}"/> specifying the languages for which the data should be pre-fetched</param>
        /// <param name="cacheManager">A <see cref="ICacheManager"/> used to interact among caches</param>
        public VariantDescriptionListCache(ICacheStore<string> cache,
                                           IDataRouterManager dataRouterManager,
                                           IMappingValidatorFactory mappingValidatorFactory,
                                           ISdkTimer timer,
                                           IReadOnlyCollection<CultureInfo> prefetchLanguages,
                                           ICacheManager cacheManager)
            : base(cacheManager)
        {
            Guard.Argument(cache, nameof(cache)).NotNull();
            Guard.Argument(dataRouterManager, nameof(dataRouterManager)).NotNull();
            Guard.Argument(mappingValidatorFactory, nameof(mappingValidatorFactory)).NotNull();
            Guard.Argument(timer, nameof(timer)).NotNull();
            Guard.Argument(prefetchLanguages, nameof(prefetchLanguages)).NotNull().NotEmpty();

            _cache = cache;
            _dataRouterManager = dataRouterManager;
            _mappingValidatorFactory = mappingValidatorFactory;
            _timer = timer;
            _prefetchLanguages = prefetchLanguages;
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        /// <summary>
        /// Invoked when the <c>_timer</c> ticks in order to periodically fetch market descriptions for configured languages
        /// </summary>
        /// <param name="sender">The <see cref="ISdkTimer"/> raising the event</param>
        /// <param name="e">A <see cref="EventArgs"/> instance containing the event data</param>
        private async void OnTimerElapsed(object sender, EventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            //when the timer first elapses fetch data only for languages which were not yet fetched. On subsequent timer elapses fetch data for all configured languages
            var languagesToFetch = _prefetchLanguages.Where(language => !_fetchedLanguages.Contains(language) || _hasTimerElapsedOnce).ToList();

            if (!languagesToFetch.Any())
            {
                _hasTimerElapsedOnce = true;
                return;
            }
            try
            {
                _fetchedLanguages.Clear();
                ExecutionLog.LogDebug($"Loading variant market descriptions for [{string.Join(",", languagesToFetch.Select(l => l.TwoLetterISOLanguageName))}] (timer).");
                await GetVariantDescriptionInternalAsync("0", languagesToFetch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is CommunicationException || ex is DeserializationException || ex is FormatException)
                {
                    var languagesString = string.Join(",", languagesToFetch.Select(l => l.TwoLetterISOLanguageName));
                    ExecutionLog.LogWarning(ex, $"An error occurred while periodically fetching variant market description of languages {languagesString}");
                    return;
                }
                if (ex is ObjectDisposedException disposedException)
                {
                    ExecutionLog.LogWarning($"An error occurred while periodically fetching variant market descriptions because the object graph is being disposed. Object causing the exception:{disposedException.ObjectName}");
                    return;
                }
                if (ex is TaskCanceledException)
                {
                    ExecutionLog.LogWarning(ex, "An error occurred while periodically fetching variant market descriptions because the object graph is being disposed.");
                    return;
                }
                ExecutionLog.LogWarning(ex, "An error occurred while periodically fetching variant market description.");
            }

            _hasTimerElapsedOnce = true;
        }

        /// <summary>
        /// Gets the <see cref="VariantDescriptionCacheItem"/> specified by it's id from the local cache
        /// </summary>
        /// <param name="id">The id of the <see cref="VariantDescriptionCacheItem"/> to get</param>
        /// <returns>The <see cref="VariantDescriptionCacheItem"/> retrieved from the cache or a null reference if item is not found</returns>
        private VariantDescriptionCacheItem GetItemFromCache(string id)
        {
            _semaphoreCacheMerge.Wait();
            var cacheItem = _cache.Get(id);
            _semaphoreCacheMerge.ReleaseSafe();
            return (VariantDescriptionCacheItem)cacheItem;
        }

        /// <summary>
        /// Asynchronously gets the <see cref="VariantDescriptionCacheItem"/> specified by it's id. If the item is not found in local cache, all items for specified
        /// language are fetched from the service and stored/merged into the local cache.
        /// </summary>
        /// <param name="id">The id of the <see cref="VariantDescriptionCacheItem"/> instance to get</param>
        /// <param name="cultures">A <see cref="IReadOnlyCollection{CultureInfo}"/> specifying the languages which the returned item must contain</param>
        /// <returns>A <see cref="Task"/> representing the async operation</returns>
        /// <exception cref="CommunicationException">An error occurred while accessing the remote party</exception>
        /// <exception cref="DeserializationException">An error occurred while deserializing fetched data</exception>
        /// <exception cref="FormatException">An error occurred while mapping deserialized entities</exception>
        private async Task<VariantDescriptionCacheItem> GetVariantDescriptionInternalAsync(string id, IReadOnlyCollection<CultureInfo> cultures)
        {
            Guard.Argument(cultures, nameof(cultures)).NotNull().NotEmpty();

            var description = GetItemFromCache(id);
            if (description != null && !LanguageHelper.GetMissingCultures(cultures, description.FetchedLanguages).Any())
            {
                return description;
            }

            try
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);

                description = GetItemFromCache(id);
                var missingLanguages = LanguageHelper.GetMissingCultures(cultures, description?.FetchedLanguages);

                if (missingLanguages.Any())
                {
                    // dont call for already fetched languages
                    missingLanguages = LanguageHelper.GetMissingCultures(missingLanguages, _fetchedLanguages);
                }

                if (!missingLanguages.Any())
                {
                    return description;
                }

                var cultureTaskDictionary = missingLanguages.ToDictionary(l => l, l => _dataRouterManager.GetVariantDescriptionsAsync(l));
                await Task.WhenAll(cultureTaskDictionary.Values).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException disposedException)
                {
                    ExecutionLog.LogWarning($"An error occurred while fetching market descriptions because the object graph is being disposed. Object causing the exception: {disposedException.ObjectName}.");
                    return null;
                }
                throw;
            }
            finally
            {
                if (!_isDisposed)
                {
                    _semaphore.ReleaseSafe();
                }
            }

            description = GetItemFromCache(id);
            return description != null && !LanguageHelper.GetMissingCultures(cultures, description.FetchedLanguages).Any()
                ? description
                : null;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (disposing)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;

                _semaphore.Dispose();
                _semaphoreCacheMerge.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the market descriptor.
        /// </summary>
        /// <param name="variantId">The variant identifier</param>
        /// <param name="cultures">The cultures</param>
        /// <exception cref="CacheItemNotFoundException">The requested key was not found in the cache and could not be loaded</exception>
        public async Task<IVariantDescription> GetVariantDescriptorAsync(string variantId, IReadOnlyCollection<CultureInfo> cultures)
        {
            VariantDescriptionCacheItem cacheItem;
            try
            {
                cacheItem = await GetVariantDescriptionInternalAsync(variantId, cultures).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is CommunicationException || ex is DeserializationException || ex is MappingException)
                {
                    throw new CacheItemNotFoundException("The requested key was not found in the cache", variantId, ex);
                }
                throw;
            }

            return cacheItem == null
                ? null
                : new VariantDescription(cacheItem, cultures);
        }

        /// <summary>
        /// Asynchronously loads the list of market descriptions from the Sports API
        /// </summary>
        /// <returns>Returns true if the action succeeded</returns>
        public async Task<bool> LoadMarketDescriptionsAsync()
        {
            try
            {
                ExecutionLog.LogDebug($"Loading variant market descriptions for [{string.Join(", ", _prefetchLanguages.Select(l => l.TwoLetterISOLanguageName))}] (user request).");
                _fetchedLanguages.Clear();
                await GetVariantDescriptionInternalAsync("0", _prefetchLanguages).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ExecutionLog.LogWarning(ex, $"An error occurred while fetching market descriptions variant list.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Set the list of <see cref="DtoType"/> in the this cache
        /// </summary>
        public override void SetDtoTypes()
        {
            RegisteredDtoTypes = new List<DtoType>
                                 {
                                     DtoType.VariantDescriptionList
                                 };
        }

        /// <summary>
        /// Deletes the item from cache
        /// </summary>
        /// <param name="id">A <see cref="Urn" /> representing the id of the item in the cache to be deleted</param>
        /// <param name="cacheItemType">A cache item type</param>
        public override void CacheDeleteItem(Urn id, CacheItemType cacheItemType)
        {
            if (id != null && (cacheItemType == CacheItemType.All || cacheItemType == CacheItemType.MarketDescription))
            {
                _cache.Remove(id.Id.ToString());
            }
        }

        /// <summary>
        /// Deletes the item from cache
        /// </summary>
        /// <param name="id">A string representing the id of the item in the cache to be deleted</param>
        /// <param name="cacheItemType">A cache item type</param>
        public override void CacheDeleteItem(string id, CacheItemType cacheItemType)
        {
            if (cacheItemType == CacheItemType.All || cacheItemType == CacheItemType.VariantDescription)
            {
                _cache.Remove(id);
            }
        }

        /// <summary>
        /// Does item exists in the cache
        /// </summary>
        /// <param name="id">A <see cref="Urn" /> representing the id of the item to be checked</param>
        /// <param name="cacheItemType">A cache item type</param>
        /// <returns><c>true</c> if exists, <c>false</c> otherwise</returns>
        public override bool CacheHasItem(Urn id, CacheItemType cacheItemType)
        {
            if (id != null)
            {
                if (cacheItemType == CacheItemType.All || cacheItemType == CacheItemType.VariantDescription)
                {
                    return _cache.Contains(id.Id.ToString());
                }
            }
            return false;
        }

        /// <summary>
        /// Does item exists in the cache
        /// </summary>
        /// <param name="id">A string representing the id of the item to be checked</param>
        /// <param name="cacheItemType">A cache item type</param>
        /// <returns><c>true</c> if exists, <c>false</c> otherwise</returns>
        public override bool CacheHasItem(string id, CacheItemType cacheItemType)
        {
            if (cacheItemType == CacheItemType.All || cacheItemType == CacheItemType.VariantDescription)
            {
                return _cache.Contains(id);
            }

            return false;
        }

        /// <inheritdoc />
        protected override async Task<bool> CacheAddDtoItemAsync(Urn id, object item, CultureInfo culture, DtoType dtoType, ISportEventCacheItem requester)
        {
            if (_isDisposed)
            {
                return false;
            }

            var saved = false;
            switch (dtoType)
            {
                case DtoType.Category:
                    break;
                case DtoType.Competitor:
                    break;
                case DtoType.CompetitorProfile:
                    break;
                case DtoType.SimpleTeamProfile:
                    break;
                case DtoType.Fixture:
                    break;
                case DtoType.MarketDescription:
                    break;
                case DtoType.MarketDescriptionList:
                    break;
                case DtoType.MatchSummary:
                    break;
                case DtoType.MatchTimeline:
                    break;
                case DtoType.PlayerProfile:
                    break;
                case DtoType.RaceSummary:
                    break;
                case DtoType.Sport:
                    break;
                case DtoType.SportList:
                    break;
                case DtoType.SportEventStatus:
                    break;
                case DtoType.SportEventSummary:
                    break;
                case DtoType.SportEventSummaryList:
                    break;
                case DtoType.Tournament:
                    break;
                case DtoType.TournamentInfo:
                    break;
                case DtoType.TournamentSeasons:
                    break;
                case DtoType.VariantDescription:
                    break;
                case DtoType.VariantDescriptionList:
                    if (item is EntityList<VariantDescriptionDto> variantDescriptionList)
                    {
                        await MergeAsync(culture, new ReadOnlyCollection<VariantDescriptionDto>(variantDescriptionList.Items.ToList())).ConfigureAwait(false);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(EntityList<VariantDescriptionDto>), item.GetType(), ExecutionLog);
                    }
                    break;
                case DtoType.Lottery:
                    break;
                case DtoType.LotteryDraw:
                    break;
                case DtoType.LotteryList:
                    break;
                case DtoType.BookingStatus:
                    break;
                case DtoType.SportCategories:
                    break;
                case DtoType.AvailableSelections:
                    break;
                case DtoType.TournamentInfoList:
                    break;
                case DtoType.PeriodSummary:
                    break;
                case DtoType.Calculation:
                    break;
                default:
                    ExecutionLog.LogWarning("Trying to add unchecked dto type: {DtoType} for id: {Id}", dtoType, id);
                    break;
            }
            return saved;
        }

        /// <summary>
        /// Merges the provided descriptions with those found in cache
        /// </summary>
        /// <param name="culture">A <see cref="CultureInfo"/> specifying the language of the <c>descriptions</c></param>
        /// <param name="descriptions">A <see cref="IReadOnlyCollection{MarketDescriptionDto}"/> containing market descriptions in specified language</param>
        private async Task MergeAsync(CultureInfo culture, IReadOnlyCollection<VariantDescriptionDto> descriptions)
        {
            Guard.Argument(culture, nameof(culture)).NotNull();
            Guard.Argument(descriptions, nameof(descriptions)).NotNull().NotEmpty();
            try
            {
                await _semaphoreCacheMerge.WaitAsync().ConfigureAwait(false);
                foreach (var marketDescription in descriptions)
                {
                    var cachedItem = (VariantDescriptionCacheItem)_cache.Get(marketDescription.Id);
                    if (cachedItem == null)
                    {
                        cachedItem = VariantDescriptionCacheItem.Build(marketDescription, _mappingValidatorFactory, culture, CacheName);
                        _cache.Add(cachedItem.Id, cachedItem);
                    }
                    else
                    {
                        cachedItem.Merge(marketDescription, culture);
                    }
                }
                _fetchedLanguages.Add(culture);
            }
            catch (Exception e)
            {
                if (!(e is InvalidOperationException))
                {
                    throw;
                }
                ExecutionLog.LogWarning(e, "Mapping validation for VariantDescriptionCacheItem failed");
            }
            finally
            {
                if (!_isDisposed)
                {
                    _semaphoreCacheMerge.ReleaseSafe();
                }
            }
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult(HealthCheckResult.Healthy($"Cache has {_cache.Count().ToString()} items"));
        }
    }
}
