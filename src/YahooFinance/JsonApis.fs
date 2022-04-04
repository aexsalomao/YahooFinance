namespace JsonApis

 open EmbeddedResources.Samples.ChartSample
 open FSharp.Data

 module Providers =

     type Chart = JsonProvider<ChartSampleWithDividends> 