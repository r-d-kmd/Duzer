namespace Kmdrd.Duzer

open Kmdrd.Duzer.Operators
open Kmdrd.Duzer.Tools

module Docker =

  type private DockerTarget(name) = 
        interface ITargets with
            member __.Name with get() = name
  
  type Docker(workdir, org) = 
        let docker arguments = run "docker" workdir arguments
        let createTarget dockerOperationName tag f = 
            let target = (DockerTarget(sprintf "%s-%s-%s" dockerOperationName org tag) :> ITargets)
            create target f
            target
        member __.Push (tag : string) =
            createTarget "push" tag (fun _ -> docker <| sprintf "push %s/%s" org tag)
        member x.Build(tag : string,?file : string,?target : string) =
            match file,target with
            None,None -> x.Build(tag,[])
            | Some f, None ->
                x.Build(tag,[],f)
            | None, Some t ->
                x.Build(tag,[],target = t)
            | Some f, Some t ->
                x.Build(tag,[], file=f, target=t)
        member __.Build(tag, buildArgs, ?file, ?target) =
            let tag = sprintf "%s/%s" org tag
            let arguments = 
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
            createTarget "build" tag (fun _ -> docker arguments)
        member x.BuildAndPush(tag,?file : string,?target : string) =
            match file,target with
            None,None -> x.BuildAndPush(tag,[])
            | Some f, None ->
                x.BuildAndPush(tag,[],f)
            | None, Some t ->
                x.BuildAndPush(tag,[],target = t)
            | Some f, Some t ->
                x.BuildAndPush(tag,[], file=f, target=t)
        member x.BuildAndPush(tag, buildArgs, ?file, ?target) =
            let pushTarget = x.Push tag
            let buildTarget = 
                match file,target with
                None,None -> 
                    x.Build(tag,buildArgs)
                | Some f, None -> x.Build(tag,buildArgs,file = f)
                | None, Some t -> x.Build(tag,buildArgs,target = t)
                | Some f, Some t -> x.Build(tag,buildArgs,file = f, target = t)
            buildTarget ?=> pushTarget  |> ignore
            pushTarget ==> DockerTarget("all") |> ignore