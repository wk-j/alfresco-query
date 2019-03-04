open System
open AlfrescoAuthApi
open System.Threading
open System
open System
open System
open Newtonsoft.Json
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

    let getTicket() =
        let url = "http://localhost:8082"
        let auth = AlfrescoAuthApi.AlfrescoAuth()
        auth.Init(url)

        let body = TicketBody(UserId = "admin", Password = "admin")
        let token = CancellationToken.None
        let data =
            auth.CreateTicketAsync(body,token)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        data.Entry.Id


    let url = "http://localhost:8082"
    let ticket = getTicket()
    let options = parseArgs args init

    let client = AlfrescoCoreApi.AlfrescoCore()
    client.Init(url, ticket)

    let token = CancellationToken.None

    (*
    string nodeId,
    int? skipCount,
    int? maxItems,
    IEnumerable<string> orderBy,
    string where,
    IEnumerable<string> include,
    string relativePath,
    bool? includeSource,
    IEnumerable<string> fields
    *)

    let skipCount = Nullable(0)
    let maxItems = Nullable(10)
    let orderBy = [
        "createdAt desc"
        ]
    let where = "(isFile=true)"
    let includes = [
        "path"
        "properties"
    ]
    let relative = "/x/y/z"
    let source = Nullable(false)
    let fields = [ ]

    let items =
        client.ListNodeChildrenAsync(
            "-root-",
            skipCount,
            maxItems,
            orderBy,
            where,
            includes,
            relative,
            source,
            fields,
            token
        )
        |> Async.AwaitTask
        |> Async.RunSynchronously

    // for item in items.List.Entries do
    //     printfn "%A" item.Entry.Name

    //     let a= JsonConvert.SerializeObject(item)
    //     printfn "%A" a

    let items2 =
        client.FindNodesAsync("*.png", "-root-", skipCount, maxItems, "cm:content", includes, orderBy, fields, token)
        |> Async.AwaitTask
        |> Async.RunSynchronously

    for item in items2.List.Entries do
        printfn "%A" item.Entry.Name

        // let json = JsonConvert.SerializeObject(item);
        // printfn "%A" json
    0