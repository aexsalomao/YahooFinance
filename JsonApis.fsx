namespace JsonApis
#r "nuget: FSharp.Data"
#load "EmbeddedResources.fsx"

open FSharp.Data
open EmbeddedResources.Samples

module Providers =

    type Chart = JsonProvider<ChartSampleWithDividends>