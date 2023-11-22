using System;
using System.Collections.Generic;
using System.Linq;

namespace Macrology
{
    public class Commands
    {
        private Macrology Plugin { get; }

        public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            ["/mmacros"] = "Open the Macrology interface",
            ["/pmacrology"] = "Alias for /mmacros",
            ["/macrology"] = "Alias for /mmacros",
            ["/mmacro"] = "Execute a Macrology macro",
            ["/mmcancel"] = "Cancel the first Macrology macro of a given type or all if \"all\" is passed",
        };

        public Commands(Macrology plugin)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Macrology cannot be null");
        }

        public void OnCommand(string command, string args)
        {
            switch (command)
            {
                case "/mmacros":
                case "/pmacrology":
                case "/macrology":
                    OnMainCommand();
                    break;
                case "/mmacro":
                    OnMacroCommand(args);
                    break;
                case "/mmcancel":
                    OnMacroCancelCommand(args);
                    break;
                default:
                    Plugin.ChatGui.PrintError($"The command {command} was passed to Macrology, but there is no handler available.");
                    break;
            }
        }

        private void OnMainCommand()
        {
            Plugin.Ui.SettingsVisible = !Plugin.Ui.SettingsVisible;
        }

        private void OnMacroCommand(string args)
        {
            var first = args.Trim().Split(' ').FirstOrDefault() ?? "";
            if (!Guid.TryParse(first, out var id))
            {
                Plugin.ChatGui.PrintError("First argument must be the UUID of the macro to execute.");
                return;
            }

            var macro = Plugin.Config.FindMacro(id);
            if (macro == null)
            {
                Plugin.ChatGui.PrintError($"No macro with ID {id} found.");
                return;
            }

            Plugin.MacroHandler.SpawnMacro(macro);
        }

        private void OnMacroCancelCommand(string args)
        {
            var first = args.Trim().Split(' ').FirstOrDefault() ?? "";
            if (first == "all")
            {
                foreach (var running in Plugin.MacroHandler.Running.Keys)
                    Plugin.MacroHandler.CancelMacro(running);

                return;
            }

            if (!Guid.TryParse(first, out var id))
            {
                Plugin.ChatGui.PrintError("First argument must either be \"all\" or the UUID of the macro to cancel.");
                return;
            }

            var macro = Plugin.Config.FindMacro(id);
            if (macro == null)
            {
                Plugin.ChatGui.PrintError($"No macro with ID {id} found.");
                return;
            }

            var entry = Plugin.MacroHandler.Running.FirstOrDefault(e => e.Value?.Id == id);
            if (entry.Value == null)
            {
                Plugin.ChatGui.PrintError("That macro is not running.");
                return;
            }

            Plugin.MacroHandler.CancelMacro(entry.Key);
        }
    }
}
