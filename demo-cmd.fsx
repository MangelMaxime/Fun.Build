#r "nuget: Fun.Result, 0.2.1"
#r "nuget: Spectre.Console, 0.46.0"
#r "Fun.Build/bin/Debug/netstandard2.0/Fun.Build.dll"

open Fun.Build


module Apps =
    let app1 = "app1"
    let app2 = "app2"
    let app3 = "app3"

    let all = [ app1; app2; app3 ]

let args = {|
    app = fun apps -> CmdArg.Create(shortName = "-a", longName = "--app", values = apps, description = "specify the app you want to dev")
    path = CmdArg.Create("-f", "--file", "publish directory for the app")
    watch = CmdArg.Create(shortName = "-w", description = "if is in watch mode")
|}


pipeline "demo" {
    description "simple demo"
    whenCmdArg (args.app Apps.all)
    stage "build" {
        stage "taiwindcss" {
            whenCmdArg args.watch
            echo "tailwindcss building"
        }
        stage Apps.app1 {
            whenCmdArg (args.app [ Apps.app1 ])
            echo $"run {Apps.app1}"
        }
        stage Apps.app2 {
            whenCmdArg (args.app [ Apps.app2 ])
            echo $"run {Apps.app2}"
        }
    }
    stage "publish" {
        whenAll {
            branch "master"
            cmdArg args.path
        }
        run (fun ctx -> printfn "%A" (ctx.TryGetCmdArg(args.path)))
    }
    runIfOnlySpecified
}

tryPrintPipelineCommandHelp ()
