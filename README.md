# PSCopilot
Use **Copilot** to suggest commands when using Powershell.

This is a project just for fun, don't take it seriously🤣

![PSCopilot](https://github.com/UlyssesWu/PSCopilot/blob/main/img/PSCopilot_1.png)

## Requirements
1. Powershell 7.2+ with **PSReadLine** module installed (usually by default).
2. A valid Copilot subscription.

## Use
1. Build the project.
2. Open Powershell in Windows Terminal. 
  `Import-Module {path to PSCopilot.dll}`.
3. `Request-Copilot`, and finish the verification.
4. Now you can use PSCopilot. Press <kbd>F2</kbd> *repeatedly* to get suggestions.

You can use this pattern to *suggest* Copilot:

`echo "shutdown the computer in 60s"; ` (now Press <kbd>F2</kbd>)

(Maybe `Write-Output` is better than `echo`.)

(Optional) You can also copy all files to `$PSHOME\Modules\PSCopilot` and use `Import-Module PSCopilot` to launch PSCopilot.

![PSCopilot_demo](https://github.com/UlyssesWu/PSCopilot/blob/main/img/PSCopilot_2.png)

## Debug
1. Build the project with `Debug` configuration.
2. Open Powershell in Windows Terminal. 
  `Import-Module {path to PSCopilot.dll}`.
3. `Set-Copilot -dbg`. Debugger Attach dialog will pop up.

## Credits
This project is using [copilotplayground/CopilotDev.NET](https://github.com/copilotplayground/CopilotDev.NET) (license: MIT). Much thanks to the author **TheFortification**.

## FAQ

### Why do I have to Press <kbd>F2</kbd> repeatedly like a psycho?
PSReadLine Predictor mechanism does **not** support async suggestion. We have to return suggestions immediately. When you input or press <kbd>F2</kbd>, a query will be send to Copilot. The returned suggestion will be kept, and will be displayed when you press <kbd>F2</kbd> again.

### There are many useless suggestions.
Yes, it is silly🤡

To clear input history, use both <kbd>Alt</kbd> + <kbd>F7</kbd> and `Clear-Copilot` command.

---
by Ulysses (wdwxy12345{at}gmail.com)