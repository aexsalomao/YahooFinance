module Program = 
    open Quotes.YahooFinance

    let [<EntryPoint>] main _ = 
        let techstocks = ["AAPL"; "MSFT"]
        let history = Series.History(techstocks, displayLogs=true)
        System.Console.WriteLine(history)
        0