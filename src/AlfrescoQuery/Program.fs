open System
open AlfrescoAuthApi
open System.Threading
open Newtonsoft.Json
open System
open Newtonsoft.Json.Serialization
open System.Linq
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open DynamicTables
open AlfrescoCoreApi
open System.Collections.Generic
open System.Collections.Generic
open System.Collections.Generic
open System.Collections.Generic

type DynamicContractResolver(exclude: string []) =
    inherit DefaultContractResolver()
    override __.CreateProperties(types:Type, members:MemberSerialization) =
        let properties = base.CreateProperties(types, members)
        let newProperties = properties |> Seq.filter (fun x -> exclude.Contains(x.PropertyName) |> not)
        upcast System.Collections.Generic.List<JsonProperty>(newProperties)

type Options = {
    Path: string
    // Query: string
    Host: string
    User: string
    Term: string
    Password: string
}

let rec parseArgs args options =
    match args with
    | [term] ->
        { options with Term = term }
    | "--host" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with Host = value}
            parseArgs xss newOptions
        | _ -> parseArgs xs options
    | "--user" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with User = value }
            parseArgs xss newOptions
        | _ -> parseArgs xs options
    | "--password" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with Password = value }
            parseArgs xss newOptions
        | _ -> parseArgs xs options
    // | "--query" :: xs ->
    //     match xs with
    //     | value :: xss ->
    //         let newOptions = { options with Query = value }
    //         parseArgs xss newOptions
    //     | _ -> parseArgs xs options
    | "--path" :: xs ->
        match xs with
        | value :: xss ->
            let newOptions = { options with Path = value }
            parseArgs xss newOptions
        | _ -> parseArgs xs options
    | _ -> options

let getTicket url user password =
    let auth = AlfrescoAuthApi.AlfrescoAuth()
    auth.Init(url)

    let body = TicketBody(UserId = user, Password = password)
    let token = CancellationToken.None
    let data =
        auth.CreateTicketAsync(body,token)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    data.Entry.Id

let basicHeader user password =
    let byteArray = Encoding.ASCII.GetBytes(sprintf "%s:%s" user password)
    let base64 = Convert.ToBase64String(byteArray);
    AuthenticationHeaderValue("Basic", base64)

let defaultOptions() =
    { Path  = ""
      User  = "admin"
      Password = "admin"
      Term = "*.pdf"
      Host = "http://localhost:8082" }

let defaultRequest() =
    {|
        query =
            {| query = "SELECT * FROM cmis:document where cmis:creationDate > TIMESTAMP '2019-01-01T00:00:00.000+00:00' ORDER BY cmis:creationDate DESC"
               language = "cmis" |}
        paging =
            {| maxItems = 10 |}
    |}

let search options =

    let url = options.Host
    let user = options.User
    let password = options.Password
    let ticket = getTicket url user password
    let api = sprintf "%s/alfresco/api/-default-/public/search/versions/1/search?alf_ticket=%s" url ticket

    use client = new HttpClient()
    let request = defaultRequest()
    let json = JsonConvert.SerializeObject(request)
    let content = new StringContent(json)
    let result =
        client.PostAsync(api, content)
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let body = result.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
    JsonConvert.DeserializeObject<AlfrescoCoreApi.NodePaging>(body)


let q options =

    (*
    http://localhost:8090/alfresco/api/-default-/public/alfresco/versions/1/queries/nodes
        ?include=path,properties
        &term=alfresco*
        &orderBy=createdAt desc'
    *)

    let url = options.Host
    let user = options.User
    let password = options.Password
    let term = options.Term

    let ticket = getTicket url user password
    let api =
        [ "{url}"
          "/alfresco/api/-default-/public/alfresco/versions/1/queries/nodes"
          "?alf_ticket={ticket}"
          "&include=path,properties"
          "&term={term}"
          "&orderBy=createdAt desc"
          "&maxItems=20" ]
        |> fun x -> System.String.Concat (x)
        |> fun x -> x.Replace("{url}", url).Replace("{term}", term).Replace("{ticket}", ticket)

    printfn "> %s" api

    use client = new HttpClient()
    let result =
        client.GetAsync(api)
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let body = result.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
    let page = JsonConvert.DeserializeObject<AlfrescoCoreApi.NodePaging>(body)
    let excludes = [ "app:" ]

    let records =
        page.List.Entries |> Seq.map (fun x ->
            let d = Dictionary<string,string>()
            d.["Id"]         <- x.Entry.Id
            d.["Path"]       <- x.Entry.Path.Name
            d.["Name"]       <- x.Entry.Name
            d.["CreatedAt"]  <- x.Entry.CreatedAt.ToString("dd/MM/yy HH:mm")
            d.["ModifiedAt"] <- x.Entry.ModifiedAt.ToString("dd/MM/yy HH:mm")
            for item in x.Entry.Properties.OrderBy(fun x -> x.Key) do
                if excludes.Any(fun k -> item.Key.Contains(k)) |> not then
                    d.[" " + item.Key] <- item.Value.ToString()
            d
        ) |> Seq.toList

    let format = DynamicTables.Format.Minimal;

    let nested =
        records |> Seq.mapi(fun i x ->
            x |> Seq.map(fun k ->
                let dict = Dictionary<string,string>()
                dict.[sprintf "# %d" (i + 1)] <- k.Key
                dict.["Value"] <- k.Value
                dict
            )
    )

    printfn "\n"

    for item in nested do
        DynamicTable.From(item).Write(format)

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    let options = parseArgs args (defaultOptions())

    q options
    0