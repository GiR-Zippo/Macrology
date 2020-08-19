# Custom Commands and Macro Macros

**This plugin is still under heavy development and is not
stable. Proceed with caution.**

## Description

This command allows you to create custom commands (not yet
implemented) and unrestricted macros.

**Custom commands** are commands that run a specific macro. For
example, you could bind `/h` to `/tp Estate Hall (Free Company)`, or
even multiple commands in sequence. In order to do this, you need...

**Macro macros** are like normal macros but different. Unlike normal
macros, you can't assign them to hotbar buttons (yet?), but you can
execute them using commands (and custom commands), run multiple at the
same time, pause them, make them loop, use `<wait.X>` with fractional
seconds, and, of course, make them as long as you please (the *macro*
part of macro macros).

Lastly, you can also organise macros into folders (and nest folders)
for easier access.

## Commands

- `/ccmm` - opens the main interface
- `/mmacro <uuid>` - executes the macro with the given UUID
- `/mmcancel <all|uuid>` - either cancels all currently-running macros
  or cancels the first instance of the macro represented by the given
  UUID

## Special macro properties

### Looping

Macro macros can loop by using the `/loop` command. Whenever this
command is encountered in a macro, it will start the macro over from
the beginning.

### Pausing

In the main interface, there is a list of currently-running macros. If
you click on one and click "Pause" below, it will pause the
macro. Clicking "Resume" after will resume it from where it left off.

### Parallel macros

You can run more than one macro macro at the same time. This is an
inherent feature and you don't have to do anything to enable it. Just
run two or more macros, and they'll run in parallel.

### Fractional `<wait.#>`

When using `<wait.#>` in a macro, you can use decimal points, like
`<wait.2.5>`.
