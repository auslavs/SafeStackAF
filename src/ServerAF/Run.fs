module ServerAF

open Giraffe
open FSharp.Control.Tasks.V2
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open System.Threading.Tasks
open Shared

open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

type Storage () =
    let todos = ResizeArray<_>()

    member __.GetTodos () =
        List.ofSeq todos

    member __.AddTodo (todo: Todo) =
        if Todo.isValid todo.Description then
            todos.Add todo
            Ok ()
        else Error "Invalid todo"

let storage = Storage()

storage.AddTodo(Todo.create "Create new SAFE project") |> ignore
storage.AddTodo(Todo.create "Write your app") |> ignore
storage.AddTodo(Todo.create "Ship it !!!") |> ignore

let todosApi =
    { getTodos = fun () -> async { return storage.GetTodos() }
      addTodo =
        fun todo -> async {
            match storage.AddTodo todo with
            | Ok () -> return todo
            | Error e -> return failwith e
        } }

let errorHandler (ex : exn) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse
    >=> ServerErrors.INTERNAL_ERROR ex.Message

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue todosApi
    |> Remoting.buildHttpHandler

[<FunctionName("Run")>]

let run ([<HttpTrigger (AuthorizationLevel.Anonymous, Route = "{*any}")>] req : HttpRequest, context : ExecutionContext, log : ILogger) =

    // let hostingEnvironment = req.HttpContext.GetHostingEnvironment()
    // hostingEnvironment.ContentRootPath <- context.FunctionAppDirectory

    let func = Some >> Task.FromResult
    { new Microsoft.AspNetCore.Mvc.IActionResult with
        member _.ExecuteResultAsync(ctx) =
          task {
            try
              return! webApp func ctx.HttpContext :> Task
            with exn ->
              return! errorHandler exn log func ctx.HttpContext :> Task
          }
          :> Task }