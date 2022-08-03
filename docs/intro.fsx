#r "../src/Quotes.YahooFinance/bin/Debug/net6.0/Quotes.YahooFinance.dll"

open System
open Quotes.YahooFinance
// Why are these returning empty lists?

let aapl = Series.History("AAPL",
                          startDate = DateTime(1990,1,1), 
                          endDate = DateTime.Now)

aapl

let aapl2 = Series.History(["AAPL"],
                          startDate = DateTime(2020,1,1), 
                          endDate = DateTime.Now.AddDays(-1))
aapl2

Series.History("AAPL",displayLogs=true)