namespace Price

 open System
 open JsonApis.Providers
 open FSharp.Data

 module Series = 

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
             Symbol    : string
             StartDate : System.DateTime
             EndDate   : System.DateTime
             Interval  : Interval
         }

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

     type Series = 
         { 
             Meta         : Chart.Meta
             History      : Quote list
             Events       : Chart.Events option
         }

     type QueryResponse = 
         {
             Data     : Series List
             ErrorLog : string List
         }

     let generateChartQueryUrl (quoteQuery : QuoteQuery) = 

         let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds() |> string

         $"https://query1.finance.yahoo.com/v8/finance/chart/{quoteQuery.Symbol}?&" +
         $"period1={datetimeToUnix quoteQuery.StartDate}&period2={datetimeToUnix quoteQuery.EndDate}&" +
         $"interval={quoteQuery.Interval.ToString()}&" + 
         "includePrePost=true&events=div%7CSplit"

     let checkChartResult (chartResult : Chart.Result) =
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

     let populateSeries (chartResult : Chart.Result) = 
         match checkChartResult chartResult with
         | Error e -> Error e
         | Ok (quote, adjClose) -> 
             let events = 
                 try
                     Some chartResult.Events 
                 with
                 | e -> None

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

             Ok {Meta = chartResult.Meta ; History = quoteHistory ; Events = events}

     let private retryCount = 5
     let private parallelSymbols = 5
     let private cache = Runtime.Caching.createInMemoryCache (TimeSpan(hours=12,minutes=0,seconds=0))

     let parseChart (query : QuoteQuery) (chartRoot : Chart.Root) = 
         let chartError = chartRoot.Chart.Error.JsonValue.ToString()
         match Array.tryExactlyOne chartRoot.Chart.Result with
         | Some chartResult when chartError = "null" ->    
             chartResult 
             |> populateSeries
             |> fun series -> 
                 cache.Set(query.ToString(), series)
                 series
         | _ -> Error chartError

     let rec asyncLoadChart attempt query = 
         async {
                 let queryUrl = generateChartQueryUrl query
             try
                 match cache.TryRetrieve(query.ToString()) with
                 | Some quotes -> return quotes
                 | None -> 
                     let! chart = Chart.AsyncLoad(queryUrl)
                     return parseChart query chart
             with
             | e -> 
                 if attempt > 0 then
                     return! asyncLoadChart (attempt - 1) query
                 else return $"Failed to request {query.ToString()}" |> Error
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

     let private foldQuotesResult (data, errorLog) (queryResult : Result<Series, string>) = 
         match queryResult with
         | Ok series -> (series :: data, errorLog)
         | Error e -> (data, e :: errorLog)

     let getSeries queries = 
         queries 
         |> getResult
         |> fun queriesResult -> 
             let data, errorLog = List.fold foldQuotesResult ([], []) queriesResult
             {Data = data ; ErrorLog = errorLog}

     module BuildQuery =

         let private log message = 
             printfn "\n %s \n" message

         let create symbol = 
             symbol 
             |> request 
             |> fun query -> 
                 printfn "====== Query ======" 
                 log (query.ToString())
                 query

         let startOn startDate query = query |> startOn startDate
         let endOn endDate query = query |> endOn endDate
         let ofInterval interval query = query |> ofInterval interval

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
             |> getSeries

         static member Quotes(symbol: string,?startDate: DateTime,?endDate: DateTime,?interval: Interval) =
             YahooQuery.Quotes(symbols=[symbol],?startDate=startDate,?endDate=endDate,?interval=interval) 
