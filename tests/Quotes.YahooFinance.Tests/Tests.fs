module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnitTyped

open FSharp.Data

open Quotes.YahooFinance

[<Fact>]
let ``downloads json`` () =
    let query = { Symbol = "AAPL"; StartDate = DateTime.Today.AddMonths(-1); EndDate = DateTime.Now; Interval = Interval.Daily }
    let queryUrl  = ParsingUtils.generateChartQueryUrl query
    let chart = Http.RequestString(queryUrl, headers = [ "User-Agent",DownloadUtils.userAgent ])
    chart |> should be ofExactType<string>

[<Fact>]
let ``gets correct list length`` () =
    let query = YahooFinance.History("AAPL", DateTime(2020, 1, 1), DateTime(2020, 12, 21), Interval.Monthly)
    query |> shouldHaveLength 12

[<Fact>]
let ``Series.Download returns ChartSeries list`` () =
    ["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]
    |> YahooFinance.History 
    |> should be ofExactType<list<Quote>>    


