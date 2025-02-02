﻿[<AutoOpen>]
module Fun.Build.StageBuilder

open System
open System.Threading.Tasks


type StageBuilder(name: string) =

    member _.Run(build: BuildStage) = build.Invoke(StageContext.Create name)


    member inline _.Yield(_: unit) = BuildStage id

    member inline _.Yield([<InlineIfLambda>] condition: BuildStageIsActive) = condition
    member inline _.Yield([<InlineIfLambda>] builder: BuildStep) = builder
    member inline _.Yield(stage: StageContext) = stage


    member inline _.Delay([<InlineIfLambda>] fn: unit -> BuildStage) = BuildStage(fun ctx -> fn().Invoke(ctx))

    member inline _.Delay([<InlineIfLambda>] fn: unit -> BuildStageIsActive) = BuildStage(fun ctx -> { ctx with IsActive = fn().Invoke })

    member inline _.Delay([<InlineIfLambda>] fn: unit -> BuildStep) =
        BuildStage(fun ctx ->
            { ctx with
                Steps = ctx.Steps @ [ Step.StepFn(fn().Invoke) ]
            }
        )

    member inline _.Delay([<InlineIfLambda>] fn: unit -> StageContext) =
        BuildStage(fun ctx -> { ctx with Steps = ctx.Steps @ [ Step.StepOfStage(fn ()) ] })


    member inline _.Combine([<InlineIfLambda>] condition: BuildStageIsActive, [<InlineIfLambda>] build: BuildStage) =
        BuildStage(fun ctx -> build.Invoke { ctx with IsActive = condition.Invoke })

    member inline _.Combine([<InlineIfLambda>] builder: BuildStep, [<InlineIfLambda>] build: BuildStage) =
        BuildStage(fun ctx ->
            build.Invoke
                { ctx with
                    Steps = ctx.Steps @ [ Step.StepFn builder.Invoke ]
                }
        )

    member inline _.Combine(stage: StageContext, [<InlineIfLambda>] build: BuildStage) =
        BuildStage(fun ctx -> build.Invoke { ctx with Steps = ctx.Steps @ [ Step.StepOfStage stage ] })


    member inline _.For([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] fn: unit -> BuildStage) =
        BuildStage(fun ctx -> fn().Invoke(build.Invoke(ctx)))

    member inline _.For([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] fn: unit -> BuildStageIsActive) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with IsActive = fn().Invoke }
        )

    member inline _.For([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] fn: unit -> BuildStep) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps = ctx.Steps @ [ Step.StepFn(fn().Invoke) ]
            }
        )

    member inline _.For([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] fn: unit -> StageContext) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with Steps = ctx.Steps @ [ Step.StepOfStage(fn ()) ] }
        )


    /// Add or override environment variables
    [<CustomOperation("envVars")>]
    member inline _.envVars([<InlineIfLambda>] build: BuildStage, kvs: seq<string * string>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                EnvVars = kvs |> Seq.fold (fun state (k, v) -> Map.add k v state) ctx.EnvVars
            }
        )

    /// Set timeout for every step under the current stage.
    /// Unit is second.
    [<CustomOperation("timeout")>]
    member inline _.timeout([<InlineIfLambda>] build: BuildStage, seconds: int) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Timeout = ValueSome(TimeSpan.FromSeconds seconds)
            }
        )

    /// Set timeout for every step under the current stage.
    /// Unit is second.
    [<CustomOperation("timeout")>]
    member inline _.timeout([<InlineIfLambda>] build: BuildStage, time: TimeSpan) =
        BuildStage(fun ctx -> { build.Invoke ctx with Timeout = ValueSome time })


    /// Set timeout for every step under the current stage.
    /// Unit is second.
    [<CustomOperation("timeoutForStep")>]
    member inline _.timeoutForStep([<InlineIfLambda>] build: BuildStage, seconds: int) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                TimeoutForStep = ValueSome(TimeSpan.FromSeconds seconds)
            }
        )

    /// Set timeout for every step under the current stage.
    /// Unit is second.
    [<CustomOperation("timeoutForStep")>]
    member inline _.timeoutForStep([<InlineIfLambda>] build: BuildStage, time: TimeSpan) =
        BuildStage(fun ctx -> { build.Invoke ctx with TimeoutForStep = ValueSome time })


    /// Set if the steps in current stage should run in parallel, default value is true.
    [<CustomOperation("paralle")>]
    member inline _.paralle([<InlineIfLambda>] build: BuildStage, ?value: bool) =
        BuildStage(fun ctx -> { build.Invoke ctx with IsParallel = defaultArg value true })

    /// Set workding dir for all steps under the stage.
    [<CustomOperation("workingDir")>]
    member inline _.workingDir([<InlineIfLambda>] build: BuildStage, dir: string) =
        BuildStage(fun ctx -> { build.Invoke ctx with WorkingDir = ValueSome dir })


    /// Add a step.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] buildStep: StageContext -> BuildStep) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun (ctx, i) -> async {
                            let builder = buildStep ctx
                            return! builder.Invoke(ctx, i)
                        }
                        )
                    ]
            }
        )


    /// Add a step to run command. This will not encrypt any sensitive information when print to console.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, exe: string, args: string) =
        BuildStage(fun ctx -> build.Invoke(ctx).AddCommandStep(fun _ -> async { return exe + " " + args }))

    /// Add a step to run command. This will not encrypt any sensitive information when print to console.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, command: string) =
        BuildStage(fun ctx -> build.Invoke(ctx).AddCommandStep(fun _ -> async { return command }))

    /// Add a step to run command. This will not encrypt any sensitive information when print to console.
    [<CustomOperation("run")>]
    member _.run(build: BuildStage, step: StageContext -> string) =
        BuildStage(fun ctx -> build.Invoke(ctx).AddCommandStep(fun ctx -> async { return step ctx }))

    /// Add a step to run command. This will not encrypt any sensitive information when print to console.
    [<CustomOperation("run")>]
    member _.run(build: BuildStage, step: StageContext -> Async<string>) = BuildStage(fun ctx -> build.Invoke(ctx).AddCommandStep(step))


    /// Add a step to run a async.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, step: Async<unit>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun _ -> async {
                            do! step
                            return 0
                        }
                        )
                    ]
            }
        )

    /// Add a step to run a async with exit code returned.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, step: Async<int>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps = ctx.Steps @ [ Step.StepFn(fun _ -> step) ]
            }
        )


    /// Add a step to run.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> unit) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun (ctx, _) -> async {
                            step ctx
                            return 0
                        }
                        )
                    ]
            }
        )

    /// Add a step to run and return an exist code.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> int) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps = ctx.Steps @ [ Step.StepFn(fun _ -> async { return step ctx }) ]
            }
        )


    /// Add a step to run.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> Async<unit>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun (ctx, _) -> async {
                            do! step ctx
                            return 0
                        }
                        )
                    ]
            }
        )

    /// Add a step to run.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> Async<int>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps = ctx.Steps @ [ Step.StepFn(fun _ -> async { return! step ctx }) ]
            }
        )


    /// Add a step to run.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> Task) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun (ctx, _) -> async {
                            do! step ctx |> Async.AwaitTask
                            return 0
                        }
                        )
                    ]
            }
        )

    /// Add a step to run.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> Task<unit>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun (ctx, _) -> async {
                            do! step ctx |> Async.AwaitTask
                            return 0
                        }
                        )
                    ]
            }
        )

    /// Add a step to run.
    [<CustomOperation("run")>]
    member inline _.run([<InlineIfLambda>] build: BuildStage, [<InlineIfLambda>] step: StageContext -> Task<int>) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps = ctx.Steps @ [ Step.StepFn(fun _ -> async { return! step ctx |> Async.AwaitTask }) ]
            }
        )


    /// Add a step to run.
    [<CustomOperation("echo")>]
    member inline _.echo([<InlineIfLambda>] build: BuildStage, msg: StageContext -> string) =
        BuildStage(fun ctx ->
            let ctx = build.Invoke ctx
            { ctx with
                Steps =
                    ctx.Steps
                    @ [
                        Step.StepFn(fun (ctx, i) -> async {
                            printfn "%s %s" (ctx.BuildStepPrefix i) (msg ctx)
                            return 0
                        }
                        )
                    ]
            }
        )

    /// Add a step to run.
    [<CustomOperation("echo")>]
    member inline this.echo([<InlineIfLambda>] build: BuildStage, msg: string) = this.echo (build, (fun _ -> msg))


/// Build a stage with multiple steps which will run in sequence by default.
let inline stage name = StageBuilder name

let inline step x = BuildStep x
