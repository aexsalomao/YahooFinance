namespace Quotes.YahooFinance

open System
open FSharp.Data
open FSharp.Data.JsonExtensions

type Interval = 
    | Daily
    | Weekly
    | Monthly
    | Quarterly
    | SemiAnnual
    | Annual
    | TwoYear
    | FiveYear
    | TenYear
    | YearToDate
    | Max
    override this.ToString() = 
        match this with
        | Daily -> "1d"
        | Weekly -> "5d"
        | Monthly -> "1mo"
        | Quarterly -> "3mo"
        | SemiAnnual -> "6mo"
        | Annual -> "1y"
        | TwoYear -> "2y"
        | FiveYear -> "5y"
        | TenYear -> "10y"
        | YearToDate -> "ytd"
        | Max -> "max"

type Quote = 
    {   
        Symbol        : string
        Date          : System.DateTime
        Open          : decimal
        High          : decimal
        Low           : decimal
        Close         : decimal
        AdjustedClose : decimal
        Volume        : decimal
    }

type Dividend = 
    {   
        Symbol : string
        Date   : System.DateTime
        Amount : decimal
    }

type Meta = 
    {
        Currency             : string
        Symbol               : string
        ExchangeName         : string
        InstrumentType       : string
        FirstTradeDate       : System.DateTime
        RegularMarketTime    : System.DateTime
        GmtOffset            : int
        Timezone             : string
        ExchangeTimezoneName : string
        RegularMarketPrice   : float
        ChartPreviousClose   : float
        DataGranularity      : string
        ValidRanges          : list<string>

    }

type ChartSeries = 
    { 
        Meta      : Meta
        History   : Quote list
        Dividends : Dividend list option
    }

type QuoteQuery = 
    { 
        Symbol    : string
        StartDate : System.DateTime
        EndDate   : System.DateTime
        Interval  : Interval
    }

module private ParsingUtils =
    let generateChartQueryUrl (quoteQuery : QuoteQuery) = 
        let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds().ToString()
        $"https://query1.finance.yahoo.com/v8/finance/chart/{quoteQuery.Symbol}?&" +
        $"period1={datetimeToUnix quoteQuery.StartDate}&period2={datetimeToUnix quoteQuery.EndDate}&" +
        $"interval={quoteQuery.Interval.ToString()}&" + 
        "includePrePost=true&events=div%7CSplit"

module private DownloadUtils = 
    let cache = Runtime.Caching.createInMemoryCache (TimeSpan(hours=12,minutes=0,seconds=0))
    
    let tryParseChartResult (chartRoot : JsonValue) = 
        chartRoot.TryGetProperty("chart")
        |> Option.bind (fun xs -> xs.TryGetProperty("result"))
        |> Option.bind (fun xs -> xs.AsArray() |> Array.tryExactlyOne)
    
    let tryParseChartMeta (chartResult : JsonValue) = 
        match chartResult.TryGetProperty("meta") with
        | Some chartMeta -> 
            match 
                chartMeta.TryGetProperty("currency"),
                chartMeta.TryGetProperty("symbol"),
                chartMeta.TryGetProperty("exchangeName"),
                chartMeta.TryGetProperty("instrumentType"),
                chartMeta.TryGetProperty("firstTradeDate"),
                chartMeta.TryGetProperty("regularMarketTime"),
                chartMeta.TryGetProperty("gmtoffset"),
                chartMeta.TryGetProperty("timezone"),
                chartMeta.TryGetProperty("exchangeTimezoneName"),
                chartMeta.TryGetProperty("regularMarketPrice"),
                chartMeta.TryGetProperty("chartPreviousClose"),
                chartMeta.TryGetProperty("dataGranularity"),
                chartMeta.TryGetProperty("validRanges") 
            with
            | Some currencyJson, 
              Some symbolJson, 
              Some exchangeNameJson,
              Some instrumentTypeJson,
              Some firstTradeDateJson,
              Some regularMarketTimeJson,
              Some gmtoffsetJson,
              Some timezoneJson,
              Some exchangeTimezoneNameJson,
              Some regularMarketPriceJson,
              Some chartPreviousCloseJson,
              Some dataGranularityJson,
              Some validRangesJson ->
                 { Currency = currencyJson.AsString()
                   Symbol = symbolJson.AsString()
                   ExchangeName = exchangeNameJson.AsString()
                   InstrumentType = instrumentTypeJson.AsString()
                   FirstTradeDate = DateTimeOffset.FromUnixTimeSeconds(firstTradeDateJson.AsInteger64()).DateTime
                   RegularMarketTime = DateTimeOffset.FromUnixTimeSeconds(regularMarketTimeJson.AsInteger64()).DateTime
                   GmtOffset = gmtoffsetJson.AsInteger()
                   Timezone = timezoneJson.AsString()
                   ExchangeTimezoneName = exchangeTimezoneNameJson.AsString()
                   RegularMarketPrice = regularMarketPriceJson.AsFloat()
                   ChartPreviousClose = chartPreviousCloseJson.AsFloat()
                   DataGranularity = dataGranularityJson.AsString()
                   ValidRanges = validRangesJson.AsArray() |> Array.map (fun xs -> xs.AsString()) |> Array.toList}
                |> Some
            | _ -> None
        | _ -> None
    
    let tryParseChartTimestamp (chartResult : JsonValue) = 
        match chartResult.TryGetProperty("timestamp") with
        | Some timestamp -> timestamp.AsArray() |> Some
        | None -> None

    let tryParseChartQuote (chartIndicators : JsonValue) = 
        chartIndicators.TryGetProperty("quote")
        |> Option.bind (fun xs -> xs.AsArray() |> Array.tryExactlyOne)
        |> Option.bind (fun quoteJson -> 
            match 
                  quoteJson.TryGetProperty("open"), 
                  quoteJson.TryGetProperty("high"),
                  quoteJson.TryGetProperty("low"),
                  quoteJson.TryGetProperty("close"),
                  quoteJson.TryGetProperty("volume") 
            with
            | Some quoteOpenJson, 
              Some quoteHighJson, 
              Some quoteLowJson, 
              Some quoteCloseJson, 
              Some quoteVolumeJson -> Some quoteJson
            | _ -> None)
    
    let tryParseChartAdjClose (chartIndicators : JsonValue) = 
        chartIndicators.TryGetProperty("adjclose")
        |> Option.bind (fun xs -> xs.AsArray() |> Array.tryExactlyOne)
        |> Option.bind (fun xs -> xs.TryGetProperty("adjclose"))
        |> Option.map (fun xs -> xs.AsArray())

    let tryParseDividends (chartResult : JsonValue) = 
        chartResult.TryGetProperty("events")
        |> Option.bind (fun eventsJson -> eventsJson.TryGetProperty("dividends"))
        |> Option.bind (fun dividendsJson -> 
            dividendsJson.Properties 
            |> Array.choose (fun (_, innerDividendsJson) -> 
                match 
                    innerDividendsJson.TryGetProperty("amount"), 
                    innerDividendsJson.TryGetProperty("date") 
                with
                | Some amountJson, 
                  Some dateJson ->
                    { Amount = amountJson.AsFloat() |> decimal
                      Date = DateTimeOffset.FromUnixTimeSeconds(dateJson.AsInteger64()).DateTime} |> Some
                | _ -> None)
                |> fun xs -> 
                    if xs.Length > 0 
                    then (xs |> Array.toList |> Some) 
                    else None)
    
    let generateChartSeries (chartResult : JsonValue) (chartIndicators : JsonValue) = 
        match
            tryParseChartMeta chartResult,
            tryParseChartTimestamp chartResult, 
            tryParseChartQuote chartIndicators, 
            tryParseChartAdjClose chartIndicators 
        with
        | Some meta,
          Some timestampJson, 
          Some quoteJson, 
          Some adjCloseJson ->
            let openJson = quoteJson.GetProperty("open").AsArray()
            let highJson = quoteJson.GetProperty("high").AsArray()
            let lowJson = quoteJson.GetProperty("low").AsArray()
            let closeJson = quoteJson.GetProperty("close").AsArray()
            let volumeJson = quoteJson.GetProperty("volume").AsArray()
            
            let numDistinctLengths = 
                [timestampJson;
                 adjCloseJson;
                 openJson;
                 highJson;
                 lowJson;
                 closeJson;
                 volumeJson]
                |> List.map Seq.length
                |> List.distinct
                |> List.length
                                
            if numDistinctLengths = 1 then
                let quotes = 
                    timestampJson
                    |> Array.Parallel.mapi (fun i ts -> 
                        { Symbol = meta.Symbol
                          Date = DateTimeOffset.FromUnixTimeSeconds(ts.AsInteger64()).DateTime
                          Open = openJson.[i].AsDecimal()
                          High = highJson.[i].AsDecimal()
                          Low = lowJson.[i].AsDecimal()
                          Close = closeJson.[i].AsDecimal()
                          AdjustedClose = adjCloseJson.[i].AsDecimal()
                          Volume = volumeJson.[i].AsDecimal()})
                    |> Array.toList
                
                let dividends = tryParseDividends chartResult

                { Meta = meta
                  History = quotes
                  Dividends = dividends} |> Some
            else None
        | _ -> None

    let tryParseChartJson (chartJson : JsonValue) =         
        let chartResult = tryParseChartResult chartJson
                
        let chartIndicators = 
            chartResult
            |> Option.bind (fun xs -> xs.TryGetProperty("indicators"))
        
        match chartResult, chartIndicators with
        | Some result, Some indicators -> generateChartSeries result indicators
        | _ -> None

    let rec asyncLoadChart attempt query = 
        async {
                let queryUrl = ParsingUtils.generateChartQueryUrl query
            try
                match cache.TryRetrieve(query.ToString()) with
                | Some quotes -> return quotes
                | None -> 
                    let! chart = Http.AsyncRequestString(queryUrl)
                    return JsonValue.TryParse(chart) |> Option.bind tryParseChartJson
            with
            | e -> 
                if attempt > 0 then
                    return! asyncLoadChart (attempt - 1) query
                else return None
            }

    let rec getSymbols (queries : list<QuoteQuery>) output =
        let retryCount = 5
        let parallelSymbols = 5
        
        let download thisDownload =
            [| for query in thisDownload do 
                asyncLoadChart retryCount query|]
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.toList

        if queries.Length > parallelSymbols then
            let thisDownload, remaining = queries |> List.splitAt parallelSymbols
            let result = download thisDownload
            System.Threading.Thread.Sleep(1000) // Throttle 1 sec per batch of symbols
            getSymbols remaining (result @ output)
        else
            let result = download queries
            result @ output

    let displayLogs (symbolSuccess: list<string * ChartSeries option>, symbolFail: list<string * ChartSeries option>)= 
        Console.WriteLine("\n ====== Logs ======")
        if not symbolSuccess.IsEmpty then
            System.Threading.Thread.Sleep(2000)
            Console.WriteLine($"\n - Successfully retrieved data for the following ({symbolSuccess.Length}) symbol(s) - \n")
            symbolSuccess
            |> List.iteri (fun i (symbol, chartSeries) -> Console.WriteLine($"{i + 1}. {symbol}"))
            System.Threading.Thread.Sleep(2000)
            Console.WriteLine("\n")
        
        if not symbolFail.IsEmpty then
            System.Threading.Thread.Sleep(2000)
            Console.WriteLine($"\n - Unable to retrieve data for the following ({symbolFail.Length}) symbol(s) - \n")
            symbolFail
            |> List.iteri (fun i (symbol, chartSeries) -> Console.WriteLine($"{i + 1}. {symbol}"))
            System.Threading.Thread.Sleep(2000)
            Console.WriteLine("\n")
        Console.WriteLine("====== Logs ====== \n")

type Series =  
    static member private DownloadChartSeries(symbols: seq<string>, startDate: DateTime, endDate: DateTime, interval: Interval) =
        let queries = 
            symbols
            |> Seq.toList
            |> List.map (fun symbol -> 
                {
                    Symbol = symbol
                    StartDate = startDate
                    EndDate = endDate
                    Interval = interval
                })
        
        DownloadUtils.getSymbols queries []
    
    static member private ExtractChartSeries(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval, ?displayLogs: bool)= 
        let startDate = defaultArg startDate (DateTime.Today.AddYears(-1))
        let endDate = defaultArg endDate (DateTime.Today)
        let interval = defaultArg interval Interval.Daily
        let displayLogs = defaultArg displayLogs false
        
        let chartSeries = 
            Series.DownloadChartSeries(Seq.distinct symbols, startDate=startDate, endDate=endDate, interval=interval)

        let successfullDownloads, failedDownloads = 
            List.zip (Seq.toList symbols) chartSeries
            |> List.partition (fun (symbol, chartSeries) -> chartSeries.IsSome)

        if displayLogs then DownloadUtils.displayLogs(successfullDownloads, failedDownloads)
        
        successfullDownloads
        |> List.map snd
        |> List.choose id
    
    static member History(symbols: string, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval, ?displayLogs: bool) = 
        YahooFinance.ExtractChartSeries([symbols], ?startDate=startDate, ?endDate=endDate, ?interval=interval, ?displayLogs=displayLogs)
        |> List.collect (fun xs -> xs.History)

    /// <summary>Downloads historical ticker data from YahooFinance.</summary>
    /// <param name="symbols">A collection of ticker symbols.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="interval">Data granularity.</param>
    /// <param name="displayLogs">Option to display logs.</param>
    /// <returns>Historical ticker data.</returns>
    /// <example>
    /// By default, if no optional parameters are specified, 
    /// <c>YahooFinance.History()</c>
    /// will download the most recent year of **daily** ticker history for all of the provided <c>symbols</c>.
    /// <code lang="fsharp">
    /// 
    /// YahooFinance.History(symbols=["MSFT"; "IBM"])
    /// </code>
    /// Alternatively, the user may also opt to specify a given <c>startDate</c>, <c>endDate</c>, and/or <c>interval</c>.
    /// <code lang="fsharp">
    /// 
    /// YahooFinance.History(symbols=["MSFT"; "IBM"], 
    ///                      startDate=DateTime(2015,1,1), 
    ///                      endDate=DateTime(2020,12,31), 
    ///                      interval=Interval.Weekly,
    ///                      displayLogs=true)
    /// </code>
    /// </example>
    static member History(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval, ?displayLogs: bool) = 
        YahooFinance.ExtractChartSeries(symbols, ?startDate=startDate, ?endDate=endDate, ?interval=interval, ?displayLogs=displayLogs)
        |> List.collect (fun xs -> xs.History)
    
    static member Meta(symbols: list<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval, ?displayLogs: bool)= 
        YahooFinance.ExtractChartSeries(symbols, ?startDate=startDate, ?endDate=endDate, ?interval=interval, ?displayLogs=displayLogs)
        |> List.map (fun xs -> xs.Meta)