﻿module internal System.Text.Json.Serialization.Helpers

open System
open System.Text.Json
open FSharp.Reflection

let fail expected (reader: byref<Utf8JsonReader>) (ty: Type) =
    sprintf "Failed to parse type %s: expected %s, found %A"
        ty.FullName expected reader.TokenType
    |> JsonException
    |> raise

let expectAlreadyRead expectedTokenType expectedLabel (reader: byref<Utf8JsonReader>) ty =
    if reader.TokenType <> expectedTokenType then
        fail expectedLabel &reader ty

let readExpecting expectedTokenType expectedLabel (reader: byref<Utf8JsonReader>) ty =
    if not (reader.Read()) || reader.TokenType <> expectedTokenType then
        fail expectedLabel &reader ty

let readExpectingPropertyNamed (expectedPropertyName: string) (reader: byref<Utf8JsonReader>) ty =
    if not (reader.Read()) || reader.TokenType <> JsonTokenType.PropertyName || not (reader.ValueTextEquals expectedPropertyName) then
        fail ("\"" + expectedPropertyName + "\"") &reader ty

let isNullableUnion (ty: Type) =
    ty.GetCustomAttributes(typeof<CompilationRepresentationAttribute>, false)
    |> Array.exists (fun x ->
        let x = (x :?> CompilationRepresentationAttribute)
        x.Flags.HasFlag(CompilationRepresentationFlags.UseNullAsTrueValue))

let isSkippableType (ty: Type) =
    ty.IsGenericType
    && ty.GetGenericTypeDefinition() = typedefof<Skippable<_>>

let isSkip (ty: Type) =
    if isSkippableType ty then
        let getTag = FSharpValue.PreComputeUnionTagReader(ty)
        fun x -> getTag x = 0
    else
        fun _ -> false

let rec isNullableFieldType (fsOptions: JsonFSharpOptions) (ty: Type) =
    fsOptions.AllowNullFields
    || isNullableUnion ty
    || (fsOptions.UnionEncoding.HasFlag JsonUnionEncoding.UnwrapOption
        && ty.IsGenericType
        && ty.GetGenericTypeDefinition() = typedefof<voption<_>>)
    || (isSkippableType ty && isNullableFieldType fsOptions (ty.GetGenericArguments().[0]))

let isSkippableFieldType (fsOptions: JsonFSharpOptions) (ty: Type) =
    isNullableFieldType fsOptions ty
    || isSkippableType ty
