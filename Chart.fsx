#r "nuget: FSharp.Data"

open System
open FSharp.Data

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
            |> Array.exactlyOne

        let openPrice, closePrice, high, low, volume = 
            chartResult 
            |> Array.collect (fun xs -> xs.Indicators.Quote)
            |> fun quotes ->
                quotes |> Array.collect (fun xs -> xs.Open),
                quotes |> Array.collect (fun xs -> xs.Close),
                quotes |> Array.collect (fun xs -> xs.High),
                quotes |> Array.collect (fun xs -> xs.Low),
                quotes |> Array.collect (fun xs -> xs.Volume)

        let adjustedClose = 
            chartResult
            |> Array.collect (fun xs -> xs.Indicators.Adjclose |> Array.collect (fun xs -> xs.Adjclose))
        
        let timestamp = 
            chartResult 
            |> Array.collect (fun xs -> xs.Timestamp)
        
        let lengthCounts =
            [openPrice.Length
             closePrice.Length
             high.Length
             low.Length
             volume.Length
             adjustedClose.Length
             timestamp.Length
            ]
            |> Set
        
        if lengthCounts.Count > 1 then
            Error $"Bad data, {symbol}"
        else
            timestamp
            |> Array.mapi (fun i ts -> 
                { 
                    Symbol = symbol
                    Date = DateTimeOffset.FromUnixTimeMilliseconds(int64 ts).DateTime
                    Open  = openPrice.[i]
                    High = high.[i]
                    Low = low.[i]
                    Close = closePrice.[i]
                    AdjustedClose = adjustedClose.[i]
                    Volume = decimal volume.[i]
                })
            |> Ok
    
    let private retryCount = 5
    let private parallelSymbols = 5
        
    let rec asyncLoadChart parseChart attempt chartQuery = 
        async {
            let queryUrl = generateChartQueryUrl chartQuery
            try
                let! result = Chart.AsyncLoad(queryUrl)
                return parseChart result.Chart.Result
            with e -> 
                if attempt > 0 then
                    return! asyncLoadChart parseChart (attempt - 1) chartQuery
                else return $"Failed to request {chartQuery.Symbol}, Error: {e}" |> Error
                }
    
    let rec getSymbols (queries : list<ChartQuery>) output =
        let download thisDownload =
            [| for query in thisDownload do 
                asyncLoadChart populateQuotes retryCount query|]
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
            StartDate = DateTime.Now.AddDays(-50.)
            EndDate = DateTime.Now
            Interval = "1d"
        }

let sp500Hist = 
    sp500Constituents.Rows
    |> Seq.toList
    |> List.map (fun xs -> {myQuery with Symbol=xs.Symbol})
    |> getQueries