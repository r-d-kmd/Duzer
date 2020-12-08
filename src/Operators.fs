namespace Kmdrd.Fake
open Fake.Core.TargetOperators
open Fake.Core

module Operators = 
    type ITargets =
        abstract member Name: string with get

    type private Generic(name : string) =
        interface ITargets with
            member __.Name with get() = name    

    let (==>) (lhs : ITargets) (rhs : ITargets) =
        Generic((lhs.Name) ==> (rhs.Name)) :> ITargets

    let (?=>) (lhs : ITargets) (rhs : ITargets) =
        Generic((lhs.Name) ?=> (rhs.Name)) :> ITargets

    let (<===) (lhs : ITargets) (rhs : ITargets) =
        rhs ==> lhs //deliberately changing order of arguments

    let create (target : ITargets) = 
        target.Name
        |> Target.create

    let runOrDefaultWithArguments (target: ITargets) =
        target.Name
        |> Target.runOrDefaultWithArguments 
        