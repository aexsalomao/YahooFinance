#r "nuget: FSharp.Data"

open System.Text.RegularExpressions
open FSharp.Data

module  Fetch = 

    // Cookie and Crumb (safe version) - adapted from https://gist.github.com/kMutagene/b1af6e4d388dbd04a47a1014ad450593 (kMutagene)
    type Cookie = option<string>
    type Crumb = option<string>
    type CookieAndCrumb = { Cookie : Cookie ; Crumb : Crumb ; Response : HttpResponse }

    let fetchCookie (response : HttpResponse) = response.Cookies.TryFind "B"

    let fetchCrumb response = 
        match response.Body with
        | HttpResponseBody.Text body -> 
            let hasCrumb, crumbGroup = Regex("CrumbStore\":{\"crumb\":\"(?<crumb>.+?)\"}").Match(body).Groups.TryGetValue("crumb")
            if hasCrumb then Some (crumbGroup.Value) else None
        | _ -> None

    let getResponse url = 
        try 
            let response = Http.Request(url, httpMethod=HttpMethod.Get)
            Ok response
        with
        | :? System.Net.WebException as e -> Error e

    let fetchCookieCrumb =
        match getResponse "https://finance.yahoo.com/quote" with
        | Ok res -> { Cookie = fetchCookie res ; Crumb = fetchCrumb res ; Response = res }
        | Error e -> failwith $"{e.Message}"