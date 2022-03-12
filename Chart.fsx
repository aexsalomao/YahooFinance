#r "nuget: FSharp.Data"

open System
open FSharp.Data
open FSharp.Data.JsonExtensions

module Chart = 

    [<Literal>]
    let ChartSample = "https://query1.finance.yahoo.com/v8/finance/chart/AAPL?&period1=1&period2=1647025009&interval=1d&includePrePost=true&events=div%7CSplit"
    type Chart = JsonProvider<ChartSample>

    type ChartQuery = 
        { 
            Symbol : string
            StartDate : System.DateTime
            EndDate : System.DateTime
            Interval : string
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

    let generateChartQueryUrl (chartQuery : ChartQuery) = 

        let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds() |> string

        $"https://query1.finance.yahoo.com/v8/finance/chart/{chartQuery.Symbol}?&" +
        $"period1={datetimeToUnix chartQuery.StartDate}&period2={datetimeToUnix chartQuery.EndDate}&" +
        $"interval={chartQuery.Interval}&" + 
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
        
        match symbol, quote, adjustedClose with
        | Some symbol, Some quote, Some adjustedClose 
            when Set([quote.Close.Length; 
                      quote.High.Length;
                      quote.Open.Length;
                      quote.Low.Length;
                      quote.Volume.Length;
                      adjustedClose.Adjclose.Length;
                      timestamp.Length]).Count
                    = 1 ->
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
        | None, _, _ -> Error "Missing symbol"
        | _, None, _ -> Error "Missing quote"
        | _, _, None -> Error "Missing adjusted close"
        | _ -> Error $"Missing data for {symbol}"
                
    let private retryCount = 5
    let private parallelSymbols = 5
        
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

                return chartReturn
            with e -> 
                if attempt > 0 then
                    return! asyncLoadChart (attempt - 1) chartQuery
                else return $"Failed to request {chartQuery.Symbol}, Error: {e}" |> Error
                }
    
    let rec getSymbols (queries : list<ChartQuery>) output =
        
        let download thisDownload =
            [| for query in thisDownload do 
                asyncLoadChart retryCount query
                printfn $"{query.Symbol}"|]
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
    
    let getQueries queries = getSymbols queries []

open Chart

// SP500
[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__
type SP500Constituents = CsvProvider<"data-cache/sp500_constituents.csv", ResolutionFolder=ResolutionFolder>
let sp500Constituents = SP500Constituents.Load(__SOURCE_DIRECTORY__ + "/data-cache/sp500_constituents.csv").Cache()

let myQuery = 
        {
            Symbol = "AAPL" 
            StartDate = DateTime.Now.AddDays(-1500.)
            EndDate = DateTime.Now
            Interval = "1d"
        }

let sp500Hist = 
    sp500Constituents.Rows
    |> Seq.toList
    |> List.map (fun xs -> {myQuery with Symbol=xs.Symbol})
    |> getQueries
