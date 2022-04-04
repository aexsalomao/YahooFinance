namespace EmbeddedResources

 module Samples = 
    
    module ChartSample =
    
        [<Literal>]
        let ChartSampleWithDividends = """
            {
                "chart":
                    {
                        "result":
                            [
                                {
                                    "meta":
                                        {
                                            "currency":"USD",
                                            "symbol":"AAPL",
                                            "exchangeName":"NMS",
                                            "instrumentType":"EQUITY",
                                            "firstTradeDate":345479400,
                                            "regularMarketTime":1647023958,
                                            "gmtoffset":-18000,
                                            "timezone":"EST",
                                            "exchangeTimezoneName":"America/New_York",
                                            "regularMarketPrice":156.4,
                                            "chartPreviousClose":163.17,
                                            "dataGranularity":"1d",
                                            "validRanges":["1d","5d","1mo","3mo","6mo","1y","2y","5y","10y","ytd","max"]
                                        },
                                    "timestamp":[1646663400,1646749800,1646836200,1646922600,1647023958],
                                    "events":
                                            {
                                                "dividends":
                                                    {
                                                        "1629293400":
                                                            {
                                                                "amount":0.56,
                                                                "date":1629293400
                                                            },
                                                        "1637159400":
                                                            {
                                                                "amount":0.62,
                                                                "date":1637159400
                                                            },
                                                        "1645021800":
                                                            {
                                                                "amount":0.62,
                                                                "date":1645021800
                                                            }
                                                    }
                                            },
                                    "indicators":
                                        {
                                            "quote":
                                                [
                                                    {
                                                        "close":[159.3000030517578,157.44000244140625,162.9499969482422,158.52000427246094,156.39999389648438],
                                                        "volume":[96418800,131148300,91454900,105180300,58326959],
                                                        "high":[165.02000427246094,162.8800048828125,163.41000366210938,160.38999938964844,159.2799072265625],
                                                        "open":[163.36000061035156,158.82000732421875,161.47999572753906,160.1999969482422,158.92999267578125],
                                                        "low":[159.0399932861328,155.8000030517578,159.41000366210938,155.97999572753906,154.74749755859375]
                                                    }
                                                ],
                                            "adjclose":
                                                [
                                                    {
                                                        "adjclose":[159.3000030517578,157.44000244140625,162.9499969482422,158.52000427246094,156.39999389648438]
                                                    }
                                                ]
                                        }
                                }
                            ],
                        "error":null
                    }
            }""" 

    module OptionChainSample = 

        [<Literal>]
        let OptionChainQuote = """
            {
            "language"                           : "en-US",
            "region"                             : "US",
            "quoteType"                          : "EQUITY",
            "typeDisp"                           : "Equity",
            "quoteSourceName"                    : "Delayed Quote",
            "triggerable"                        : true,
            "customPriceAlertConfidence"         : "HIGH",
            "currency"                           : "USD",
            "tradeable"                          : false,
            "exchange"                           : "NMS",
            "shortName"                          : "Apple Inc.",
            "longName"                           : "Apple Inc.",
            "messageBoardId"                     : "finmb_24937",
            "exchangeTimezoneName"               : "America/New_York",
            "exchangeTimezoneShortName"          : "EDT",
            "gmtOffSetMilliseconds"              : -14400000,
            "marketState"                        : "CLOSED",
            "market"                             : "us_market",
            "esgPopulated"                       : false,
            "earningsTimestamp"                  : 1643301000,
            "earningsTimestampStart"             : 1651003200,
            "earningsTimestampEnd"               : 1651521600,
            "trailingAnnualDividendRate"         : 0.865,
            "trailingPE"                         : 29.047382,
            "trailingAnnualDividendYield"        : 0.0049692653,
            "epsTrailingTwelveMonths"            : 6.015,
            "epsForward"                         : 6.56,
            "epsCurrentYear"                     : 6.16,
            "priceEpsCurrentYear"                : 28.363638,
            "sharesOutstanding"                  : 16319399936,
            "bookValue"                          : 4.402,
            "fiftyDayAverage"                    : 166.312,
            "fiftyDayAverageChange"              : 8.408005,
            "fiftyDayAverageChangePercent"       : 0.050555613,
            "twoHundredDayAverage"               : 155.5514,
            "twoHundredDayAverageChange"         : 19.168594,
            "twoHundredDayAverageChangePercent"  : 0.123229966,
            "marketCap"                          : 2851325607936,
            "forwardPE"                          : 26.634148,
            "priceToBook"                        : 39.69105,
            "sourceInterval"                     : 15,
            "exchangeDataDelayedBy"              : 0,
            "pageViewGrowthWeekly"               : 0.065387554,
            "averageAnalystRating"               : "1.8 - Buy",
            "firstTradeDateMilliseconds"         : 345479400000,
            "priceHint"                          : 2,
            "postMarketChangePercent"            : 0.13163675,
            "postMarketTime"                     : 1648252793,
            "postMarketPrice"                    : 174.95,
            "postMarketChange"                   : 0.22999573,
            "regularMarketChange"                : 0.6499939,
            "regularMarketChangePercent"         : 0.37340948,
            "regularMarketTime"                  : 1648238404,
            "regularMarketPrice"                 : 174.72,
            "regularMarketDayHigh"               : 175.28,
            "regularMarketDayRange"              : "172.75 - 175.28",
            "regularMarketDayLow"                : 172.75,
            "regularMarketVolume"                : 80546156,
            "regularMarketPreviousClose"         : 174.07,
            "bid"                                : 174.87,
            "ask"                                : 174.95,
            "bidSize"                            : 9,
            "askSize"                            : 11,
            "fullExchangeName"                   : "NasdaqGS",
            "financialCurrency"                  : "USD",
            "regularMarketOpen"                  : 173.88,
            "averageDailyVolume3Month"           : 93465964,
            "averageDailyVolume10Day"            : 94897140,
            "fiftyTwoWeekLowChange"              : 55.86,
            "fiftyTwoWeekLowChangePercent"       : 0.46996465,
            "fiftyTwoWeekRange"                  : "118.86 - 182.94",
            "fiftyTwoWeekHighChange"             : -8.220001,
            "fiftyTwoWeekHighChangePercent"      : -0.04493277,
            "fiftyTwoWeekLow"                    : 118.86,
            "fiftyTwoWeekHigh"                   : 182.94,
            "dividendDate"                       : 1644451200,
            "displayName"                        : "Apple",
            "symbol"                             : "AAPL"
            }
            """

        [<Literal>]
        let OptionChainCallOrPut = 
            """
            [{
            "contractSymbol"    : "AAPL220401C00105000",
            "strike"            : 105.0,
            "currency"          : "USD",
            "lastPrice"         : 65.14,
            "change"            : 0.0,
            "percentChange"     : 0.0,
            "volume"            : 1,
            "openInterest"      : 28,
            "bid"               : 69.3,
            "ask"               : 70.2,
            "contractSize"      : "REGULAR",
            "expiration"        : 1648771200,
            "lastTradeDate"     : 1648045323,
            "impliedVolatility" : 1.484377578125,
            "inTheMoney"        : true
            }]
            """

        [<Literal>]
        let OptionChain = 
            """  
            {
            "optionChain" :
                {
                "result" : [{
                           "underlyingSymbol"   : "AAPL",
                           "expirationDates"    : [1648771200,1649376000,1649894400,1650585600,1651190400,1651795200,1653004800,1655424000,1657843200,1660867200,1663286400,1666310400,1668729600,1674172800,1679011200,1686873600,1694736000,1705622400,1718928000],
                           "strikes"            : [105.0,110.0,115.0,120.0,125.0,130.0,135.0,140.0,141.0,142.0,143.0,144.0,145.0,146.0,147.0,148.0,149.0,150.0,152.5,155.0,157.5,160.0,162.5,165.0,167.5,170.0,172.5,175.0,177.5,180.0,182.5,185.0,187.5,190.0,192.5,195.0,197.5,200.0,205.0,210.0,215.0,220.0,225.0,230.0,235.0,240.0,245.0,250.0],
                           "hasMiniOptions"     : false,
                           "quote"              : """ + OptionChainQuote + """,
                           "options"            : [{
                                                  "expirationDate" : 1648771200,
                                                  "hasMiniOptions" : false,
                                                  "calls"          : """ + OptionChainCallOrPut + """,
                                                  "puts"           : """ + OptionChainCallOrPut + """
                                                  }]
                            }],
                "error"  : null
                }
            }
            """