(**
Quotes.YahooFinance
===================
Quotes.YahooFinance is a library created for accessing historical stock related data from the [Yahoo! Finance](https://finance.yahoo.com/) database.
*)

(**
You can use `Quotes.YahooFinance` in [dotnet interactive](https://github.com/dotnet/interactive) 
notebooks in [Visual Studio Code](https://code.visualstudio.com/) 
or [Jupyter](https://jupyter.org/), or in F# scripts (`.fsx` files), 
by referencing: #r "nuget: FSharp.Data" and #r "nuget: Quotes.YahooFinance" // Use the latest version
*)

(*** hide ***)
#r """..\src\Quotes.YahooFinance\bin\Debug\net6.0\Quotes.YahooFinance.dll"""
#r "nuget: FSharp.Data"
(***)

(*** hide ***)
#r """..\src\Quotes.YahooFinance\bin\Debug\net6.0\Quotes.YahooFinance.dll"""
#r "nuget: FSharp.Data"
(***)

open System
open Quotes.YahooFinance

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
    
    Default: Today's date - `DateTime.Today`: 
    
    The end date.

4. **interval**, optional: 
    Dafault: Daily data - `Interval.Daily` 
    
    Data granularity.
    
    To view the different types of intervals supported, refer to the `Interval` type documentation.

5. **displayLogs**, optional:
    
    Default: false
    Option to display logs.
*)

(**
Examples:
*)

(**
Downloading daily ticker data for multiple stocks.
*)
YahooFinance.History(symbols = ["IBM"; "TSLA"], 
                     startDate = DateTime.Today.AddDays(-3), 
                     endDate = DateTime.Now, 
                     interval = Interval.Daily)
(*** include-fsi-merged-output ***)
