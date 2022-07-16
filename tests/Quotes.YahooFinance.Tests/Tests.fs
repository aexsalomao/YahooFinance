module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnitTyped

open Quotes.YahooFinance.Series

let techstocks = ["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]

let response = 
    techstocks
    |> List.map BuildQuery.create
    |> getSeries

[<Fact>]
let ``getSeries returns QueryResponse`` () =
    response |> should be ofExactType<QueryResponse> 

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

