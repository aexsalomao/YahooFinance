namespace EmbeddedResources

module Samples = 

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
                                        "priceHint":2,
                                        "currentTradingPeriod":{"pre":{"timezone":"EST","end":1647009000,"start":1646989200,"gmtoffset":-18000},"regular":{"timezone":"EST","end":1647032400,"start":1647009000,"gmtoffset":-18000},"post":{"timezone":"EST","end":1647046800,"start":1647032400,"gmtoffset":-18000}},
                                        "dataGranularity":"1d",
                                        "range":"",
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

    [<Literal>]
    let ChartSampleWithoutDividends = """
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
                                        "priceHint":2,
                                        "currentTradingPeriod":{"pre":{"timezone":"EST","end":1647009000,"start":1646989200,"gmtoffset":-18000},"regular":{"timezone":"EST","end":1647032400,"start":1647009000,"gmtoffset":-18000},"post":{"timezone":"EST","end":1647046800,"start":1647032400,"gmtoffset":-18000}},
                                        "dataGranularity":"1d",
                                        "range":"",
                                        "validRanges":["1d","5d","1mo","3mo","6mo","1y","2y","5y","10y","ytd","max"]
                                    },
                                "timestamp":[1646663400,1646749800,1646836200,1646922600,1647023958],
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

