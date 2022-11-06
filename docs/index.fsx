(**
Quotes.YahooFinance
===================
Quotes.YahooFinance is a library created for accessing historical stock related data from the [Yahoo! Finance](https://finance.yahoo.com/) database.
*)

(**
You can use `Quotes.YahooFinance` in [dotnet interactive](https://github.com/dotnet/interactive) 
notebooks in [Visual Studio Code](https://code.visualstudio.com/) 
or [Jupyter](https://jupyter.org/), or in F# scripts (`.fsx` files), 
by referencing (1) #r "nuget: FSharp.Data" and (2) #r "nuget: Quotes.YahooFinance, 0.0.4-alpha" // Use the latest version
*)

(*** hide ***)
#r """..\src\Quotes.YahooFinance\bin\Debug\net6.0\Quotes.YahooFinance.dll"""
#r "nuget: FSharp.Data" // If you don't reference FSharp.Data it will fail silently!
(***)

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
YahooFinance.History(symbols = ["IBM"; "TSLA"], 
                     startDate = DateTime.Today.AddMonths(-1), 
                     endDate = DateTime.Now, 
                     interval = Interval.Daily)
|> List.truncate 3
(*** include-output ***)

(**
### Dividends

- Downloading dividend data for [The Coca-Cola Company](https://finance.yahoo.com/quote/KO/)
*)

YahooFinance.Dividends(symbols=["KO"],
                       startDate = DateTime.Today.AddYears(-1),
                       endDate = DateTime.Now)
|> List.truncate 3
(*** include-output ***)
