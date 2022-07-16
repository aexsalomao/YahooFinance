namespace Quotes.YahooFinance.JsonApis

 open Quotes.YahooFinance.EmbeddedResources.Samples
 open FSharp.Data

 module Providers =

     type ChartProvider = JsonProvider<ChartLiterals.Chart> 