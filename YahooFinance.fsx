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

        $"https://query1.finance.yahoo.com/v7/finance/download/{t}?period1={p1}&period2={p2}&interval={i}&events={e}&includeAdjustedClose=true"

    let parsePriceSeries ticker httpResponse =
        PriceObsCsv.Parse httpResponse
        |> makePriceObs ticker
    
    let parseDividendSeries ticker httpResponse =
        DividendObsCsv.Parse httpResponse
        |> makeDividendObs ticker

    let parseSplitsSeries ticker httpResponse =
        StockSplitObsCsv.Parse httpResponse
        |> makeStockSplitObs ticker

    let cache = Runtime.Caching.createInMemoryCache (TimeSpan(hours=12,minutes=0,seconds=0))

    let parseYahooResponse yahooRequest yahooResponse = 
        match yahooRequest.Event with
        | Event.History -> YahooObs.History (parsePriceSeries yahooRequest.Ticker yahooResponse)
        | Event.Dividends -> YahooObs.Dividends (parseDividendSeries yahooRequest.Ticker yahooResponse)
        | Event.StockSplits -> YahooObs.StockSplits (parseSplitsSeries yahooRequest.Ticker yahooResponse)

    let asyncYahooHttpsRequest yahooRequest = 
            async {
                let! asyncRequest = 
                    Http.AsyncRequestString(url = generateYahooUrl yahooRequest, 
                                            httpMethod = "GET",
                                            query = ["format","csv"],
                                            headers = [HttpRequestHeaders.Accept HttpContentTypes.Csv],
                                            silentHttpErrors = false)
                    |> Async.Catch
                
                do! Async.Sleep 2000
                
                let asyncResult = 
                    match asyncRequest with
                    | Choice1Of2 response -> Ok response
                    | Choice2Of2 exn -> Error exn
                                                
                return asyncResult
        }

    let getSeriesResult yahooRequest = 
        let key = yahooRequest.ToString()
        let parseRes = parseYahooResponse yahooRequest
        match cache.TryRetrieve(key) with
        | Some response -> response |> parseRes |> Ok
        | None -> 
            asyncYahooHttpsRequest yahooRequest
            |> Async.RunSynchronously
            |> function
            | Ok response ->
                cache.Set(key, response) 
                response |> parseRes |> Ok
            | Error e -> Error e
        
    let getSeriesMany (yahooRequests: YahooRequest seq) = 
        yahooRequests
        |> Seq.map (fun request -> (request, cache.TryRetrieve(request.ToString()).IsSome))
        |> Seq.groupBy (fun (_, inCache) -> inCache)
        |> Seq.collect (fun (isInCacheFlag, requests) -> 

            let groupRequests = 
                requests 
                |> Seq.map fst 
                |> Seq.toArray

            if isInCacheFlag then 
                groupRequests
                |> Seq.map (fun (request) -> 
                    let response = cache.TryRetrieve(request.ToString()).Value
                    parseYahooResponse request response |> Ok)
            else
                groupRequests
                |> Seq.map asyncYahooHttpsRequest
                |> Async.Parallel
                |> Async.RunSynchronously
                |> Seq.mapi (fun i res -> 
                    match res with
                    | Ok response ->
                        let request = groupRequests.[i]
                        cache.Set(request.ToString(), response)
                        parseYahooResponse request response |> Ok
                    | Error e -> Error e))

    let inline makeTryGet expr =
        match expr with
        | Ok series -> Some series
        | Error _ -> None
        
    let inline makeGet expr = 
        match expr with
        | Ok series -> series
        | Error e -> failwith $"{e}"

    module Api =

        module Functional = 
            let request ticker = 
                {Ticker = ticker
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
                    |> Seq.toArray
                    |> Array.map (fun res -> 
                        match res with
                        | Ok res -> Ok (unwrapHistory res)
                        | Error e -> Error e)
                
                let getMany (yahooRequests : seq<YahooRequest>) = 
                    yahooRequests
                    |> getManyResult
                    |> Array.map makeGet
                
                let getResult (yahooRequest: YahooRequest) =
                    [{ yahooRequest with Event = Event.History}]
                    |> getManyResult
                    |> Seq.head
                    
                let tryGet yahooRequest = 
                    yahooRequest
                    |> (getResult >> makeTryGet)
                
                let get yahooRequest = 
                    yahooRequest
                    |> (getResult >> makeGet)
                   
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

let fb = 
    "FB"
    |> request
    |> ofEvent Event.History
    |> startOn (DateTime(2020, 1, 1))
    |> endOn (DateTime(2020, 1, 5))
    |> getResult

// Price
let sp500Hist = 
    sp500Constituents.Rows
    |> Seq.map (fun xs -> 
        xs.Symbol 
        |> request
        |> ofEvent Event.History
        |> startOn (DateTime(2020, 1, 1))
        |> endOn (DateTime(2020, 1, 30)))
    |> getManyResult