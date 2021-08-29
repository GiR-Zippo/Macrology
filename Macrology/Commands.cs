using System;
using System.Collections.Generic;
using System.Linq;

namespace Macrology {
    public class Commands {
        private Macrology Plugin { get; }

        public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string> {
            ["/mmacros"] = "Open the Macrology interface",
            ["/pmacrology"] = "Alias for /mmacros",
            ["/macrology"] = "Alias for /mmacros",
            ["/mmacro"] = "Execute a Macrology macro",
            ["/mmcancel"] = "Cancel the first Macrology macro of a given type or all if \"all\" is passed",
        };

        public Commands(Macrology plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Macrology cannot be null");
        }

        public void OnCommand(string command, string args) {
            switch (command) {
                case "/mmacros":
                case "/pmacrology":
                case "/macrology":
                    this.OnMainCommand();
                    break;
                case "/mmacro":
                    this.OnMacroCommand(args);
                    break;
                case "/mmcancel":
                    this.OnMacroCancelCommand(args);
                    break;
                default:
                    this.Plugin.ChatGui.PrintError($"The command {command} was passed to Macrology, but there is no handler available.");
                    break;
            }
        }

        private void OnMainCommand() {
            this.Plugin.Ui.SettingsVisible = !this.Plugin.Ui.SettingsVisible;
        }

        private void OnMacroCommand(string args) {
            var first = args.Trim().Split(' ').FirstOrDefault() ?? "";
            if (!Guid.TryParse(first, out var id)) {
                this.Plugin.ChatGui.PrintError("First argument must be the UUID of the macro to execute.");
                return;
            }

            var macro = this.Plugin.Config.FindMacro(id);
            if (macro == null) {
                this.Plugin.ChatGui.PrintError($"No macro with ID {id} found.");
                return;
            }

            this.Plugin.MacroHandler.SpawnMacro(macro);
        }

        private void OnMacroCancelCommand(string args) {
            var first = args.Trim().Split(' ').FirstOrDefault() ?? "";
            if (first == "all") {
                foreach (var running in this.Plugin.MacroHandler.Running.Keys) {
                    this.Plugin.MacroHandler.CancelMacro(running);
                }

                return;
            }

            if (!Guid.TryParse(first, out var id)) {
                this.Plugin.ChatGui.PrintError("First argument must either be \"all\" or the UUID of the macro to cancel.");
                return;
            }

            var macro = this.Plugin.Config.FindMacro(id);
            if (macro == null) {
                this.Plugin.ChatGui.PrintError($"No macro with ID {id} found.");
                return;
            }

            var entry = this.Plugin.MacroHandler.Running.FirstOrDefault(e => e.Value?.Id == id);
            if (entry.Value == null) {
                this.Plugin.ChatGui.PrintError("That macro is not running.");
                return;
            }

            this.Plugin.MacroHandler.CancelMacro(entry.Key);
        }
    }
}
