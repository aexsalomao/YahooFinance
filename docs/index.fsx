(*** condition: prepare ***)

#r """../src/Quotes.YahooFinance/bin/Debug/net6.0/Quotes.YahooFinance.dll"""
// If you don't reference FSharp.Data it will fail silently! 
// Make sure same version as in Quotes.YahooFinance.fsproj
#r "nuget: FSharp.Data, 5.0.2" 


(**
Quotes.YahooFinance
===================
Quotes.YahooFinance is a library created for accessing historical stock related data from the [Yahoo! Finance](https://finance.yahoo.com/) database.
*)

(**
You can use `Quotes.YahooFinance` in [dotnet interactive](https://github.com/dotnet/interactive) 
notebooks in [Visual Studio Code](https://code.visualstudio.com/) 
or [Jupyter](https://jupyter.org/), or in F# scripts (`.fsx` files), 
by referencing


    // Use one of the following two lines
    #r "nuget: Quotes.YahooFinance" // Use the latest version
    #r "nuget: Quotes.YahooFinance,{{fsdocs-package-version}}" // Use a specific version  

*)

open Quotes.YahooFinance
open System

(**
## Downloading stock quotes and dividend data

- You can use `YahooFinance.History` to download the ticker history of a given list of stock symbols.

Parameters:

1. **symbols**, required.

    A collection of ticker symbols.

2. **startDate**, optional. 
    
    Default: One year ago - `DateTime.Today.AddYears(-1)`
    
    The start date.

3. **endDate**, optional. 
    
    Default: Today's date - `DateTime.Today`
    
    The end date.

4. **interval**, optional: 
    
    Dafault: Daily data - `Interval.Daily` 
    
    Data granularity.
    
    To view the different types of intervals supported, refer to the `Interval` type documentation.

5. **displayLogs**, optional:
    
    Default: false

    Option to display logs.

A more detailed description of the parameters can be found in [YahooFinance Type](reference/quotes-yahoofinance-yahoofinance.html).

*)

(**
### Stock quotes

- Downloading daily stock data for [International Business Machines Corporation (IBM)](https://finance.yahoo.com/quote/IBM/) and [Tesla, Inc. (TSLA)](https://finance.yahoo.com/quote/TSLA/).
*)

let quotes = 
    YahooFinance.History(symbols = ["IBM"; "TSLA"], 
                         startDate = DateTime.Today.AddMonths(-1), 
                         endDate = DateTime.Now, 
                         interval = Interval.Daily)

for quote in quotes[..3] do
    printfn $"{quote.Symbol},{quote.Date},%.2f{quote.Open},%.2f{quote.High},%.2f{quote.Low},%.2f{quote.Close},%.2f{quote.AdjustedClose},{quote.Volume}"

(*** include-output ***)

(**
### Dividends

- Downloading dividend data for [The Coca-Cola Company](https://finance.yahoo.com/quote/KO/)
*)

let dividends = 
    YahooFinance.Dividends(symbols=["KO"],
                           startDate = DateTime.Today.AddYears(-3),
                           endDate = DateTime.Now)

for dividend in dividends[..3] do
    printfn $"{dividend.Symbol}, {dividend.Date}, {dividend.Amount} USD"

(*** include-output ***)
