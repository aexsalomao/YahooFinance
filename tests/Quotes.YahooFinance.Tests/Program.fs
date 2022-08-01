module Program = 
    open Quotes.YahooFinance

    let [<EntryPoint>] main _ = 
        let techstocks = ["MSFT"; "AMZN"; "IBM"; "AAPL"; "NovaSbeInc."]
        let history : Quote list = Series.History(techstocks, interval=Interval.Monthly)
        let meta : Meta list = Series.MetaData(techstocks)
        let events : Events list = Series.Events(techstocks)
        0   