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
        Date   : System.DateTime
        Amount : decimal
    }

type Meta = 
    {
        Currency            : string
        Symbol              : string
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

type History = Quote list

type ChartSeries = 
    { 
        Meta    : Meta
        History : Quote list
        Events  : Events
    }

type QuoteQuery = 
    { 
        Symbol    : string
        StartDate : System.DateTime
        EndDate   : System.DateTime
        Interval  : Interval
    }

type QueryResponse = 
    {
        Data     : ChartSeries List
        ErrorLog : string List
    }

type ErrorMsg = string

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
        match Array.tryExactlyOne chartResult.Indicators.Quote, 
        Array.tryExactlyOne chartResult.Indicators.Adjclose with
        | Some quote, Some adjClose -> 
            if Set([quote.Close.Length; 
                    quote.High.Length; 
                    quote.Open.Length; 
                    quote.Low.Length; 
                    quote.Volume.Length; 
                    chartResult.Timestamp.Length; 
                    adjClose.Adjclose.Length]).Count = 1 then Ok (quote, adjClose)
            else
                Error "Missing data error (Data is not aligned)"
        | _ -> Error "Chart.Result Error"

    let populateSeries (chartResult : ChartProvider.Result) = 
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

    let parseChart (query : QuoteQuery) (chartRoot : ChartProvider.Root) = 
        let chartError = chartRoot.Chart.Error.JsonValue.ToString()
        match Array.tryExactlyOne chartRoot.Chart.Result with
        | Some chartResult when chartError = "null" ->    
            chartResult 
            |> ParsingUtils.populateSeries
            |> fun series -> 
                cache.Set(query.ToString(), series)
                series
        | _ -> Error chartError

    let rec asyncLoadChart attempt query : Async<Result<ChartSeries, ErrorMsg>> = 
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
                else return $"Failed to request {query.ToString()}" |> Error
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
        
    let getSeries queries = 
        let getResult queries = getSymbols queries []

        let foldResult (data, errorLog) (queryResult : Result<ChartSeries, string>) = 
            match queryResult with
            | Ok series -> (series :: data, errorLog)
            | Error e -> (data, e :: errorLog)

        queries 
        |> getResult
        |> List.fold foldResult ([], []) 
        |> fun (data, errorLog) -> {Data = data ; ErrorLog = errorLog}

type Series =     
    static member private Quotes(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval) =
        let startDate = defaultArg startDate (DateTime.Today.AddYears(-1))
        let endDate = defaultArg endDate (DateTime.Today)
        let interval = defaultArg interval Interval.Daily

        symbols
        |> Seq.toList
        |> List.map (fun symbol -> 
            {
                Symbol = symbol
                StartDate = startDate
                EndDate = endDate
                Interval = interval
            })
        |> DownloadUtils.getSeries

    static member MetaData(symbol: seq<string>) = 
        Series.Quotes(symbols=symbol).Data
        |> List.map (fun xs -> xs.Meta)

    static member History(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime, ?interval: Interval) = 
        Series.Quotes(symbols=symbols,?startDate=startDate,?endDate=endDate,?interval=interval).Data
        |> List.collect (fun xs -> xs.History)
    
    static member Events(symbols: seq<string>, ?startDate: DateTime, ?endDate: DateTime) = 
        Series.Quotes(symbols=symbols,?startDate=startDate,?endDate=endDate).Data
        |> List.map (fun xs -> xs.Events)