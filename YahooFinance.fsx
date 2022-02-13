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

    type YahooObs =
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

        $"https://query1.finance.yahoo.com/v8/finance/quote/{t}?period1={p1}&period2={p2}&interval={i}&events={e}&includeAdjustedClose=true"
        
    let parsePriceSeries ticker httpResponse =
        PriceObsCsv.Parse httpResponse
        |> makePriceObs ticker
    
    let parseDividendSeries ticker httpResponse =
        DividendObsCsv.Parse httpResponse
        |> makeDividendObs ticker

    let parseSplitsSeries ticker httpResponse =
        StockSplitObsCsv.Parse httpResponse
        |> makeStockSplitObs ticker

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
                | Choice1Of2 response -> 
                    match yahooRequest.Event with
                    | Event.History -> YahooObs.History (parsePriceSeries yahooRequest.Ticker response)
                    | Event.Dividends -> YahooObs.Dividends (parseDividendSeries yahooRequest.Ticker response)
                    | Event.StockSplits -> YahooObs.StockSplits (parseSplitsSeries yahooRequest.Ticker response)
                    |> Ok
                | Choice2Of2 exn -> Error exn
                                            
            return asyncResult
       }

    let processSeriesResult parseCsv makeSeries (result : (Result<string*YahooRequest, exn>)) = 
        match result with
        | Error e -> Error e
        | Ok (seriesCsv, yahooRequest) -> 
            parseCsv seriesCsv
            |> makeSeries yahooRequest.Ticker
            |> Ok

    let getSeriesResult yahooRequest = 
        asyncYahooHttpsRequest yahooRequest
        |> Async.RunSynchronously
    
    let getSeriesMany yahooRequests = 
        yahooRequests
        |> Seq.map asyncYahooHttpsRequest
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
            
            let ofEvent ofEvent yahooRequest : YahooRequest = 
                {yahooRequest with Event=ofEvent}
                                 
            module PriceSeries= 
                let unwrapHistory hist =
                    match hist with
                    | YahooObs.History x -> x
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
                    
                let tryGet yahooRequest = 
                    yahooRequest
                    |> (getResult >> makeTryGet)
                
                let get yahooRequest = 
                    yahooRequest
                    |> (getResult >> makeGet >> unwrapHistory)
                   
        module ObjectOriented =

            let unwrapPriceObs seriesResult = 
                match seriesResult with
                | Ok (YahooObs.History series) -> Ok series
                | Error exn -> Error exn
                | _ -> failwith "should be priceObs"
            
            let unwrapDividendObs seriesResult = 
                match seriesResult with
                | Ok (YahooObs.Dividends series) -> Ok series
                | Error exn -> Error exn
                | _ -> failwith "should be dividendObs"

            module YahooFinanceOO = 

                type yahooFinance(?startOn:DateTime, ?endOn:DateTime, ?interval:Interval, ?event:Event) = 
                    let endDate = defaultArg endOn (DateTime.Now)
                    let startDate = defaultArg startOn (endDate.AddMonths(-1))
                    let interval = defaultArg interval Daily
                    let event = defaultArg event Event.History

                    member val StartDateYf = startDate
                    member val EndDateYf = endDate
                    member val IntervalYf = interval
                    member val EventYf = event
                
                type PriceHistory() = 

                    inherit yahooFinance()

                    member this.LoadResult(symbol) = 
                        { Ticker = symbol
                          StartDate = this.StartDateYf
                          EndDate = this.EndDateYf
                          Event = Event.History
                          Interval = this.IntervalYf}
                        |> getSeriesResult
                        |> unwrapPriceObs

                    static member GetResult(symbol) = 
                       PriceHistory().LoadResult(symbol)

open YahooFinance.Api.Functional
open YahooFinance.Api.Functional.PriceSeries
open YahooFinance

// SP500
[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__
type SP500Constituents = CsvProvider<"data-cache/sp500_constituents.csv", ResolutionFolder=ResolutionFolder>
let sp500Constituents = SP500Constituents.Load(__SOURCE_DIRECTORY__ + "/data-cache/sp500_constituents.csv").Cache()

// Nasdaq
type NasdaqConstituents = CsvProvider<"data-cache/nasdaq_constituents.csv", ResolutionFolder=ResolutionFolder, Separators=";">
let nasdaqConstituents = NasdaqConstituents.Load(__SOURCE_DIRECTORY__ + "/data-cache/nasdaq_constituents.csv").Cache()

type ReturnObs = 
    { Date : DateTime
      Return : float}

type StockObs = 
    { PriceObs : PriceObs array} with

    member this.ReturnObs = 
        this.PriceObs
        |> Array.sortBy (fun xs -> xs.Date)
        |> Array.pairwise
        |> Array.map (fun (pv, fv) -> 
            { Date = pv.Date
              Return = (fv.AdjustedClose / pv.AdjustedClose) - 1.})
    
    member this.CumulativeReturn = 
        this.PriceObs
        |> Array.sortBy (fun xs -> xs.Date)
        |> fun xs -> ((Seq.last xs).AdjustedClose / (Seq.head xs).AdjustedClose) - 1.

// Price
let sp500Hist = 
    sp500Constituents.Rows
    |> Seq.map (fun xs -> 
        xs.Symbol 
        |> request
        |> ofEvent Event.History
        |> startOn (DateTime.Now.AddDays(-10.))
        |> endOn (DateTime.Now))
    |> getManyResult
    |> Seq.choose (fun xs -> 
        match xs with
        | Ok (YahooObs.History obs) -> Some {PriceObs = obs}
        | _ -> None)
    |> Seq.toArray