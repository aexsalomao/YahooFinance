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
        
    let parsePriceSeries (ticker:string) (httpResponse:string) =
        PriceObsCsv.Parse httpResponse
        |> makePriceObs ticker
    
    let parseDividendSeries (ticker:string) (httpResponse:string) =
        DividendObsCsv.Parse httpResponse
        |> makeDividendObs ticker

    let parseSplitsSeries (ticker:string) (httpResponse:string) =
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
                    | Event.History -> History (parsePriceSeries yahooRequest.Ticker response)
                    | Event.Dividends -> Dividends (parseDividendSeries yahooRequest.Ticker response)
                    | Event.StockSplits -> StockSplits (parseSplitsSeries yahooRequest.Ticker response)
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
                   
        module ObjectOriented =

            let unwrapPriceObs seriesResult = 
                match seriesResult with
                | Ok (EventParsed.History series) -> Ok series
                | Error exn -> Error exn
                | _ -> failwith "should be priceObs"
            
            let unwrapDividendObs seriesResult = 
                match seriesResult with
                | Ok (EventParsed.Dividends series) -> Ok series
                | Error exn -> Error exn
                | _ -> failwith "should be priceObs"
            
            type priceHistory (?startOn:DateTime, ?endOn:DateTime, ?interval:Interval, ?event:Event) =
                let endDate = defaultArg endOn (DateTime.Now)
                let startDate = defaultArg startOn (endDate.AddMonths(-1))
                let interval = defaultArg interval Daily
                let event = defaultArg event Event.History

                member this.StartDate = startDate
                member this.EndDate = endDate
                member this.Interval = interval
                member this.Event = event

                member this.getResult(symbol, ?startOn, ?endOn, ?interval) = 
                    let endDate = defaultArg endOn this.EndDate
                    let startDate = defaultArg startOn this.StartDate
                    let interval = defaultArg interval this.Interval

                    { Ticker = symbol
                      StartDate = startDate
                      EndDate = endDate
                      Event = event
                      Interval = interval}
                    |> getSeriesResult
                    |> unwrapPriceObs
                
                member this.getMany(symbols:seq<string>, ?startOn:DateTime, ?endOn:DateTime, ?interval:Interval) =
                    let endDate = defaultArg endOn this.EndDate
                    let startDate = defaultArg startOn this.StartDate
                    let interval = defaultArg interval this.Interval

                    symbols
                    |> Seq.map (fun symbol ->
                        { Ticker = symbol
                          StartDate = startDate
                          EndDate = endDate
                          Event = Event.History
                          Interval = interval})
                    |> getSeriesMany
                    |> Array.map unwrapPriceObs

open YahooFinance
open YahooFinance.Api.ObjectOriented

let hist = priceHistory(startOn=DateTime(2021,1,1), endOn=DateTime(2021,1,10), interval=Daily)
hist.getMany(["IBM"; "DIS"])