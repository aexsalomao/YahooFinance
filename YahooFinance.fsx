#r "nuget: FSharp.Data"

open System
open FSharp.Data

module YahooFinance = 

    type Interval = 
        | Daily
        | Weekly
        | Monthly
        override this.ToString() = 
            match this with
            | Daily -> "1d"
            | Weekly -> "1wk"
            | Monthly -> "1mo"
    
    type Event = 
        | History
        | Dividends
        | StockSplits
        override this.ToString() =
            match this with 
            | History -> "history"
            | Dividends -> "div"
            | StockSplits -> "stockSplits"

    type YahooRequest =
        { Ticker : string
          StartDate : DateTime
          EndDate : DateTime
          Interval : Interval
          Event : Event}
   
    type PriceObs = 
        { Ticker : string
          Date : DateTime
          Open : float
          High : float
          Low : float
          Close : float
          AdjustedClose : float
          Volume : float }
    
    type DividendObs = 
        { Ticker : string
          Date : DateTime
          Dividends : float}

    type StockSplitObs =
        { Ticker : string
          Date : DateTime
          StockSplits : string}

    type EventParsed =
        | History of PriceObs []
        | Dividends of DividendObs []
        | StockSplits of StockSplitObs []
    
    type PriceObsCsv = CsvProvider<Sample="Date (date),Open (float),High (float),Low (float), Close (float),Adj Close (float),Volume (float)">
                      
    type DividendObsCsv = CsvProvider<Sample="Date (date),Dividends (float)">

    type StockSplitObsCsv = CsvProvider<Sample="date (date), Stock Splits (string)">

    let makePriceObs idx (csv : PriceObsCsv) = 
        Seq.toArray csv.Rows
        |> Array.Parallel.map (fun xs -> 
            { Ticker = idx
              Date = xs.Date
              Open = xs.Open
              High = xs.High
              Low = xs.Low
              Close = xs.Close
              AdjustedClose = xs.``Adj Close``
              Volume = xs.Volume})
     
    let makeDividendObs idx (csv : DividendObsCsv) = 
        Seq.toArray csv.Rows
        |> Array.Parallel.map (fun xs -> 
            { Ticker = idx
              Date = xs.Date
              Dividends = xs.Dividends})
     
    let makeStockSplitObs idx (csv : StockSplitObsCsv) = 
        Seq.toArray csv.Rows
        |> Array.Parallel.map (fun xs -> 
            { Ticker = idx
              Date = xs.Date
              StockSplits = xs.``Stock Splits``})

    let generateYahooUrl (yahooRequest : YahooRequest) =
        
        let dateTimeToUnix dt = 
            int (DateTimeOffset(dt).ToUnixTimeSeconds())

        let t, p1, p2, i, e = 
            yahooRequest.Ticker,
            dateTimeToUnix yahooRequest.StartDate,
            dateTimeToUnix yahooRequest.EndDate,
            yahooRequest.Interval.ToString(),
            yahooRequest.Event.ToString()

        $"https://query1.finance.yahoo.com/v7/finance/download/{t}?period1={p1}&period2={p2}&interval={i}&events={e}&includeAdjustedClose=true"
        
    let asyncYahooHttpsRequest yahooRequest = 
        async {
            let! asyncRequest = 
                Http.AsyncRequestString(url = generateYahooUrl yahooRequest, 
                                        httpMethod = "GET",
                                        query = ["format","csv"],
                                        headers = [HttpRequestHeaders.Accept HttpContentTypes.Csv],
                                        silentHttpErrors = false)
                |> Async.Catch
            
            let asyncResult = 
                match asyncRequest with
                | Choice1Of2 r -> Ok (r, yahooRequest)
                | Choice2Of2 exn -> Error exn
                                            
            return asyncResult
       }
    
    let parsePriceSeries (ticker:string) (httpResponse:string) =
        PriceObsCsv.Parse httpResponse
        |> makePriceObs ticker
    let parseDividendSeries (ticker:string) (httpResponse:string) =
        DividendObsCsv.Parse httpResponse
        |> makeDividendObs ticker
    let parseSplitsSeries (ticker:string) (httpResponse:string) =
        StockSplitObsCsv.Parse httpResponse
        |> makeStockSplitObs ticker

    let asyncYahooHttpsRequest2 yahooRequest = 
        async {
            let! asyncRequest = 
                Http.AsyncRequestString(url = generateYahooUrl yahooRequest, 
                                        httpMethod = "GET",
                                        query = ["format","csv"],
                                        headers = [HttpRequestHeaders.Accept HttpContentTypes.Csv],
                                        silentHttpErrors = false)
                |> Async.Catch
            
            let asyncResult = 
                match asyncRequest with
                | Choice1Of2 r -> 
                    match yahooRequest.Event with
                    | Event.History -> History (parsePriceSeries yahooRequest.Ticker r)
                    | Event.Dividends -> Dividends (parseDividendSeries yahooRequest.Ticker r)
                    | Event.StockSplits -> StockSplits (parseSplitsSeries yahooRequest.Ticker r)
                    |> Ok
                | Choice2Of2 exn -> Error exn
                                            
            return asyncResult
       }

    let processSeriesResult parseCsv makeSeries (result : (Result<string*YahooRequest,exn>)) = 
        match result with
        | Error e -> Error e
        | Ok (seriesCsv, yahooRequest) -> 
            parseCsv seriesCsv
            |> makeSeries yahooRequest.Ticker
            |> Ok

    let getSeriesResult parseCsv makeSeries yahooRequest = 
        asyncYahooHttpsRequest yahooRequest
        |> Async.RunSynchronously
        |> processSeriesResult parseCsv makeSeries
    
    let getSeriesMany yahooRequests = 
        yahooRequests
        |> Seq.map asyncYahooHttpsRequest2
        |> Async.Parallel 
        |> Async.RunSynchronously
        
    let inline makeTryGet expr =
        match expr with
        | Ok series -> Some series
        | _ -> None
        
    let inline makeGet expr = 
        match expr with
        | Ok series -> series
        | Error e -> failwith $"{e}"

    module Api =

        module Functional = 
            let request idx = 
                {Ticker = idx
                 StartDate = DateTime.Now.AddMonths(-1)
                 EndDate = DateTime.Now
                 Interval = Daily
                 Event = Event.History}
            
            let startOn startOn yahooRequest : YahooRequest = 
                {yahooRequest with StartDate=startOn}
            
            let endOn endOn yahooRequest : YahooRequest = 
                {yahooRequest with EndDate=endOn}
            
            let ofInterval ofInterval yahooRequest : YahooRequest = 
                {yahooRequest with Interval=ofInterval}
                                 
            module PriceSeries= 
                let unwrapHistory hist =
                    match hist with
                    | EventParsed.History x -> x
                    | _ -> failwith "hey you have a coding error developer"

                let getManyResult (yahooRequests : seq<YahooRequest>) = 
                    yahooRequests
                    |> Seq.map (fun xs -> {xs with Event=Event.History})
                    |> getSeriesMany
                
                let getMany (yahooRequests : seq<YahooRequest>) = 
                    yahooRequests
                    |> getManyResult
                    |> Array.map (makeGet >> unwrapHistory)
                
                let getResult (yahooRequest: YahooRequest) =
                    [{ yahooRequest with Event = Event.History}]
                    |> getManyResult
                    |> Seq.head                    
                

                let tryGet yahooRequest = (getResult >> makeTryGet) yahooRequest
                let get yahooRequest = 
                    yahooRequest
                    |> (getResult >> makeGet >> unwrapHistory)
                    
 (*               
            module DividendSeries = 
                let getMany (yahooRequests : seq<YahooRequest>)  = 
                    yahooRequests
                    |> Seq.map (fun req -> {req with Event=Event.Dividends})
                    |> getSeriesMany
  
                let tryGet yahooRequest = (getResult >> makeTryGet) yahooRequest
                let get yahooRequest = (getResult >> makeGet) yahooRequest
                     
            module StockSplitSeries = 
                let getMany (yahooRequests : seq<YahooRequest>) = 
                    yahooRequests
                    |> Seq.map (fun req -> {req with Event=StockSplits})
                    |> getSeriesMany StockSplitObsCsv.Parse makeStockSplitObs
  
                let tryGet yahooRequest = (getMany >> makeTryGet) yahooRequest
                let get yahooRequest = (getMany >> makeGet) yahooRequest
*)
        module ObjectOriented =

            type yahooFinance (symbol, ?startOn:DateTime, ?endOn:DateTime, ?interval:Interval,?event:Event) =
                let endDate = defaultArg endOn (DateTime.Now)
                let startDate = defaultArg startOn (endDate.AddMonths(-1))
                let interval = defaultArg interval Daily
                let event = defaultArg event Event.History
                let request =
                    { Ticker = symbol
                      StartDate = startDate
                      EndDate = endDate
                      Interval = interval
                      Event = event } 

                static member priceHistoryResult(symbol, ?startOn, ?endOn, ?interval) =
                    yahooFinance(symbol, ?startOn=startOn, ?endOn=endOn, ?interval=interval,?event=Some Event.History).Request
                    |> getSeriesResult PriceObsCsv.Parse makePriceObs

                static member priceHistory(symbol, ?startOn, ?endOn, ?interval) =
                    yahooFinance.priceHistoryResult(symbol, ?startOn=startOn, ?endOn=endOn, ?interval=interval)
                    |> makeGet

                static member tryPriceHistory (symbol, ?startOn, ?endOn, ?interval) =
                    yahooFinance.priceHistoryResult(symbol, ?startOn=startOn, ?endOn=endOn, ?interval=interval)
                    |> makeTryGet

                static member priceHistory(symbols:seq<string>, ?startOn, ?endOn, ?interval) =
                    symbols
                    |> Seq.map (fun s -> yahooFinance(s, ?startOn=startOn, ?endOn=endOn, ?interval=interval).Request)
                    |> getSeriesMany

                member this.Request = request

            type PriceSeries()=

                static member GetResult(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    { Ticker = symbol
                      StartDate = startDate
                      EndDate = endDate
                      Interval = interval
                      Event = Event.History }
                    |> getSeriesResult PriceObsCsv.Parse makePriceObs

                static member Get(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    makeGet (PriceSeries.GetResult(symbol=symbol, startOn=startDate, endOn=endDate, interval=interval))

                static member TryGet(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    makeTryGet (PriceSeries.GetResult(symbol=symbol, startOn=startDate, endOn=endDate, interval=interval))
(*
            type DividendSeries() = 
                member x.GetResult(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    { Ticker = symbol
                      StartDate = startDate
                      EndDate = endDate
                      Interval = interval
                      Event = Dividends }
                    |> getSeriesResult DividendObsCsv.Parse makeDividendObs

                member x.TryGet(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    makeTryGet (DividendSeries().GetResult(symbol=symbol, startOn=startDate, endOn=endDate, interval=interval))

                member x.Get(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    makeGet (DividendSeries().GetResult(symbol=symbol, startOn=startDate, endOn=endDate, interval=interval))

            type StockSplitSeries() = 
                member x.GetResult(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    { Ticker = symbol
                      StartDate = startDate
                      EndDate = endDate
                      Interval = interval
                      Event = StockSplits }
                    |> getSeriesResult StockSplitObsCsv.Parse makeStockSplitObs
                
                member x.TryGet(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    makeTryGet (DividendSeries().GetResult(symbol=symbol, startOn=startDate, endOn=endDate, interval=interval))

                member x.Get(symbol, ?startOn, ?endOn, ?interval) =
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    makeGet (DividendSeries().GetResult(symbol=symbol, startOn=startDate, endOn=endDate, interval=interval))
*)

open YahooFinance
open YahooFinance.Api.Functional
open YahooFinance.Api.ObjectOriented

let generatePeriodRequest startDate endDate interval ticker = 
    request ticker
    |> startOn startDate
    |> endOn endDate
    |> ofInterval interval

let startDate, endDate = 
    DateTime(1990,1,1), DateTime.Now

let periodRequest = 
    generatePeriodRequest startDate endDate Daily

[|"MSFT"; "IBM"; "AAPL"; "GOOG"; "DIS";|]
|> Seq.map (periodRequest >> PriceSeries.get)

[|"MSFT"; "IBM"; "AAPL"; "GOOG"; "DIS";|]
|> Seq.map periodRequest
|> PriceSeries.getManyResult


PriceSeries.Get("IBM", startOn=startDate, endOn=endDate, interval=Interval.Daily)

yahooFinance.priceHistory("IBM", startOn=startDate, endOn=endDate, interval=Interval.Daily)
yahooFinance.priceHistory("IBM")
yahooFinance.priceHistory(["IBM";"DIS"])
yahooFinance.tryPriceHistory("IBM", startOn=startDate,interval=Interval.Monthly)

(*
DividendSeries.TryGet("IBM", startOn=startDate, endOn=endDate, interval=Interval.Daily)
StockSplitSeries.GetResult("IBM", startOn=startDate, endOn=endDate, interval=Interval.Daily)
*)

(*
*.FunctionalApi
*.ObjectApi

// two modules, or just work on functional
open YahooFinance.API.Functional
open YahooFinance.API.ObjectOriented

// 1. functional YahooFinance.get that failwith if there's an error.
// 2. functional YahooFinance.tryGet that does Some/None if there's an error, or OkError and then it'd be YahooFinance.getResult
// 3. Same for the objectoriented one, this should automatically return the price history. No "get" needed. Like YahooFinance.tryPriceHistory
// 4. Last thing, implement getting one that takes a sequence of tickers as input and returns an array of histories.

**)
(*
[| Ok [| 1.. 3|]
   Error "this is terrible" |]

open YahooFinance

["MSFT"; "GOOG"; "BBLN"]
|> List.map (fun stock -> 
    stock
    |> PriceHistory.request
    |> YahooFinance.startOn (DateTime(2020,1,1))
    |> YahooFinance.endOn (DateTime(2020,1,5))
    |> YahooFinance.setInterval.weekly
    |> PriceHistory.get)

let msft = 
    "MSFT"
    |> PriceHistory.request
    |> YahooFinance.startOn (DateTime(2020,1,1))
    |> YahooFinance.endOn (DateTime(2020,2,5))
    |> PriceHistory.get        

// OfFrequequncy
"MSFT"
**)