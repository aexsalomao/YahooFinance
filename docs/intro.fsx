
#r "../src/Quotes.YahooFinance/bin/Debug/net6.0/Quotes.YahooFinance.dll"

// If you don't reference FSharp.Data it will fail silently!
#r "nuget: FSharp.Data"
open System
open Quotes.YahooFinance

let aapl = YahooFinance.History("AAPL",
                           startDate = DateTime(1990,1,1), 
                           endDate = DateTime.Now)

["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]
|> YahooFinance.History 

