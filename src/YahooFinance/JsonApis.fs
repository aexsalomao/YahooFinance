namespace JsonApis

 open EmbeddedResources.Samples
 open FSharp.Data

 module Providers =

     type ChartProvider = JsonProvider<ChartLiterals.Chart> 