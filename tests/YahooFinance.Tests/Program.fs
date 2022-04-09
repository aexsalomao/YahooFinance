open Price.Series

[<EntryPoint>]
let main argv =

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

    printfn "args are %A" argv
    0 
    // return an integer exit code 