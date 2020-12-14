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
#load "./src/Tools.fs"
#load "./src/Operators.fs"
#load "./src/Docker.fs"
#load "./src/Packing.fs"


#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.DotNet
open Kmdrd.Duzer.Operators
open Kmdrd.Duzer.Docker

let sdkVersions = 
    [
         "3.1"
         "5.0"
    ]

[<RequireQualifiedAccess>]
[<NoComparison>]
type Targets = 
   Sdk
   | Runtime 
   | Build
   | PushAll
   | Generic of name:string
   interface ITargets with
       member x.Name with get() =
               match x with
               | Targets.Sdk -> "Sdk"
               | Targets.Build -> "Build"
               | Targets.Runtime -> 
                   "Runtime"
               | Targets.Generic name -> name
               | Targets.PushAll -> "Push"

let docker = Docker(".","kmdrd")

let paketBuilder =
    docker.Build("paket-publisher",
                  file = "Dockerfile.paket-publisher"
                )


let pushPaketBuilder =
    docker.Push "paket-publisher"

create Targets.PushAll ignore
create Targets.Build ignore
create <| Targets.Sdk  <| ignore
create <| Targets.Runtime  <| ignore

let getRuntimeFileVersion conf = 
    let isDebug = conf = DotNet.BuildConfiguration.Debug
    if isDebug then
       "runtime-debug"
    else
       "runtime"

sdkVersions
|> List.iter(fun version ->
    let createRuntime tag buildTarget =
        let runtimeFileVersion = "Dockerfile.runtime"
        let buildArgs = ["RUNTIME_VERSION",version]
        let buildTarget = 
            match buildTarget with
            Some buildTarget ->
                docker.Build(tag,
                  file = runtimeFileVersion,
                  buildArgs = buildArgs,
                  target = buildTarget
                )
            | None -> 
                docker.Build(tag,
                  file = runtimeFileVersion,
                  buildArgs = buildArgs
                )
        let pushTarget = docker.Push tag
        buildTarget ?=> pushTarget  |> ignore
        pushTarget ==> Targets.PushAll |> ignore
        

    createRuntime ("runtime:" + version)  (Some "runtime")
    createRuntime ("runtime:" + version + "-debugable") None
)

sdkVersions
|> List.iter(fun version ->
    let tag = "sdk:" + version
    let buildArgs = ["SDK_VERSION",version]
    let file = "Dockerfile.sdk"
    let buildTarget = docker.Build(tag,
                              file = file,
                              buildArgs = buildArgs
                            )
    let pushTarget = docker.Push tag
    buildTarget ?=> pushTarget  |> ignore
    pushTarget ==> Targets.PushAll |> ignore
)

pushPaketBuilder ==> Targets.PushAll
paketBuilder ==> Targets.Build
pushPaketBuilder ?=> pushPaketBuilder
Targets.Build ?=> Targets.PushAll

Targets.Build
|> runOrDefaultWithArguments 