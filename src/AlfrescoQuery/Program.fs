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

type DynamicContractResolver(exclude: string []) =
    inherit DefaultContractResolver()
    override __.CreateProperties(types:Type, members:MemberSerialization) =
        let properties = base.CreateProperties(types, members)
        let newProperties = properties |> Seq.filter (fun x -> exclude.Contains(x.PropertyName) |> not)
        upcast System.Collections.Generic.List<JsonProperty>(newProperties)

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

let getTicket url user password =
    let auth = AlfrescoAuthApi.AlfrescoAuth()
    auth.Init(url)

    let body = TicketBody(UserId = "admin", Password = "admin")
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
      Query = ""
      User  = "admin"
      Password = "admin"
      Host = "http://localhost:8082" }

let defaultRequest() =
    {|
        query =
            {| query = "SELECT * FROM cmis:document where cmis:creationDate > TIMESTAMP '2019-01-01T00:00:00.000+00:00' ORDER BY cmis:creationDate DESC"
               language = "cmis" |}
        paging =
            {| maxItems = 10 |}
    |}

let search url user password =
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

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    let options = parseArgs args (defaultOptions())

    let url = options.Host
    let user = options.User
    let password = options.Password
    let page = search url user password

    let records =
        page.List.Entries |> Seq.map (fun x ->
            {|
                Id = x.Entry.Id
                Name = x.Entry.Name
                CreatedAt = x.Entry.CreatedAt
                ModifiedAt = x.Entry.ModifiedAt
            |}
        )

    DynamicTable.From(records).Write();
    0