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
         |> List.choose (fun series -> 
             match series.Meta.Symbol with
             | "MSFT" -> Some (series.History |> List.map (fun xs -> xs.AdjustedClose))
             | _ -> None)

     let ibmDiv = 
         response.Data
         |> List.filter (fun xs -> xs.Meta.Symbol = "IBM")
         |> List.exactlyOne
         |> fun ibmSeries -> 
             match ibmSeries.Events with
             | Some events -> Some events.Dividends
             | None -> None

     let missing = response.ErrorLog

     printfn "args are %A" argv
     0 // return an integer exit code 