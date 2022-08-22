module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnitTyped

open Quotes.YahooFinance

[<Fact>]
let ``Series.Download returns ChartSeries list`` () =
    ["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]
    |> YahooFinance.History 
    |> should be ofExactType<list<Quote>>    