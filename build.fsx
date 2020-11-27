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
         "5.0"
    ]

[<RequireQualifiedAccess>]
type Targets = 
   Sdk of version:string
   | Runtime of version:string
   | Build
   | Push of Targets
   | PushAll
   | Generic of name:string

let rec targetName = 
    function
       | Targets.Sdk version -> 
            if version = "" then "Sdk"
            else
                version |> sprintf "Sdk-%s"
       | Targets.Build -> "Build"
       | Targets.Runtime version -> 
           if version = "" then "Runtime"
           else
               version |> sprintf "Runtime-%s"
       | Targets.Generic name -> name
       | Targets.Push t -> "Push" + (t |> targetName)
       | Targets.PushAll -> "Push"


let createTagName target = 
    let targetName = 
        match target with
        Targets.Push t ->
            t |> targetName
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

let docker workdir command =
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
    run "docker" workdir (arguments.Replace("  "," ").Trim())

create Targets.Build ignore
create Targets.PushAll ignore
create <| Targets.Sdk "" <| ignore
create <| Targets.Runtime "" <| ignore
create <| Targets.Push(Targets.Sdk "") <| ignore
create <| Targets.Push(Targets.Runtime "") <| ignore

let getRuntimeFileVersion conf = 
    let isDebug = conf = DotNet.BuildConfiguration.Debug
    if isDebug then
       "runtime-debug"
    else
       "runtime"

let setupTargets target allTarget = 
    let pushTarget = Targets.Push target
    let tag = target |> createTagName
    create pushTarget (fun _ -> docker "." (Push tag))
    target ?=> pushTarget  |> ignore
    pushTarget ==> (Targets.Push(allTarget)) ==> Targets.PushAll |> ignore
    target ==> allTarget |> ignore

sdkVersions
|> List.iter(fun version ->
    let createRuntime target buildTarget _ =
        let tag = target |> createTagName
        let runtimeFileVersion = "Dockerfile.runtime"
        let buildArgs = ["RUNTIME_VERSION",version]
        docker "." <| Build(Some(runtimeFileVersion),tag ,buildArgs,buildTarget)
        setupTargets target (Targets.Runtime "")

    let target = Targets.Runtime version
    create target (createRuntime target (Some "runtime"))
    let target = Targets.Runtime <| version + "-debugable"
    create target (createRuntime target None)   
)

sdkVersions
|> List.iter(fun version ->
    let target = (Targets.Sdk version)
    let tag = 
        target
        |> createTagName
    create target (fun _ ->   
        let buildArgs = ["SDK_VERSION",version]
        let file = Some("Dockerfile.sdk")
        docker "." <| Build(file,tag,buildArgs,None)
    )

    setupTargets target (Targets.Sdk "")
)

Targets.Sdk "" ==> Targets.Build
Targets.Runtime "" ==> Targets.Build

Targets.Build ?=> Targets.PushAll

Targets.Build
|> runOrDefaultWithArguments 