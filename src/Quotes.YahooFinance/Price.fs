namespace Quotes.YahooFinance

open System
open Quotes.YahooFinance.JsonApis.Providers
open FSharp.Data
open System.Text.RegularExpressions

type Interval = 
    | Daily
    | Weekly
    | Monthly
    | Quarterly
    | SemiAnnual
    | Annual
    | TwoYear
    | ThreeYear
    | FiveYear
    | TenYear
    override this.ToString() = 
        match this with
        | Daily -> "1d"
        | Weekly -> "1w"
        | Monthly -> "1mo"
        | Quarterly -> "3mo"
        | SemiAnnual -> "6mo"
        | Annual -> "1y"
        | TwoYear -> "2y"
        | ThreeYear -> "3y"
        | FiveYear -> "5y"
        | TenYear -> "10y"

type Symbol = string

type Quote = 
    {   
        Symbol        : Symbol
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
        Date   : System.DateTime
        Amount : decimal
    }

type Meta = 
    {
        Currency            : string
        Symbol              : Symbol
        ExchangeName        : string
        InstrumentType      : string
        FirstTradeDate      : int
        RegularMarketTime   : int
        GmtOffset           : int
        Timezone            : string
        ExchangeTimezonName : string
        RegularMarketPrice  : float
        ChartPreviousClose  : float
        DataGranularity     : string
        ValidRanges         : list<string>

    }

type Events = {Dividends : Dividend list option}

type ChartSeries = 
    { 
        Meta    : Meta
        History : Quote list
        Events  : Events
    }

type QuoteQuery = 
    { 
        Symbol    : Symbol
        StartDate : System.DateTime
        EndDate   : System.DateTime
        Interval  : Interval
    }

type ErrorLog = string

module private ParsingUtils =

    let generateChartQueryUrl (quoteQuery : QuoteQuery) = 
        let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds().ToString()
        
        $"https://query1.finance.yahoo.com/v8/finance/chart/{quoteQuery.Symbol}?&" +
        $"period1={datetimeToUnix quoteQuery.StartDate}&period2={datetimeToUnix quoteQuery.EndDate}&" +
        $"interval={quoteQuery.Interval.ToString()}&" + 
        "includePrePost=true&events=div%7CSplit"

    let parseJsonDividends dividends gmtOffset = 
        match Regex.Matches(dividends, "(?<=\"amount\":\s+)\d+[.]?\d+"), Regex.Matches(dividends, "((?<=\"date\":\s+)\d+)") with
        | amount_matches, date_matches when amount_matches.Count = date_matches.Count && amount_matches.Count > 0 
            -> amount_matches
                |> Seq.zip date_matches
                |> Seq.map (fun (date_match, amount_match) -> 
                    { Date = DateTimeOffset.FromUnixTimeSeconds(float date_match.Value - float gmtOffset |> int64).DateTime
                      Amount = amount_match.Value |> decimal})
                |> Seq.toList
                |> Some 
        | _ -> None

    let parseChartMeta (chartMeta : ChartProvider.Meta) = 
        {
            Currency            = chartMeta.Currency
            Symbol              = chartMeta.Symbol
            ExchangeName        = chartMeta.ExchangeName
            InstrumentType      = chartMeta.InstrumentType
            FirstTradeDate      = chartMeta.FirstTradeDate
            RegularMarketTime   = chartMeta.RegularMarketTime
            GmtOffset           = chartMeta.Gmtoffset
            Timezone            = chartMeta.Timezone
            ExchangeTimezonName = chartMeta.ExchangeTimezoneName
            RegularMarketPrice  = float chartMeta.RegularMarketPrice
            ChartPreviousClose  = float chartMeta.ChartPreviousClose
            DataGranularity     = chartMeta.DataGranularity
            ValidRanges         = Array.toList chartMeta.ValidRanges

        }

    let checkChartResult (chartResult : ChartProvider.Result) =
        match Array.tryExactlyOne chartResult.Indicators.Quote, Array.tryExactlyOne chartResult.Indicators.Adjclose with
        | Some quote, Some adjClose -> 
            if Set([quote.Close.Length; 
                    quote.High.Length; 
                    quote.Open.Length; 
                    quote.Low.Length; 
                    quote.Volume.Length; 
                    chartResult.Timestamp.Length; 
                    adjClose.Adjclose.Length]).Count = 1 then Ok (quote, adjClose)
            else
                Error "Privided Quotes data is not aligned (Provider has missing data)"
        | _ , None -> Error "Unable to download: Adjusted Close"
        | None, _ -> Error "Unable to download: Open/High/Low/Close/Volume"

    let populateSeries (chartResult : ChartProvider.Result) : Result<ChartSeries, ErrorLog> = 
        match checkChartResult chartResult with
        | Error e -> Error e
        | Ok (quote, adjClose) -> 
            let meta = parseChartMeta chartResult.Meta

            let dividends = 
                try
                    parseJsonDividends (chartResult.Events.Dividends.JsonValue.ToString()) chartResult.Meta.Gmtoffset
                with
                | _ -> None

            let quoteHistory = 
                chartResult.Timestamp
                |> Array.Parallel.mapi (fun i ts -> 
                    { 
                        Symbol = chartResult.Meta.Symbol
                        Date = DateTimeOffset.FromUnixTimeSeconds(int64 ts).DateTime
                        Open = quote.Open.[i]
                        High = quote.High.[i]
                        Low = quote.Low.[i]
                        Close = quote.Close.[i]
                        AdjustedClose = adjClose.Adjclose.[i]
                        Volume = decimal quote.Volume.[i]
                    })
                    |> Array.toList
                
            Ok { Meta = meta
                 History = quoteHistory 
                 Events = {Dividends = dividends}}

module private DownloadUtils = 
    let cache = Runtime.Caching.createInMemoryCache (TimeSpan(hours=12,minutes=0,seconds=0))

    let parseChart (query : QuoteQuery) (chartRoot : ChartProvider.Root) : Result<ChartSeries, ErrorLog> = 
        let chartError = chartRoot.Chart.Error.JsonValue.ToString()
        match Array.tryExactlyOne chartRoot.Chart.Result with
        | Some chartResult when chartError = "null" ->    
            chartResult 
            |> ParsingUtils.populateSeries
            |> fun series -> 
                cache.Set(query.ToString(), series)
                series
        | _ -> Error chartError

    let rec asyncLoadChart attempt query : Async<Result<ChartSeries, ErrorLog>> = 
        async {
                let queryUrl = ParsingUtils.generateChartQueryUrl query
            try
                match cache.TryRetrieve(query.ToString()) with
                | Some quotes -> return quotes
                | None -> 
                    let! chart = ChartProvider.AsyncLoad(queryUrl)
                    return parseChart query chart
            with
            | e -> 
                if attempt > 0 then
                    return! asyncLoadChart (attempt - 1) query
                else return (e.ToString() |> Error)
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
    
    let request symbol =
        { 
            Symbol = symbol
            StartDate = DateTime.Today.AddYears(-1)
            EndDate = DateTime.Today
            Interval = Daily
        }

    let foldResult (data, errorLog) symbolSeriesResult = 
        let symbol, result = symbolSeriesResult
        match result with
        | Ok series -> ((symbol, series) :: data, errorLog)
        | Error e -> (data, (symbol, e) :: errorLog)
    
    let partitionOnResult symbols response = 
        List.zip (Seq.toList symbols) response
        |> List.fold foldResult ([], [])

type Series =  
    static member private displayLogs (symbolSuccess: list<Symbol * ChartSeries>, symbolFail: list<Symbol * ErrorLog>)= 
        Console.WriteLine("\n ====== Logs ====== \n")
        if not symbolSuccess.IsEmpty then
            System.Threading.Thread.Sleep(2000)
            Console.WriteLine($"- Successfully retrieved data for the following ({symbolSuccess.Length}) symbol(s) - \n")
            symbolSuccess
            |> List.iteri (fun i (symbol, series) -> Console.WriteLine($"{i + 1}. {symbol}"))
            Console.WriteLine("\n ====== Logs ====== \n")
            System.Threading.Thread.Sleep(2000)
        
        if not symbolFail.IsEmpty then
            System.Threading.Thread.Sleep(2000)
            Console.WriteLine($"- Unable to retrieve data for the following ({symbolFail.Length}) symbol(s) - \n")
            symbolFail
            |> List.iteri (fun i (symbol, errorLog) -> Console.WriteLine($"{i + 1}. {symbol}"))
            Console.WriteLine("\n ====== Logs ====== \n")
            System.Threading.Thread.Sleep(2000)

    static member private Quotes(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval) =
        let startDate = defaultArg startDate (DateTime.Today.AddYears(-1))
        let endDate = defaultArg endDate (DateTime.Today)
        let interval = defaultArg interval Interval.Daily

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
    
    static member Meta(symbols: seq<string>, ?displayLogs: bool) = 
        let displayLogs = defaultArg displayLogs false

        let successfulResponse, failedResponse = 
            Series.Quotes(symbols=symbols)
            |> DownloadUtils.partitionOnResult symbols
        
        if displayLogs then Series.displayLogs(successfulResponse, failedResponse)
        
        successfulResponse
        |> List.map (fun (symbol, series) -> series.Meta)

    static member History(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval, ?displayLogs: bool) = 
        let displayLogs = defaultArg displayLogs false 

        let successfulResponse, failedResponse = 
            Series.Quotes(symbols=symbols, ?startDate=startDate, ?endDate=endDate, ?interval=interval)
            |> DownloadUtils.partitionOnResult symbols 
        
        if displayLogs then Series.displayLogs(successfulResponse, failedResponse)

        successfulResponse
        |> List.collect (fun (symbol, series) -> series.History)
    
    static member Events(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?displayLogs: bool) = 
        let displayLogs = defaultArg displayLogs false

        let successfulResponse, failedResponse = 
            Series.Quotes(symbols=symbols, ?startDate=startDate, ?endDate=endDate)
            |> DownloadUtils.partitionOnResult symbols 
        
        if displayLogs then Series.displayLogs(successfulResponse, failedResponse)

        successfulResponse
        |> List.map (fun (symbol, series) -> series.Events)