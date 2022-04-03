namespace JsonApis

open EmbeddedResources.Samples
open FSharp.Data

module Providers =

    type Chart = JsonProvider<ChartSampleWithDividends>