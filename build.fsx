#r "paket:
nuget Fake ~> 5 //
nuget Fake.Core ~> 5 //
nuget Fake.Core.Target  //
nuget Fake.DotNet //
nuget Fake.DotNet.AssemblyInfoFile //
nuget Fake.DotNet.Cli //
nuget Fake.DotNet.NuGet //
nuget Fake.IO.FileSystem //
nuget Fake.Tools.Git ~> 5 //"
#load "./.fake/build.fsx/intellisense.fsx"


#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.DotNet

let configurations = 
    [
        DotNet.BuildConfiguration.Release
        DotNet.BuildConfiguration.Debug
    ]
    
let sdkVersions = 
    [
        "" //use default
    ]

let appVersions = 
   [ for sdk in sdkVersions ->
       [
           for conf in configurations -> sdk,conf,""
       ]
   ] |> List.collect id

[<RequireQualifiedAccess>]
type Targets = 
   Sdk of version:string
   | App of sdkVersion : string * runtimeVersion : DotNet.BuildConfiguration * selfVersion : string
   | Runtime of DotNet.BuildConfiguration
   | CouchDb
   | Build
   | PushSdk of version:string
   | PushRuntime of DotNet.BuildConfiguration
   | PushCouchDb
   | PushApp of version: string
   | Push
   | Generic of name:string
let defaultSdk = "3.1" 
let targetName = 
    function
       | Targets.Sdk version -> 
            let version = 
                if version = "" then defaultSdk else version
            sprintf "Sdk-%s" version
       | Targets.Build -> "Build"
       | Targets.Runtime release -> 
           match release with
           DotNet.BuildConfiguration.Release -> "Runtime"
           | DotNet.BuildConfiguration.Debug -> "Runtime-debug"
           | _ -> failwith "Configuration unknown"
       | Targets.CouchDb -> "CouchDb"
       | Targets.Generic name -> name
       | Targets.PushSdk version -> 
           if version = "" then "PushSdk"
           else sprintf "PushSdk-%s" version
       | Targets.Push -> "Push"
       | Targets.PushRuntime release -> 
           match release with
           DotNet.BuildConfiguration.Release -> "PushRuntime"
           | DotNet.BuildConfiguration.Debug -> "PushRuntime-debug"
           | _ -> failwith "Configuration unknown"
       | Targets.App(sdk,runtime,self) -> 
           let sdk = 
               if sdk = "" then defaultSdk else sdk
           let version = 
               System.String.Join("-",[
                       yield sdk
                       yield if runtime = DotNet.BuildConfiguration.Release then "runtime:" + sdk else sprintf "runtime:%s-debug" sdk
                       yield if self = "" then sdk else self
                   ])
           sprintf "App-%s" version
       | Targets.PushApp(self) -> 
           sprintf "Push%s" self
       | Targets.PushCouchDb -> "PushCouchDb"


let createTagName target = 
    let targetName = 
        match target with
        | Targets.PushSdk _
        | Targets.PushRuntime _
        | Targets.PushCouchDb
        | Targets.PushApp _ ->
            (target |> targetName).Substring("push".Length)
        | _ -> target |> targetName
    let tag = 
        match targetName.ToLower().Split('-') |> List.ofArray with
        [] -> failwith "Must have a name"
        | [tag] -> tag
        | major::[minor] -> sprintf "%s:%s" major minor
        | major::minor::tail -> sprintf "%s:%s-%s" major minor (System.String.Join("-",tail))
    sprintf "kmdrd/%s" tag

open Fake.Core.TargetOperators
let inline (==>) (lhs : Targets) (rhs : Targets) =
    Targets.Generic((targetName lhs) ==> (targetName rhs))

let inline (?=>) (lhs : Targets) (rhs : Targets) =
    Targets.Generic((targetName lhs) ?=> (targetName rhs))

let create target f = 
    let targetName = 
        target
        |> targetName
    Target.create targetName (f target)

let runOrDefaultWithArguments =
    targetName
    >> Target.runOrDefaultWithArguments 

let run command workingDir args = 
    let arguments = 
        match args |> String.split ' ' with
        [""] -> Arguments.Empty
        | args -> args |> Arguments.OfArgs
    RawCommand (command, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
    
type DockerCommand = 
    Push of string
    | Pull of string
    | Build of file:string option * tag:string * buildArgs: (string * string) list
    | Tag of original:string * newTag:string

let docker command =
    let arguments = 
        match command with
        Push tag -> sprintf "push %s" tag
        | Pull tag -> sprintf "pull %s" tag
        | Build(file,tag,buildArgs) -> 
            let buildArgs = 
                System.String.Join(" ", 
                    buildArgs 
                    |> List.map(fun (n,v) -> sprintf "--build-arg %s=%s" n v)
                ).Trim()
            ( match file with
              None -> 
                  sprintf "build -t %s %s ."  
              | Some f -> sprintf "build -f %s -t %s %s ." f) (tag.ToLower()) buildArgs
        | Tag(t1,t2) -> sprintf "tag %s %s" t1 t2
    let arguments = 
        //replace multiple spaces with just one space
        let args = arguments.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)
        System.String.Join(" ",args) 
    run "docker" "." (arguments.Replace("  "," ").Trim())

create Targets.Build (fun _ _ -> ())
create Targets.Push (fun _ _ -> ())

let getRuntimeFileVersion conf = 
    let isDebug = conf = DotNet.BuildConfiguration.Debug
    if isDebug then
       "runtime-debug"
    else
       "runtime"

configurations
|> List.iter(fun conf->
    let target = (Targets.Runtime conf)
    let targetName = getRuntimeFileVersion conf 
    let tag = target |> createTagName
    create target (fun (Targets.Runtime conf) _ ->
        let runtimeFileVersion = sprintf "Dockerfile.%s" targetName
        docker (Build(Some(runtimeFileVersion),tag,[]))
    )
    let pushTarget = Targets.PushRuntime conf
    create pushTarget (fun _ _ -> docker (Push tag))
    target ==> Targets.Build |> ignore
    target ==> pushTarget  ==> Targets.Push |> ignore
)

sdkVersions
|> List.iter(fun version ->
    let target = (Targets.Sdk version)
    let tag = 
        target
        |> createTagName
    create target (fun (Targets.Sdk version) _ ->   
        let buildArgs = 
            if "" = version then []
            else ["VERSION",version]
        let file = Some("Dockerfile.sdk")
        docker (Build(file,tag,buildArgs))
    )
    let pushTarget = (Targets.PushSdk version)
    create pushTarget (fun _ _ -> docker (Push tag))
    if version <> "" then target ==> (Targets.Sdk "") |> ignore
    target ==> Targets.Build |> ignore
    target ==> pushTarget ==> Targets.Push|> ignore
)

appVersions
|> List.iter(fun (sdk,runtime,self) ->
    let target = Targets.App(sdk,runtime,self)
    let tag = 
        if "" = self then "app"
        else sprintf "app:%s" self
        
    create target (fun (Targets.App(sdk,runtime,self)) _ ->   
        let buildArgs = 
            [
                if sdk <> "" then yield "SDK_VERSION",sdk
                match runtime with
                  DotNet.BuildConfiguration.Release -> yield "RUNTIME_VERSION",sdk
                  | _ -> yield "RUNTIME_VERSION",sdk + ":debug"
                if self <> "" then yield "VERSION",self
            ]
        let file = Some("Dockerfile.app")
        docker (Build(file,tag,buildArgs))
    )
    let pushTarget = Targets.PushApp(target |> targetName)
    create pushTarget (fun _ _ -> docker (Push tag))
    Targets.Sdk(sdk) ?=> target |> ignore
    target ==> Targets.Build |> ignore
    Targets.Runtime(runtime) ?=> target |> ignore
    target ==> pushTarget ==> Targets.Push|> ignore
)

create Targets.CouchDb (fun t _ ->
    let tag = t |> createTagName
    tag
    |> sprintf "build -t %s ."
    |> run "docker" "couchdb/" 
)

create Targets.PushCouchDb (fun t _ ->
    let tag = (t |> createTagName)
    docker (Push tag)
)

Targets.CouchDb ==> Targets.Build
Targets.PushCouchDb ==> Targets.Push
Targets.Build ?=> Targets.Push

Targets.Build
|> runOrDefaultWithArguments 