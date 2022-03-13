#load "Queries.fsx"

open System
open Queries.Chart
open Queries.Chart.Functional

let techstocks = ["MSFT"; "IBM"; "AAPL"; "GOOG"]

let buildChartQuery stock = 
    stock
    |> request
    |> startOn (DateTime.Now.AddDays(-14.))
    |> endOn (DateTime.Now)
    |> ofInterval Interval.Daily

// Method 1 - Option type
techstocks
|> List.map buildChartQuery
|> tryGet

// Method 2 - Result<Quote [], str> []
techstocks
|> List.map buildChartQuery
|> getResult

// Method 3 - Get (failwith)
techstocks
|> List.map buildChartQuery
|> get

