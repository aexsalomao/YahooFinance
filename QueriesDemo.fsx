#r "/Users/antonioelias/Documents/GitHub/YahooFinance/src/YahooFinance/bin/Debug/net5.0/YahooFinance.dll"
#r "nuget: FSharp.Data"
open System
open YahooFinance.Queries.Quotes

let techstocks = ["MSFT"; "AMZN"; "IBM"; "AAPL"]

let yQuery = YahooQueryFun()

let buildChartQuery stock = 
    stock
    |> yQuery.request
    |> yQuery.startOn (DateTime.Today.AddDays(-7.))
    |> yQuery.endOn (DateTime.Today)
    |> yQuery.ofInterval Interval.Monthly

// Method 1 - Option type
techstocks
|> List.map buildChartQuery
|> yQuery.tryGet
|> List.choose id
|> List.concat

// Method 2 - Result<Quote [], str> []
techstocks
|> List.map buildChartQuery
|> yQuery.getResult
|> List.choose (fun quotes -> 
    match quotes with
    | Ok quotes -> Some quotes
    | Error e -> None)
|> List.concat

// Method 3 - Get (failwith)
let xs = 
    techstocks
    |> List.map buildChartQuery
    |> yQuery.get

// Method 4 - Get (failwith) - OO
let quotes = YahooQuery.Quotes(techstocks)