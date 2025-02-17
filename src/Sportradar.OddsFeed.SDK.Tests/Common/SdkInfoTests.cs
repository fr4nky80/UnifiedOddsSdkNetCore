﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Sportradar.OddsFeed.SDK.Common;
using Sportradar.OddsFeed.SDK.Common.Exceptions;
using Sportradar.OddsFeed.SDK.Common.Internal;
using Xunit;
// ReSharper disable ArrangeRedundantParentheses

namespace Sportradar.OddsFeed.SDK.Tests.Common;

public class SdkInfoTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "+1")]
    [InlineData(-1, "-1")]
    [InlineData(0.05, "+0.05")]
    [InlineData(-1.01, "-1.01")]
    [InlineData(1.25, "+1.25")]
    public void DecimalToStringWithSignReturnsCorrect(decimal inputValue, string expectedResult)
    {
        Assert.Equal(expectedResult, SdkInfo.DecimalToStringWithSign(inputValue));
    }

    [Fact]
    public void GetRandomReturnsInt()
    {
        var value = SdkInfo.GetRandom();
        Assert.True(value >= 0);
    }

    [Fact]
    public void GetRandomReturnsCorrectBetweenMinMax()
    {
        const int min = 100;
        const int max = 1000;
        for (var i = 0; i < 10000; i++)
        {
            var value = SdkInfo.GetRandom(min, max);
            Assert.True(value >= min, $"Value[{i}] must be equal or greater then min: {min} <= {value}");
            Assert.True(value < max, $"Value[{i}] must be less then max: {value} < {max}");
        }
    }

    [Fact]
    public void GetRandomReturnsCorrectBetweenMinMaxForNegativeBoundaries()
    {
        const int min = -1000;
        const int max = -100;
        for (var i = 0; i < 10000; i++)
        {
            var value = SdkInfo.GetRandom(min, max);
            Assert.True(value < 0);
            Assert.True(value >= min, $"Value[{i}] must be equal or greater then min: {min} <= {value}");
            Assert.True(value < max, $"Value[{i}] must be less then max: {value} < {max}");
        }
    }

    [Fact]
    public void GetRandomIsMinInclusive()
    {
        const int min = 100;
        const int max = 1000;
        var minGenerated = false;
        for (var i = 0; i < 10000; i++)
        {
            var value = SdkInfo.GetRandom(min, max);
            if (value == min)
            {
                minGenerated = true;
                break;
            }
        }
        Assert.True(minGenerated);
    }

    [Fact]
    public void GetRandomIsMaxExclusive()
    {
        const int min = 100;
        const int max = 1000;
        var maxGenerated = false;
        for (var i = 0; i < 10000; i++)
        {
            var value = SdkInfo.GetRandom(min, max);
            if (value == max)
            {
                maxGenerated = true;
                break;
            }
        }
        Assert.False(maxGenerated);
    }

    [Fact]
    public void GetRandomMinMaxMustBeDifferent()
    {
        const int min = 100;
        const int max = 100;
        Assert.Throws<ArgumentOutOfRangeException>(() => SdkInfo.GetRandom(min, max));
    }

    [Fact]
    public void GetRandomMinMustBeLessThenMax()
    {
        const int min = 100;
        const int max = 10;
        Assert.Throws<ArgumentOutOfRangeException>(() => SdkInfo.GetRandom(min, max));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Readability")]
    public void GetVariableNumberForIntReturnsVariableBetweenMinMax()
    {
        const int baseValue = 100;
        const int variablePercent = 10;
        const double min = ((100 - (double)variablePercent) / 100) * baseValue;
        const double max = ((100 + (double)variablePercent) / 100) * baseValue;
        for (var i = 0; i < 100; i++)
        {
            var value = SdkInfo.GetVariableNumber(baseValue, variablePercent);
            Assert.True(value >= min, $"{value} must be greater then {min}");
            Assert.True(value < max, $"{value} must be less then {max}");
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Readability")]
    public void GetVariableNumberForTimeSpanReturnsVariableBetweenMinMax()
    {
        var baseValue = TimeSpan.FromSeconds(100);
        const int variablePercent = 10;
        var min = ((100 - (double)variablePercent) / 100) * baseValue.TotalSeconds;
        var max = ((100 + (double)variablePercent) / 100) * baseValue.TotalSeconds;
        for (var i = 0; i < 100; i++)
        {
            var value = SdkInfo.GetVariableNumber(baseValue, variablePercent);
            Assert.True(value.TotalSeconds >= min, $"{value.TotalSeconds} must be greater then {min}");
            Assert.True(value.TotalSeconds < max, $"{value.TotalSeconds} must be less then {max}");
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Readability")]
    public void AddVariableNumberForTimeSpanReturnsVariableBetweenMinMax()
    {
        var baseValue = TimeSpan.FromSeconds(100);
        const int variablePercent = 10;
        var max = ((100 + (double)variablePercent) / 100) * baseValue.TotalSeconds;
        for (var i = 0; i < 100; i++)
        {
            var value = SdkInfo.AddVariableNumber(baseValue, variablePercent);
            Assert.True(value.TotalSeconds >= baseValue.TotalSeconds, $"{value.TotalSeconds} must be greater then {baseValue.TotalSeconds}");
            Assert.True(value.TotalSeconds < max, $"{value.TotalSeconds} must be less then {max}");
        }
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Readability")]
    public void AddVariableNumberForProfileCacheTimeoutReturnsVariableBetweenMinMax()
    {
        var baseValue = TestConfiguration.GetConfig(null).Cache.ProfileCacheTimeout;
        const int variablePercent = 10;
        var max = ((100 + (double)variablePercent) / 100) * baseValue.TotalSeconds;
        for (var i = 0; i < 100; i++)
        {
            var value = SdkInfo.AddVariableNumber(baseValue, variablePercent);
            Assert.True(value.TotalSeconds >= baseValue.TotalSeconds, $"{value.TotalSeconds} must be greater then {baseValue.TotalSeconds}");
            Assert.True(value.TotalSeconds < max, $"{value.TotalSeconds} must be less then {max}");
            Assert.True(value.TotalSeconds < baseValue.TotalSeconds * 1.1, $"{value.TotalSeconds} must be less then {baseValue.TotalSeconds * 1.1}");
        }
    }

    [Fact]
    public void CultureInfoTest()
    {
        const int iteration = 1000000;
        var cultureList = new List<CultureInfo>();
        for (var i = 0; i < iteration; i++)
        {
            cultureList.Add(new CultureInfo("en"));
        }

        for (var i = 0; i < 1000; i++)
        {
            var r1 = SdkInfo.GetRandom(iteration);
            var r2 = SdkInfo.GetRandom(iteration);
            var culture1 = cultureList[r1];
            var culture2 = cultureList[r2];
            Assert.NotNull(culture1);
            Assert.NotNull(culture2);
            Assert.Equal(culture1, culture2);
            Assert.StrictEqual(culture1, culture2);
        }
    }

    [Fact]
    public void CultureInfoAsStringTest()
    {
        const int iteration = 1000000;
        var cultureList = new List<string>();
        for (var i = 0; i < iteration; i++)
        {
            cultureList.Add("en");
        }

        for (var i = 0; i < 1000; i++)
        {
            var r1 = SdkInfo.GetRandom(iteration);
            var r2 = SdkInfo.GetRandom(iteration);
            var culture1 = cultureList[r1];
            var culture2 = cultureList[r2];
            Assert.NotNull(culture1);
            Assert.NotNull(culture2);
            Assert.Equal(culture1, culture2);
        }
    }

    [Fact]
    public void CultureInfoInDictionaryTest()
    {
        const int iteration = 10000;
        var dictList = new List<Dictionary<CultureInfo, string>>();
        var cultureList = new Dictionary<CultureInfo, string>();
        for (var i = 0; i < iteration; i++)
        {
            cultureList.Clear();
            cultureList.Add(new CultureInfo("en"), SdkInfo.GetGuid(20));
            dictList.Add(cultureList);
        }

        for (var i = 0; i < 1000; i++)
        {
            var r1 = SdkInfo.GetRandom(iteration);
            var r2 = SdkInfo.GetRandom(iteration);
            var culture1 = dictList[r1].Keys.First();
            var culture2 = dictList[r2].Keys.First();
            Assert.NotNull(culture1);
            Assert.NotNull(culture2);
            Assert.Equal(culture1, culture2);
            Assert.StrictEqual(culture1, culture2);
        }
    }

    [Fact]
    public void UtcNowStringReturnsCorrectResult()
    {
        Assert.Equal($"{DateTime.UtcNow:yyyyMMddHHmm}", SdkInfo.UtcNowString());
    }

    [Fact]
    public void GetObjectSize_ForNull()
    {
        var fake = (FakeTimeProvider)null;

        var size = SdkInfo.GetObjectSize(fake);

        Assert.Equal(0, size);
    }

    [Fact]
    public void GetObjectSize_ForNonSerializableObject()
    {
        var urn = Urn.Parse("sr:sport:1234");

        var size = SdkInfo.GetObjectSize(urn);

        Assert.False(urn.GetType().IsSerializable);
        Assert.Equal(0, size);
    }

    [Fact]
    public void GetObjectSize_ForSerializableObject()
    {
        var serializableObject = new MappingException();

        var size = SdkInfo.GetObjectSize(serializableObject);

        Assert.True(serializableObject.GetType().IsSerializable, "Object is not serializable");
        Assert.True(size > 0, "Size is 0");
    }

    [Fact]
    public void GetOrCreateReadOnlyNames_NamesValidCultureIncluded()
    {
        const string firstName = "Name 1";
        var inputNames = new Dictionary<CultureInfo, string> { { TestData.Culture, firstName } };
        var wantedCultures = new Collection<CultureInfo> { TestData.Culture };

        var result = SdkInfo.GetOrCreateReadOnlyNames(inputNames, wantedCultures);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(TestData.Culture, result.Keys.First());
        Assert.Equal(firstName, result.Values.First());
    }

    [Fact]
    public void GetOrCreateReadOnlyNames_NamesValidCultureNotIncluded()
    {
        const string firstName = "Name 1";
        var inputNames = new Dictionary<CultureInfo, string> { { TestData.Culture, firstName } };
        var wantedCultures = new Collection<CultureInfo> { TestData.CultureNl };

        var result = SdkInfo.GetOrCreateReadOnlyNames(inputNames, wantedCultures);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(TestData.CultureNl, result.Keys.First());
        Assert.Equal(string.Empty, result.Values.First());
    }

    [Fact]
    public void GetOrCreateReadOnlyNames_NullWantedCultures()
    {
        const string firstName = "Name 1";
        var inputNames = new Dictionary<CultureInfo, string> { { TestData.Culture, firstName } };

        var result = SdkInfo.GetOrCreateReadOnlyNames(inputNames, null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetOrCreateReadOnlyNames_EmptyWantedCultures()
    {
        const string firstName = "Name 1";
        var inputNames = new Dictionary<CultureInfo, string> { { TestData.Culture, firstName } };

        var result = SdkInfo.GetOrCreateReadOnlyNames(inputNames, new Collection<CultureInfo>());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetOrCreateReadOnlyNames_NamesEmpty()
    {
        var inputNames = new Dictionary<CultureInfo, string>();
        var wantedCultures = new Collection<CultureInfo> { TestData.Culture };

        var result = SdkInfo.GetOrCreateReadOnlyNames(inputNames, wantedCultures);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(TestData.Culture, result.Keys.First());
        Assert.Equal(string.Empty, result.Values.First());
    }

    [Fact]
    public void GetOrCreateReadOnlyNames_NamesContainsMoreThenWantedCultures()
    {
        const string firstName = "Name 1";
        var inputNames = new Dictionary<CultureInfo, string>
        {
            { TestData.Cultures.ElementAt(0), firstName }, { TestData.Cultures.ElementAt(1), firstName }, { TestData.Cultures.ElementAt(2), firstName }
        };
        var wantedCultures = new Collection<CultureInfo> { TestData.Cultures.ElementAt(1) };

        var result = SdkInfo.GetOrCreateReadOnlyNames(inputNames, wantedCultures);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(TestData.Cultures.ElementAt(1), result.Keys.First());
        Assert.Equal(firstName, result.Values.First());
    }
}
