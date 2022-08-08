module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnitTyped

open Quotes.YahooFinance

[<Fact>]
let ``Series.Download returns ChartSeries list`` () =
    ["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]
    |> Series.Download 
    |> should be ofExactType<ChartSeries> 