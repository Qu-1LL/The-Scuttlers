---
tags:
  - trilobites/csharp
  - trilobites/csharp/reference
  - trilobites/csharp/external-docs
type: reference
area: external-docs
aliases:
  - External Docs
  - MonoGame and Gum Docs
---
# External Docs - MonoGame and Gum

This note collects the main external documentation links that are useful when working on the C# version of Trilobites. These are not replacements for the local vault notes. Instead, they are official framework references to use when you need deeper detail on the APIs and concepts behind [[Boot and Game Root]], [[Rendering]], [[UI and Input]], and [[Build Content and Packaging]].

## MonoGame official docs

### Core framework and lifecycle

- [MonoGame API reference](https://docs.monogame.net/api/)
- [Game class](https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html)

Use these when working on:

- [[Boot and Game Root]]
- [[Runtime Flow]]
- [[Rendering]]

### Input

- [Microsoft.Xna.Framework.Input namespace](https://docs.monogame.net/api/Microsoft.Xna.Framework.Input.html)

Use this when working on:

- [[UI and Input]]
- [[Controls and Shortcuts]]

### Content and MGCB

- [What is the Content Pipeline?](https://docs.monogame.net/articles/getting_to_know/whatis/content_pipeline/index.html)
- [What Is Content?](https://docs.monogame.net/articles/getting_to_know/whatis/content_pipeline/CP_Overview.html)
- [MonoGame Content Builder (MGCB)](https://docs.monogame.net/articles/getting_started/tools/mgcb.html)

Use these when working on:

- [[Build Content and Packaging]]
- [[Audio]]
- [[Rendering]]

## Gum official docs

### Gum with MonoGame

- [Gum MonoGame overview](https://docs.flatredball.com/gum/code/monogame)
- [GumService (GumUI)](https://docs.flatredball.com/gum/code/gum-code-reference/gumservice-gumui)

Use these when working on:

- [[UI and Input]]
- [[Rendering]]
- [[Build Content and Packaging]]

### Gum shapes and retained UI chrome

- [Shapes (Apos.Shapes)](https://docs.flatredball.com/gum/code/monogame/shapes-apos.shapes)
- [GumBatch](https://docs.flatredball.com/gum/code/monogame/gumbatch)

Use these when working on:

- [[Rendering]]
- [[UI and Input]]

### Gum controls

- [CheckBox](https://docs.flatredball.com/gum/code/monogame/gum-forms/controls/checkbox)
- [Forms Controls tutorial](https://docs.flatredball.com/gum/code/monogame/tutorials/code-only-gum-forms-tutorial/forms-controls)

Use these when working on:

- [[UI and Input]]
- [[Features Overview]]

## How to use this note

- Start with the local vault note first so you understand how Trilobites currently uses a system.
- Use the external docs when you need official API behavior, setup details, or framework examples.
- Prefer the local notes for project-specific behavior and the official docs for framework semantics.

## Related notes

- [[Trilobites CSharp Home]]
- [[Build Content and Packaging]]
- [[UI and Input]]
- [[Rendering]]
