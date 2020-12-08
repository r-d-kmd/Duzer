namespace Kmdrd.Fake

open Fake.Core.TargetOperators
open Fake.Core

module Operators = 
    type ITargets =
        abstract member Name: string with get
    
    let mutable private targets = Set.empty

    let (==>) (lhs : ITargets) (rhs : ITargets) =
        if targets.Contains lhs.Name && targets.Contains rhs.Name then
            lhs.Name ==> rhs.Name |> ignore
        rhs

    let (?=>) (lhs : ITargets) (rhs : ITargets) =
        if targets.Contains lhs.Name && targets.Contains rhs.Name then
            lhs.Name ?=> rhs.Name |> ignore
        rhs

    let (<===) (lhs : ITargets) (rhs : ITargets) =
        rhs ==> lhs //deliberately changing order of arguments

    let create (target : ITargets) f = 
        target.Name
        |> Target.create <| f
        targets <- Set.add target.Name targets

    let runOrDefaultWithArguments (target: ITargets) =
        target.Name
        |> Target.runOrDefaultWithArguments 
        