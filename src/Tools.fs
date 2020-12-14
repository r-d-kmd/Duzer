namespace Kmdrd.Duzer

open Fake.Core
open Fake
open Fake.DotNet
open Fake.IO

module Tools =
    let mutable buildConfiguration = 
            DotNet.BuildConfiguration.Release
      
    let mutable verbosity = Quiet
        
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

    let paket workDir args = 
        run "dotnet" workDir ("paket " + args) 

    let build conf outputDir projectFile =
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
    
    
        