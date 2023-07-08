﻿namespace rec Fun.Build

open System


type PipelineCancelledException(msg: string) =
    inherit Exception(msg)


type PipelineFailedException =
    inherit Exception

    new(msg: string) = { inherit Exception(msg) }
    new(msg: string, ex: exn) = { inherit Exception(msg, ex) }


[<Struct; RequireQualifiedAccess>]
type CmdName =
    | ShortName of shortName: string
    | LongName of longName: string
    | FullName of short: string * long: string

    member this.Names =
        match this with
        | ShortName x -> [ x ]
        | LongName x -> [ x ]
        | FullName(s, l) -> [ s; l ]

type CmdArg =
    {
        Name: CmdName
        Values: string list
        Description: string option
    }

    static member Create(?shortName: string, ?longName: string, ?description: string, ?values) = {
        Name =
            match shortName, longName with
            | Some s, Some l -> CmdName.FullName(s, l)
            | Some s, None -> CmdName.ShortName(s)
            | None, Some l -> CmdName.LongName(l)
            | _ -> failwith "shortName or longName cannot be empty at the same time"
        Values = defaultArg values []
        Description = description
    }

    member this.WithValue value = { this with Values = this.Values @ [ value ] }
    member this.WithValue values = { this with Values = this.Values @ values }
    member this.WithDescription x = { this with Description = Some x }
