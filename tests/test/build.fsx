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
    
let sdkVersions = 
    [
         "3.1"
    ]

[<RequireQualifiedAccess>]
type Targets = 
  Builder
  | Build
  | Generic of string

let rec targetName = 
    function
       Targets.Builder -> "Builder"
       | Targets.Build -> "Build"
       | Targets.Generic s -> s


let createTagName target = 
    let targetName = target |> targetName
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
    Target.create targetName f

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
    | Build of file:string option * tag:string * buildArgs: (string * string) list * target : string option

let docker command =
    let arguments = 
        match command with
        Push tag -> sprintf "push %s" tag
        | Build(file,tag,buildArgs,target) -> 
            let buildArgs = 
                System.String.Join(" ", 
                    buildArgs 
                    |> List.map(fun (n,v) -> sprintf "--build-arg %s=%s" n v)
                ).Trim()
            let argsWithoutTarget = 
                (match file with
                 None -> 
                    sprintf "build -t %s %s"  
                 | Some f -> sprintf "build -f %s -t %s %s" f) (tag.ToLower()) buildArgs
            match target with
            None -> argsWithoutTarget + " ."
            | Some t -> argsWithoutTarget + (sprintf " --target %s ." t)

    let arguments = 
        //replace multiple spaces with just one space
        let args = arguments.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)
        System.String.Join(" ",args) 
    run "docker" "." (arguments.Replace("  "," ").Trim())

create Targets.Builder (fun _ ->
    let buildArgs = 
        match Environment.environVarOrNone "FEED_PAT" with
        None -> []
        | Some fp -> ["FEED_PAT_ARG",fp]
    docker <| Build(Some "Dockerfile.builder", "builder",buildArgs ,None)
)

create Targets.Build (fun _ ->
    docker <| Build(None, "test", [],None)
)


Targets.Build
|> runOrDefaultWithArguments 