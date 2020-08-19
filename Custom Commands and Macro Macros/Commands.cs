using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCMM {
    public class Commands {
        private readonly CCMMPlugin plugin;

        public static readonly IReadOnlyDictionary<string, string> COMMANDS = new Dictionary<string, string> {
            ["/ccmm"] = "Open the CCMM interface",
            ["/mmacro"] = "Execute a CCMM macro",
            ["/mmcancel"] = "Cancel the first CCMM macro of a given type or all if \"all\" is passed",
        };

        public Commands(CCMMPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "CCMMPlugin cannot be null");
        }

        public void OnCommand(string command, string args) {
            switch (command) {
                case "/ccmm":
                    this.OnMainCommand();
                    break;
                case "/mmacro":
                    this.OnMacroCommand(args);
                    break;
                case "/mmcancel":
                    this.OnMacroCancelCommand(args);
                    break;
                default:
                    this.plugin.Interface.Framework.Gui.Chat.PrintError($"The command {command} was passed to CCMM, but there is no handler available.");
                    break;
            }
        }

        private void OnMainCommand() {
            this.plugin.Ui.SettingsVisible = !this.plugin.Ui.SettingsVisible;
        }

        private void OnMacroCommand(string args) {
            string first = args.Trim().Split(' ').FirstOrDefault() ?? "";
            if (!Guid.TryParse(first, out Guid id)) {
                this.plugin.Interface.Framework.Gui.Chat.PrintError("First argument must be the UUID of the macro to execute.");
                return;
            }
            Macro macro = this.plugin.Config.FindMacro(id);
            if (macro == null) {
                this.plugin.Interface.Framework.Gui.Chat.PrintError($"No macro with ID {id} found.");
                return;
            }
            this.plugin.MacroHandler.SpawnMacro(macro);
        }

        private void OnMacroCancelCommand(string args) {
            string first = args.Trim().Split(' ').FirstOrDefault() ?? "";
            if (first == "all") {
                foreach (Guid running in this.plugin.MacroHandler.Running.Keys) {
                    this.plugin.MacroHandler.CancelMacro(running);
                }
                return;
            }
            if (!Guid.TryParse(first, out Guid id)) {
                this.plugin.Interface.Framework.Gui.Chat.PrintError("First argument must either be \"all\" or the UUID of the macro to cancel.");
                return;
            }
            Macro macro = this.plugin.Config.FindMacro(id);
            if (macro == null) {
                this.plugin.Interface.Framework.Gui.Chat.PrintError($"No macro with ID {id} found.");
                return;
            }
            KeyValuePair<Guid, Macro> entry = this.plugin.MacroHandler.Running.FirstOrDefault(e => e.Value.Id == id);
            if (entry.Value == null) {
                this.plugin.Interface.Framework.Gui.Chat.PrintError($"That macro is not running.");
                return;
            }
            this.plugin.MacroHandler.CancelMacro(entry.Key);
        }
    }
}
