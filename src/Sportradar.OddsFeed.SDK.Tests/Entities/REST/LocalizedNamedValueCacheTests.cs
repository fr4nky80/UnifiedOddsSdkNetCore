﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Moq;
using Sportradar.OddsFeed.SDK.Api.Internal.ApiAccess;
using Sportradar.OddsFeed.SDK.Api.Internal.Caching;
using Sportradar.OddsFeed.SDK.Api.Internal.Config;
using Sportradar.OddsFeed.SDK.Common.Enums;
using Sportradar.OddsFeed.SDK.Common.Internal;
using Sportradar.OddsFeed.SDK.Entities.Rest.Internal;
using Sportradar.OddsFeed.SDK.Tests.Common;
using Xunit;

namespace Sportradar.OddsFeed.SDK.Tests.Entities.Rest;

public class LocalizedNamedValueCacheTests
{
    private Mock<IDataFetcher> _fetcherMock;
    private Uri _enMatchStatusUri;
    private Uri _deMatchStatusUri;
    private Uri _huMatchStatusUri;
    private Uri _nlMatchStatusUri;

    private LocalizedNamedValueCache Setup(ExceptionHandlingStrategy exceptionStrategy, SdkTimer cacheSdkTimer = null)
    {
        var dataFetcher = new TestDataFetcher();
        _fetcherMock = new Mock<IDataFetcher>();

        _enMatchStatusUri = new Uri($"{TestData.RestXmlPath}/match_status_descriptions_en.xml", UriKind.Absolute);
        _fetcherMock.Setup(args => args.GetDataAsync(_enMatchStatusUri)).Returns(dataFetcher.GetDataAsync(_enMatchStatusUri));

        _deMatchStatusUri = new Uri($"{TestData.RestXmlPath}/match_status_descriptions_de.xml", UriKind.Absolute);
        _fetcherMock.Setup(args => args.GetDataAsync(_deMatchStatusUri)).Returns(dataFetcher.GetDataAsync(_deMatchStatusUri));

        _huMatchStatusUri = new Uri($"{TestData.RestXmlPath}/match_status_descriptions_hu.xml", UriKind.Absolute);
        _fetcherMock.Setup(args => args.GetDataAsync(_huMatchStatusUri)).Returns(dataFetcher.GetDataAsync(_huMatchStatusUri));

        _nlMatchStatusUri = new Uri($"{TestData.RestXmlPath}/match_status_descriptions_nl.xml", UriKind.Absolute);
        _fetcherMock.Setup(args => args.GetDataAsync(_nlMatchStatusUri)).Returns(dataFetcher.GetDataAsync(_nlMatchStatusUri));

        var uriFormat = $"{TestData.RestXmlPath}/match_status_descriptions_{{0}}.xml";
        var nameCacheSdkTimer = cacheSdkTimer ?? SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        return new LocalizedNamedValueCache("MatchStatus", exceptionStrategy, new NamedValueDataProvider(UofSdkBootstrap.DataProviderForNamedValueCacheMatchStatus, uriFormat, _fetcherMock.Object, "match_status"), nameCacheSdkTimer, new[] { new CultureInfo("en"), new CultureInfo("de"), new CultureInfo("hu") });
    }

    [Fact]
    public void ConstructingWithNullCacheNameThrows()
    {
        var sdkTimer = SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        var dataProvider = new NamedValueDataProvider(UofSdkBootstrap.DataProviderForNamedValueCacheMatchStatus, "any", new TestDataFetcher(), "match_status");
        Assert.Throws<ArgumentNullException>(() => new LocalizedNamedValueCache(null, ExceptionHandlingStrategy.Catch, dataProvider, sdkTimer, new List<CultureInfo> { new CultureInfo("en") }));
    }

    [Fact]
    public void ConstructingWithEmptyCacheNameThrows()
    {
        var sdkTimer = SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        var dataProvider = new NamedValueDataProvider(UofSdkBootstrap.DataProviderForNamedValueCacheMatchStatus, "any", new TestDataFetcher(), "match_status");
        Assert.Throws<ArgumentNullException>(() => new LocalizedNamedValueCache(string.Empty, ExceptionHandlingStrategy.Catch, dataProvider, sdkTimer, new List<CultureInfo> { new CultureInfo("en") }));
    }

    [Fact]
    public void ConstructingWithNullDataProviderThrows()
    {
        var sdkTimer = SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        Assert.Throws<ArgumentNullException>(() => new LocalizedNamedValueCache("AnyCacheName", ExceptionHandlingStrategy.Catch, null, sdkTimer, new List<CultureInfo> { new CultureInfo("en") }));
    }

    [Fact]
    public void ConstructingWithNullSdkTimerThrows()
    {
        var dataProvider = new NamedValueDataProvider(UofSdkBootstrap.DataProviderForNamedValueCacheMatchStatus, "any", new TestDataFetcher(), "match_status");
        Assert.Throws<ArgumentNullException>(() => new LocalizedNamedValueCache("AnyCacheName", ExceptionHandlingStrategy.Catch, dataProvider, null, new List<CultureInfo> { new CultureInfo("en") }));
    }

    [Fact]
    public void ConstructingWithEmptyCulturesThrows()
    {
        var sdkTimer = SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        var dataProvider = new NamedValueDataProvider(UofSdkBootstrap.DataProviderForNamedValueCacheMatchStatus, "any", new TestDataFetcher(), "match_status");
        Assert.Throws<ArgumentNullException>(() => new LocalizedNamedValueCache("AnyCacheName", ExceptionHandlingStrategy.Catch, dataProvider, sdkTimer, new List<CultureInfo>()));
    }

    [Fact]
    public void ConstructingWithNullCulturesThrows()
    {
        var sdkTimer = SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        var dataProvider = new NamedValueDataProvider(UofSdkBootstrap.DataProviderForNamedValueCacheMatchStatus, "any", new TestDataFetcher(), "match_status");
        Assert.Throws<ArgumentNullException>(() => new LocalizedNamedValueCache("AnyCacheName", ExceptionHandlingStrategy.Catch, dataProvider, sdkTimer, null));
    }

    [Fact]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    [SuppressMessage("Major Code Smell", "S1854:Unused assignments should be removed", Justification = "Allowed in this test")]
    public async Task DataIsFetchedOnlyOncePerLocale()
    {
        var cache = Setup(ExceptionHandlingStrategy.Throw);
        var namedValue = await cache.GetAsync(0);
        namedValue = await cache.GetAsync(0, new[] { new CultureInfo("en") });
        namedValue = await cache.GetAsync(0, new[] { new CultureInfo("de") });
        namedValue = await cache.GetAsync(0, new[] { new CultureInfo("hu") });

        Assert.NotNull(namedValue);

        _fetcherMock.Verify(x => x.GetDataAsync(_enMatchStatusUri), Times.Once);
        _fetcherMock.Verify(x => x.GetDataAsync(_deMatchStatusUri), Times.Once);
        _fetcherMock.Verify(x => x.GetDataAsync(_huMatchStatusUri), Times.Once);
        _fetcherMock.Verify(x => x.GetDataAsync(_nlMatchStatusUri), Times.Never);

        namedValue = await cache.GetAsync(0, new[] { new CultureInfo("nl") });
        namedValue = await cache.GetAsync(0, TestData.Cultures4);

        Assert.NotNull(namedValue);

        _fetcherMock.Verify(x => x.GetDataAsync(_enMatchStatusUri), Times.Once);
        _fetcherMock.Verify(x => x.GetDataAsync(_deMatchStatusUri), Times.Once);
        _fetcherMock.Verify(x => x.GetDataAsync(_huMatchStatusUri), Times.Once);
        _fetcherMock.Verify(x => x.GetDataAsync(_nlMatchStatusUri), Times.Once);
    }

    [Fact]
    public void InitialDataFetchDoesNotBlockConstructor()
    {
        Setup(ExceptionHandlingStrategy.Catch, SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromSeconds(10), TimeSpan.Zero));
        _fetcherMock.Verify(x => x.GetDataAsync(_enMatchStatusUri), Times.Never);
        _fetcherMock.Verify(x => x.GetDataAsync(_deMatchStatusUri), Times.Never);
        _fetcherMock.Verify(x => x.GetDataAsync(_huMatchStatusUri), Times.Never);
        _fetcherMock.Verify(x => x.GetDataAsync(_nlMatchStatusUri), Times.Never);
    }

    [Fact]
    public void InitialDataFetchStartedByConstructor()
    {
        Setup(ExceptionHandlingStrategy.Catch, SdkTimer.Create(UofSdkBootstrap.TimerForLocalizedNamedValueCache, TimeSpan.FromMilliseconds(10), TimeSpan.Zero));

        var finished = ExecutionHelper.WaitToComplete(() =>
        {
            _fetcherMock.Verify(x => x.GetDataAsync(_enMatchStatusUri), Times.Once);
            _fetcherMock.Verify(x => x.GetDataAsync(_deMatchStatusUri), Times.Once);
            _fetcherMock.Verify(x => x.GetDataAsync(_huMatchStatusUri), Times.Once);
            _fetcherMock.Verify(x => x.GetDataAsync(_nlMatchStatusUri), Times.Never);
        }, 15000);
        Assert.True(finished);
    }

    [Fact]
    public async Task CorrectValuesAreLoaded()
    {
        var cache = Setup(ExceptionHandlingStrategy.Throw);
        var doc = XDocument.Load($"{TestData.RestXmlPath}/match_status_descriptions_en.xml");
        Assert.NotNull(doc);
        Assert.NotNull(doc.Element("match_status_descriptions"));

        foreach (var xElement in doc.Element("match_status_descriptions")!.Elements("match_status"))
        {
            Assert.NotNull(xElement.Attribute("id"));
            var id = int.Parse(xElement.Attribute("id")!.Value);
            var namedValue = await cache.GetAsync(id);

            Assert.NotNull(namedValue);
            Assert.Equal(id, namedValue.Id);

            Assert.True(namedValue.Descriptions.ContainsKey(new CultureInfo("en")));
            Assert.True(namedValue.Descriptions.ContainsKey(new CultureInfo("de")));
            Assert.True(namedValue.Descriptions.ContainsKey(new CultureInfo("hu")));
            Assert.False(namedValue.Descriptions.ContainsKey(new CultureInfo("nl")));

            Assert.NotEqual(namedValue.GetDescription(new CultureInfo("en")), new CultureInfo("de").Name);
            Assert.NotEqual(namedValue.GetDescription(new CultureInfo("en")), new CultureInfo("hu").Name);
            Assert.NotEqual(namedValue.GetDescription(new CultureInfo("de")), new CultureInfo("hu").Name);
        }
    }

    [Fact]
    public async Task ThrowingExceptionStrategyIsRespected()
    {
        var cache = Setup(ExceptionHandlingStrategy.Throw);
        Func<Task> action = () => cache.GetAsync(1000);
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CatchingExceptionStrategyIsRespected()
    {
        var cache = Setup(ExceptionHandlingStrategy.Catch);
        var value = await cache.GetAsync(1000);

        Assert.Equal(1000, value.Id);
        Assert.NotNull(value.Descriptions);
        Assert.False(value.Descriptions.Any());
    }
}
