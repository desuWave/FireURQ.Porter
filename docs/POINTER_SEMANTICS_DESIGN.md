# Delphi Pointer Semantics — Design Baseline

Status: investigation baseline; implementation is not complete.

## Scope

This document records the pointer-semantics gap exposed by the generated
`MD5.cs` from the FireURQ source tree. It is a design checkpoint, not a claim
that pointer translation is already correct.

## Source roots used for the local full-project verification

- `E:\EVI_DATA\EVI_Projects\fireurq-master`
- `E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4`

The generated MD5 sample came from
`fireurq-master\MD5.pas`.

## Observed source constructs

The MD5 unit uses typed pointer aliases such as:

- `PArray4UINT4 = ^TArray4UINT4`
- `PByteArray = ^TByteArray`
- `PUINT4Array = ^TUINT4Array`
- `PMD5Context = ^TMD5Context`

It also uses address expressions for locals, fields, array elements and
parameters, for example:

- `@x`
- `@Context.state`
- `@Context.buffer`
- `@Input[i]`

Pointer dereference and indexing are used to read and write through these
aliases.

## Current implementation gap

The current declaration translator maps Pascal pointer aliases to
`IntPtr`. The expression translator still emits a TODO marker for address-of
in general cases. Dereference currently translates to its operand, which
drops the memory-access operation. Indexing a pointer alias is emitted as
ordinary C# indexing, which is not valid for `IntPtr`.

Therefore, the current output is a useful translation artifact but is not yet
a semantically correct or necessarily compilable translation of pointer-heavy
units such as MD5.

## Required semantic model

The implementation must preserve these distinct operations:

1. A pointer type retains the type of its pointee.
2. Address-of obtains the address of an lvalue; it must not be treated as a
   textual cast or silently removed.
3. Dereference reads or writes the pointee; it must not be erased.
4. Pointer indexing advances by the size of the pointee type, not by one byte
   unconditionally.
5. Address expressions must distinguish locals, fields, array elements and
   already-indirect expressions.
6. `nil`, equality/inequality and `Assigned` must use the same pointer
   representation consistently.
7. Any generated unsafe code must be explicitly marked and must respect C#
   pinning/lifetime rules for managed arrays and objects.

## Investigation and implementation order

1. Trace pointer declarations through parsing, semantic analysis and
   declaration translation.
2. Record pointee types and pointer indirection explicitly in semantic data;
   do not infer pointer-ness only from a name beginning with `P`.
3. Define the generated C# representation and lifetime strategy before
   changing expression output.
4. Implement and test address-of for identifiers, fields and indexed
   elements.
5. Implement dereference for both reads and assignment targets.
6. Implement typed pointer indexing and pointer casts.
7. Re-run the MD5 translation and compile a focused generated-code fixture.
8. Re-run the 58-file project translation and report translation success
   separately from generated-C# compilation and runtime correctness.

## Acceptance examples

The following source forms must be represented without TODO placeholders and
without losing their memory semantics:

```pascal
P := nil;
if Assigned(P) then ...
Value := P^;
P^ := Value;
Call(@LocalValue);
Call(@RecordValue.Field);
Call(@Values[Index]);
Call(PointerToArray[Index]);
```

A passing parser/build smoke test alone does not satisfy these acceptance
criteria.
