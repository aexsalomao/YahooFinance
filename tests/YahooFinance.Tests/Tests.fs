module Tests

open System
open Xunit

open Price.Series

let techstocks = ["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]

let response = 
    techstocks
    |> List.map BuildQuery.create
    |> getSeries

let msftSeries = 
    response.Data
    |> List.filter (fun xs -> xs.Meta.Symbol = "MSFT")
    |> List.exactlyOne
    |> fun msft -> msft.History

let ibmDiv = 
    response.Data
    |> List.filter (fun xs -> xs.Meta.Symbol = "IBM")
    |> List.exactlyOne
    |> fun ibmSeries ->  ibmSeries.Events.Dividends

let missing = response.ErrorLog


[<Fact>]
let ``My test`` () =
    Assert.True(true)
