#load "references.fsx"

open Axial.Data
open Axial.Schema

type Person = { Name: string; Age: int }

schema<Person> {
    field "name" _.Name
    field "age" _.Age
    construct (fun age name -> { Name = name; Age = age })
}
|> ignore
