#load "JsonApis.fsx"
#r "nuget: FSharp.Data"

open System
open JsonApis.Providers
open FSharp.Data

module Quotes = 

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
    
    type QuoteQuery = 
        { 
            Symbol : string
            StartDate : System.DateTime
            EndDate : System.DateTime
            Interval : Interval
        }

    type Quote = 
        {   
            Symbol : string
            Date : System.DateTime
            Open : decimal
            High : decimal
            Low : decimal
            Close : decimal
            AdjustedClose : decimal
            Volume : decimal
        }
    
    type Dividend = 
        {
            DividendId : int
            Amount : float
        }
    
    let generateChartQueryUrl (quoteQuery : QuoteQuery) = 

        let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds() |> string

        $"https://query1.finance.yahoo.com/v8/finance/chart/{quoteQuery.Symbol}?&" +
        $"period1={datetimeToUnix quoteQuery.StartDate}&period2={datetimeToUnix quoteQuery.EndDate}&" +
        $"interval={quoteQuery.Interval.ToString()}&" + 
        "includePrePost=true&events=div%7CSplit"
      
    let populateQuotes (chartResult : Chart.Result []) = 

        let symbol =
            chartResult
            |> Array.map (fun xs -> xs.Meta.Symbol)
            |> Array.tryExactlyOne
        
        let quote = 
            chartResult
            |> Array.collect (fun xs -> xs.Indicators.Quote)
            |> Array.tryExactlyOne
        
        let adjustedClose = 
            chartResult
            |> Array.collect (fun xs -> xs.Indicators.Adjclose)
            |> Array.tryExactlyOne
        
        let timestamp = 
            chartResult 
            |> Array.collect (fun xs -> xs.Timestamp)
        
        let dataIsAlignedWithTime (chartDataToCheck: Chart.Quote * Chart.Adjclose) = 
            let quote, adjustedClose = chartDataToCheck
            Set([quote.Close.Length; 
                 quote.High.Length;
                 quote.Open.Length;
                 quote.Low.Length;
                 quote.Volume.Length;
                 adjustedClose.Adjclose.Length;
                 timestamp.Length]).Count = 1
        
        let resultData = symbol, quote, adjustedClose

        match resultData with
        | Some symbol, 
          Some quote, 
          Some adjustedClose 
          when (quote, adjustedClose) 
               |> dataIsAlignedWithTime -> 
                timestamp
                |> Array.Parallel.mapi (fun i ts -> 
                    { 
                        Symbol = symbol
                        Date = DateTimeOffset.FromUnixTimeSeconds(int64 ts).DateTime
                        Open = quote.Open.[i]
                        High = quote.High.[i]
                        Low = quote.Low.[i]
                        Close = quote.Close.[i]
                        AdjustedClose = adjustedClose.Adjclose.[i]
                        Volume = decimal quote.Volume.[i]
                    })
                    |> Array.toList
                    |> Ok
        | None, _, _ -> Error "Unable to find ticker symbol from meta"
        | _, None, _ -> Error "Quote not found"
        | _, _, None -> Error "Adjusted close not found"
        | _ -> Error $"Missing data for: {symbol}"
                    
    let private retryCount = 5
    let private parallelSymbols = 5
    let private cache = Runtime.Caching.createInMemoryCache (TimeSpan(hours=12,minutes=0,seconds=0))

    let parseChart (query : QuoteQuery) (chartRoot : Chart.Root) = 
        let chartError = chartRoot.Chart.Error.JsonValue.ToString()
        match populateQuotes chartRoot.Chart.Result with
        | Ok quotes when chartError = "null" -> 
            cache.Set(query.ToString(), quotes)
            Ok quotes
        | Error popError -> Error popError
        | _ -> Error chartError

    let rec asyncLoadChart attempt query = 
        async {
                let queryUrl = generateChartQueryUrl query
            try
                match cache.TryRetrieve(query.ToString()) with
                | Some quotes -> return Ok quotes
                | None -> 
                    let! chart = Chart.AsyncLoad(queryUrl)
                    return parseChart query chart
            with
            | e -> 
                if attempt > 0 then
                    return! asyncLoadChart (attempt - 1) query
                else return $"Failed to request {query.Symbol}, Error: {e}" |> Error
            }
    
    let rec getSymbols (queries : list<QuoteQuery>) output =
        
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
        
    let private getResult queries =
        getSymbols queries []
    
    let private tryGet queries = 
        queries
        |> getResult 
        |> List.map (fun xs -> 
            match xs with
            | Ok quotes -> Some quotes
            | Error _ -> None)
    
    let private get queries = 
        queries
        |> getResult
        |> List.map (fun xs -> 
            match xs with
            | Ok quotes -> quotes
            | Error e -> failwith e)
    
    let private request symbol =
        { 
            Symbol = symbol
            StartDate = DateTime.Today.AddYears(-1)
            EndDate = DateTime.Today
            Interval = Daily
        }
        
    let private startOn startOn query : QuoteQuery = 
        {query with StartDate=startOn}

    let private endOn endOn quoteQuery : QuoteQuery = 
        {quoteQuery with EndDate=endOn}
    
    let private ofInterval ofInterval quoteQuery : QuoteQuery = 
        {quoteQuery with Interval=ofInterval}
    
    type YahooQueryFun() =

        member this.request query = request query
        member this.startOn startDate query = startOn startDate query
        member this.endOn endDate query = endOn endDate query
        member this.ofInterval interval query = ofInterval interval query
        
        member this.getResult queries = getResult queries
        member this.tryGet queries = tryGet queries
        member this.get queries = get queries
            
    type YahooQuery =

        static member Quotes(symbols: seq<string>,?startDate: DateTime,?endDate: DateTime,?interval: Interval) =
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
            |> get 
        
        static member Quotes(symbol: string,?startDate: DateTime,?endDate: DateTime,?interval: Interval) =
            YahooQuery.Quotes(symbols=[symbol],?startDate=startDate,?endDate=endDate,?interval=interval)