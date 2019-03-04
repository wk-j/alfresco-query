open System

type Options = {
    Path: string
    Query: string
    Host: string
    User: string
    Password: string
}

let rec parseArgs args options =
    match args with
    | "--host" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with User = value}
            parseArgs xss options
        | _ -> parseArgs xs options
    | "--user" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with User = value }
            parseArgs xss options
        | _ -> parseArgs xs options
    | "--password" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with Password = value }
            parseArgs xss options
        | _ -> parseArgs xs options
    | "--query" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with Query = value }
            parseArgs xss newOptions
        | _ -> parseArgs xs options
    | "--path" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with Path = value }
            parseArgs xss newOptions
        | _ -> parseArgs xs options
    | _ -> options

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    let init =
        { Path  = ""
          Query = ""
          User  = "admin"
          Password = "admin"
          Host = "http://localhost:8082" }

    let options = parseArgs args init
    printfn "%A" options

    0