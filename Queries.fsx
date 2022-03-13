#load "JsonApis.fsx"
#r "nuget: FSharp.Data"

open System
open JsonApis.Providers
open FSharp.Data

module Chart = 

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
    
    type ChartQuery = 
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
    
    let generateChartQueryUrl (chartQuery : ChartQuery) = 

        let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds() |> string

        $"https://query1.finance.yahoo.com/v8/finance/chart/{chartQuery.Symbol}?&" +
        $"period1={datetimeToUnix chartQuery.StartDate}&period2={datetimeToUnix chartQuery.EndDate}&" +
        $"interval={chartQuery.Interval.ToString()}&" + 
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
                    |> Ok
        | None, _, _ -> Error "Unable to find ticker symbol from meta"
        | _, None, _ -> Error "Quote not found"
        | _, _, None -> Error "Adjusted close not found"
        | _ -> Error $"Missing data for: {symbol}"
                    
    let private retryCount = 5
    let private parallelSymbols = 5
    let cache = Runtime.Caching.createInMemoryCache (TimeSpan(hours=12,minutes=0,seconds=0))
        
    let rec asyncLoadChart attempt chartQuery = 
        async {
                let queryUrl = generateChartQueryUrl chartQuery
            try
                let! chart = Chart.AsyncLoad(queryUrl)

                let chartReturn = 
                    let jsonErrorStr = chart.Chart.Error.JsonValue.ToString()
                    match jsonErrorStr with
                    | "null" -> populateQuotes chart.Chart.Result
                    | _ -> Error jsonErrorStr
                cache.Set(chartQuery.ToString(), chartReturn)
                return chartReturn
            with e -> 
                if attempt > 0 then
                    return! asyncLoadChart (attempt - 1) chartQuery
                else return $"Failed to request {chartQuery.Symbol}, Error: {e}" |> Error
                }
    
    let rec getSymbols (queries : list<ChartQuery>) output =
        
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
    
    let getResult (queries : ChartQuery seq) =
        queries
        |> Seq.toList
        |> List.map (fun request -> (request, cache.TryRetrieve(request.ToString()).IsSome))
        |> List.groupBy (fun (_, inCache) -> inCache)
        |> List.collect (fun (isInCache, queries) -> 

            let groupQueries = 
                queries 
                |> Seq.map fst 
                |> Seq.toList

            if isInCache then
                groupQueries
                |> List.choose (fun query -> cache.TryRetrieve(query.ToString()))
            else
                getSymbols groupQueries [])
    
    let tryGet queries = 
        queries
        |> getResult 
        |> List.map (fun xs -> 
            match xs with
            | Ok quotes -> Some quotes
            | Error _ -> None)
    
    let get queries = 
        queries
        |> getResult
        |> List.map (fun xs -> 
            match xs with
            | Ok quotes -> quotes
            | Error e -> failwith e)
    
    module Functional = 

        let request symbol = 
            { 
                Symbol = symbol
                StartDate = DateTime.Now.AddMonths(-1)
                EndDate = DateTime.Now
                Interval = Daily
            }
                    
        let startOn startOn chartQuery : ChartQuery = 
            {chartQuery with StartDate=startOn}
        
        let endOn endOn chartQuery : ChartQuery  = 
            {chartQuery with EndDate=endOn}
        
        let ofInterval ofInterval chartQuery : ChartQuery = 
            {chartQuery with Interval=ofInterval}