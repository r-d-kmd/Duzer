namespace Kmdrd.Fake

open Fake.Core
open Fake
open Fake.DotNet
open Fake.IO

module Packaging = 
    [<RequireQualifiedAccess>]
    type Targets = 
       Build 
       | Package
       | PackageAndPush
       | Test
       | Release
       | InstallDependencies
       interface Operators.ITargets with
           member x.Name with get() = 
               match x with
               Targets.Build -> "build"
               | Targets.InstallDependencies -> "installdependencies"
               | Targets.Package -> "package"
               | Targets.PackageAndPush -> "packageandpush"
               | Targets.Test -> "test"
               | Targets.Release -> "release"
             
    let run command workingDir (args : string) = 
        let arguments = 
            match args.Trim() |> String.split ' ' with
            [""] -> Arguments.Empty
            | args -> args |> Arguments.OfArgs
        RawCommand (command, arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore

    let buildConfiguration = 
            DotNet.BuildConfiguration.Release
      
    open System.IO
    let verbosity = Quiet
        
    let package conf outputDir projectFile =
        DotNet.publish (fun opts -> 
                            { opts with 
                                   OutputPath = Some outputDir
                                   Configuration = conf
                                   MSBuildParams = 
                                       { opts.MSBuildParams with
                                              Verbosity = Some verbosity
                                       }    
                            }
                       ) projectFile
    let srcPath = "src/"
    let testsPath = "tests/"

    let getProjectFile folder = 
        if Directory.Exists folder then
            Directory.EnumerateFiles(folder,"*.?sproj")
            |> Seq.tryExactlyOne
        else
            None

    let paket workDir args = 
        run "dotnet" workDir ("paket " + args) 

    open Kmdrd.Fake.Operators
    create Targets.Release ignore
    create Targets.InstallDependencies (fun _ ->
        paket srcPath "install"
    )

    create Targets.Build (fun _ ->    
        let projectFile = 
            srcPath
            |> getProjectFile

        package buildConfiguration "./package" projectFile.Value
    )

    let packageVersion = 
        match Environment.environVarOrNone "BUILD_VERSION" with
        None -> "0.1.local"
        | Some bv ->
            sprintf "1.1.%s" bv
                
    create Targets.Package (fun _ ->
        let packages = Directory.EnumerateFiles(srcPath, "*.nupkg")
        
        File.deleteAll packages
        sprintf "pack --version %s ." packageVersion
        |> paket srcPath 
    )

    create Targets.PackageAndPush (fun _ ->
        let apiKey = 
            match Environment.environVarOrNone "API_KEY" with
            None  -> "az"
            | Some key -> key
        let args = 
            let workDir = System.IO.Path.GetFullPath(".")
            sprintf "run -e VERSION=%s -e API_KEY=%s -v %s:/source -t kmdrd/paket-publisher" packageVersion apiKey workDir
        run "docker" "." args
    )

    create Targets.Test (fun _ ->
        match testsPath |> getProjectFile with
        Some tests -> 
            tests |> DotNet.test id
        | None -> printfn "Skipping tests because no tests was found. Create a project in the folder 'tests/' to have tests run"
    )

    Targets.Build
        ==> Targets.Package
        |> ignore

    Targets.Build
        ==> Targets.PackageAndPush
        |> ignore

    Targets.Build
        ?=> Targets.Test
        ?=> Targets.Package
        ?=> Targets.PackageAndPush
        |> ignore

    Targets.InstallDependencies
        ?=> Targets.Build
        |> ignore

    Targets.Release
        <=== Targets.PackageAndPush
        <=== Targets.Test
        <=== Targets.InstallDependencies
        |> ignore