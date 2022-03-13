#load "Queries.fsx"

open System
open Queries.Quotes

let yQuery = YahooQueryFun()

let techstocks = ["MSFT"; "AMZN"; "IBM"; "AAPL"]

let buildChartQuery stock = 
    stock
    |> yQuery.request
    |> yQuery.startOn (DateTime.Today.AddDays(-7.))
    |> yQuery.endOn (DateTime.Today)
    |> yQuery.ofInterval Interval.Daily

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
techstocks
|> List.map buildChartQuery
|> yQuery.get

// Method 4 - Get (failwith) - OO
let quotes = YahooQuery.Quotes(techstocks)