﻿namespace Examples.SharedCode
open System
open System.Diagnostics
open Interstellar
open System.Threading
open System.Reflection
open System.Runtime.Versioning

module SimpleBrowserApp =
    let runtimeFramework = Assembly.GetEntryAssembly().GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName
    
    let startTitleUpdater mainCtx mapPageTitle (window: IBrowserWindow) =
        let work = async {
            use! holder = Async.OnCancel (fun () ->
                Trace.WriteLine "Window title updater cancelled")
            while true do
                let! pageTitle = Async.AwaitEvent window.Browser.PageTitleChanged
                Trace.WriteLine (sprintf "Browser page title is: %s" pageTitle)
                do! Async.SwitchToContext mainCtx
                window.Title <- mapPageTitle pageTitle
                do! Async.SwitchToThreadPool ()
        }
        let cancellation = new CancellationTokenSource()
        Async.Start (work, cancellation.Token)
        Async.Start <| async {
            do! Async.AwaitEvent window.Closed
            Trace.WriteLine "Window closed"
            cancellation.Cancel ()
        }

    let complexApp = BrowserApp.create (fun mainCtx createWindow -> async {
        Trace.WriteLine (sprintf "Runtime framework: %s" runtimeFramework)
        Trace.WriteLine "Opening first window"
        do! async {
            do! Async.SwitchToContext mainCtx
            use window1 = createWindow { defaultBrowserWindowConfig with address = Some (Uri "https://rendering/"); html = Some "<html><body><p1>Hello world</p1></body></html>" }
            do! window1.Show ()
            do! Async.SwitchToThreadPool ()
            startTitleUpdater mainCtx (fun pageTitle -> sprintf "%A - %s - %s" window1.Platform runtimeFramework pageTitle) window1
            do! Async.AwaitEvent window1.Closed
        }
        Trace.WriteLine "First window closed. Opening next window"
        do! async {
            do! Async.SwitchToContext mainCtx
            use window2 = createWindow { defaultBrowserWindowConfig with address = Some (Uri "https://google.com/") }
            do! window2.Show ()
            do! Async.SwitchToThreadPool ()
            startTitleUpdater mainCtx (fun pageTitle -> sprintf "%A - %s - %s" window2.Platform runtimeFramework pageTitle) window2
            do! Async.AwaitEvent window2.Closed
        }
        Trace.WriteLine "Second window closed -- exiting app"
    })

    let app = BrowserApp.create (fun mainCtx createWindow -> async {
        do! Async.SwitchToContext mainCtx
        Trace.WriteLine "Opening window"
        //<!-- <button onclick=\"window.webkit.messageHandlers.interstellarWkBridge.postMessage('JS Message')\">Click me</button> -->
        //<input type=\"button\" value=\"Click me\" onclick=\"window.webkit.messageHandlers.interstellarWkBridge.postMessage('Hello from Javascript')\" />
        let page = sprintf "
            <html>
                <head>
                    <title>Static HTML Example</title>
                    <script>console.log('head script executed')</script>
                </head>
                <body>
                    <p>Here is some static HTML.</p>
                    <input type=\"button\" value=\"Click me\" onclick=\"window.interstellarBridge.postMessage('Hello from Javascript')\"/>
                    <p id=\"dynamicContent\" />
                    <p id=\"host\" />
                    <p id=\"runtimeFramework\" />
                    <p id=\"browserWindowPlatform\"/>
                    <p id=\"browserEngine\" />
                    <script>console.log('body script executed')</script>
                </body>
            </html>"
        //let window = createWindow { defaultBrowserWindowConfig with showDevTools = true; address = Some "https://rendering/"; html = Some page }
        //let window = createWindow { defaultBrowserWindowConfig with address = Some "https://www.w3schools.com/jsref/tryit.asp?filename=tryjsref_alert" }
        let interstellarDetectorPageUri = Uri "https://gist.githack.com/jwosty/239408aaffd106a26dc2161f86caa641/raw/5af54d0f4c51634040ea3859ca86032694afc934/interstellardetector.html"
        let! page = async {
            use webClient = new System.Net.WebClient() in
                return webClient.DownloadString interstellarDetectorPageUri
        }
        //let window = createWindow { defaultBrowserWindowConfig with showDevTools = true; address = Some interstellarDetectorPageUrl }
        let window = createWindow { defaultBrowserWindowConfig with showDevTools = true; html = Some page; address = None }
        window.Browser.JavascriptMessageRecieved.Add (fun msg ->
            Trace.WriteLine (sprintf "Recieved message: %s" msg)
        )
        startTitleUpdater mainCtx (sprintf "BrowserApp - %s") window
        do! window.Show ()
        do! Async.SwitchToThreadPool ()
        do! Async.Sleep 1_000 // FIXME: introduce some mechanism to let us wait until it is valid to start executing Javascript
        do! Async.SwitchToContext mainCtx
        //window.Browser.LoadString (page, "https://rendering/")
        //let lines = [
        //    "document.getElementById(\"dynamicContent\")               .innerHTML = \"Hello from browser-injected Javascript!\""
        //    sprintf "document.getElementById(\"runtimeFramework\")     .innerHTML = \"Runtime framework: %s\"" runtimeFramework
        //    sprintf "document.getElementById(\"browserEngine\")        .innerHTML = \"Browser engine: %A\"" window.Browser.Engine
        //    sprintf "document.getElementById(\"browserWindowPlatform\").innerHTML = \"BrowserWindow platform: %A\"" window.Platform
        //    sprintf "setTimeout(function () { document.title = \"PSYCH, It's actually a Dynamic Javascript Example!\" }, 5000)"
        //]
        //let script = String.Join (";", lines)
        //Debug.WriteLine (sprintf "Executing script:\n%s" script)
        //window.Browser.ExecuteJavascript script
        let w, h = window.Size
        window.Size <- w + 100., h + 100.
        do! Async.SwitchToThreadPool ()
        Trace.WriteLine "Window shown"
        do! Async.AwaitEvent window.Closed
        Trace.WriteLine "Window closed. Exiting app function"
    })