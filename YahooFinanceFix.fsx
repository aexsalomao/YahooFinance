#r "nuget: FSharp.Data"

open System
open System.IO
open System.Text
open System.Net
open System.Text.RegularExpressions
open FSharp.Data
open System

// Cookie and Crumb (safe version) - adapted from https://gist.github.com/kMutagene/b1af6e4d388dbd04a47a1014ad450593 (kMutagene)
let fetchCookie (response : HttpResponse) = response.Cookies.TryFind "B" 

let fetchCrumb (response : HttpResponse) = 
    let crumbRegex = Regex("CrumbStore\":{\"crumb\":\"(?<crumb>.+?)\"}")

    match response.Body with
    | HttpResponseBody.Text body -> 
        let hasCrumb, crumbGroup = crumbRegex.Match(body).Groups.TryGetValue("crumb")
        if hasCrumb then Some (crumbGroup.Value) else None
    | _ -> None

let getResponse url = 
    try 
       let response = Http.Request(url, httpMethod=HttpMethod.Get)
       Ok response
    with
    | :? System.Net.WebException as e -> Error e.Message

type Cookie = string option
type Crumb = string option
type CookieAndCrumb = {Cookie : Cookie; Crumb : Crumb; Response : HttpResponse}

let fetchCookieAndCrumb url =
    match getResponse url with
    | Ok res -> {Cookie = fetchCookie res; Crumb = fetchCrumb res; Response = res}
    | Error e -> failwith $"{e}"

let cookieAndCrumb = fetchCookieAndCrumb "https://finance.yahoo.com/quote"

// YahooFinance
type PriceObs = 
    { Symbol : string
      Date : DateTime
      Open : float
      High : float
      Low : float
      Close : float
      AdjustedClose : float
      Volume : float }

type private PriceObsCsv = CsvProvider<Sample="Date (date),Open (float),High (float),Low (float), Close (float),AdjClose (float),Volume (float)">

let private parseYahooPriceHistory symbol result = 
    PriceObsCsv.Parse(result).Rows
    |> Seq.map (fun x -> 
        { Symbol = symbol 
          Date = x.Date
          Open = x.Open
          High = x.High
          Low = x.Low
          Close = x.Close 
          AdjustedClose = x.AdjClose
          Volume = x.Volume })
    |> Seq.toArray

let datetimeToUnix dt = DateTimeOffset(dt).ToUnixTimeSeconds()
let defaultEvent = "history"
let defaultInterval = "1d"
let defaultStartDate, defaultEndDate = DateTime.Now |> fun now -> now.AddDays(-120.) |> datetimeToUnix, now |> datetimeToUnix

let generateYahooUrl symbol startDate endDate interval event = 
    $"https://query1.finance.yahoo.com/v7/finance/download/{symbol}?" +
    $"period1={startDate}&period2={endDate}&interval={interval}" +
    $"&events={event}&includeAdjustedClose=true"

let private retryCount = 5
let private parallelSymbols = 5

let rec yahooRequest attempt symbol = 
    printfn $"{symbol}"
    async {
        let url = generateYahooUrl symbol defaultStartDate defaultEndDate defaultInterval defaultEvent
        try
            let! result = 
                Http.AsyncRequestString(
                    url = url, 
                    httpMethod = "GET",
                    query = ["format","csv"; "crumb", cookieAndCrumb.Crumb.Value],
                    headers = [HttpRequestHeaders.Accept HttpContentTypes.Csv],
                    cookies = ["B", cookieAndCrumb.Crumb.Value])
            return parseYahooPriceHistory symbol result
        with e ->
            if attempt > 0 then
                return! yahooRequest (attempt - 1) symbol 
                else return! failwith $"Failed to request {symbol}, Error: {e}"
            }

let rec getSymbols (symbols: list<string>) output =
    let download thisDownload =
        [| for symbol in thisDownload do 
            yahooRequest retryCount symbol |]
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect id
        |> Array.toList

    if symbols.Length > parallelSymbols then
        let thisDownload, remaining = symbols |> List.splitAt parallelSymbols
        let result = download thisDownload
        System.Threading.Thread.Sleep(1000) // Throttle 1 sec per batch of symbols
        getSymbols remaining (result @ output)
    else
        let result = download symbols
        result @ output

// SP500
[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__
type SP500Constituents = CsvProvider<"data-cache/sp500_constituents.csv", ResolutionFolder=ResolutionFolder>
let sp500Constituents = SP500Constituents.Load(__SOURCE_DIRECTORY__ + "/data-cache/sp500_constituents.csv").Cache()

let symbolsNotWorking = ["BRK.B", "BF.B", "KSU"]

let sp500Symbols = 
    sp500Constituents.Rows
    |> Seq.map (fun xs -> xs.Symbol)
    |> Seq.toList

let xs = getSymbols sp500Symbols []