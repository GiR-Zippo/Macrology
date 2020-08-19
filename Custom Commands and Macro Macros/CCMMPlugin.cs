using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace CCMM {
    public class CCMMPlugin : IDalamudPlugin {
        private bool disposedValue;

        public string Name => "Custom Commands and Macro Macros";

        public DalamudPluginInterface Interface { get; private set; }
        public GameFunctions Functions { get; private set; }
        public PluginUI Ui { get; private set; }
        public MacroHandler MacroHandler { get; private set; }
        public Configuration Config { get; private set; }
        private Commands Commands { get; set; }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");
            this.Functions = new GameFunctions(this);
            this.Ui = new PluginUI(this);
            this.MacroHandler = new MacroHandler(this);
            this.Config = Configuration.Load(this) ?? new Configuration();
            this.Config.Initialise(this);
            this.Commands = new Commands(this);

            this.Interface.UiBuilder.OnBuildUi += this.Ui.Draw;
            this.Interface.UiBuilder.OnOpenConfigUi += this.Ui.OpenSettings;
            this.Interface.Framework.OnUpdateEvent += this.MacroHandler.OnFrameworkUpdate;
            foreach (KeyValuePair<string, string> entry in Commands.COMMANDS) {
                this.Interface.CommandManager.AddHandler(entry.Key, new CommandInfo(this.Commands.OnCommand) {
                    HelpMessage = entry.Value,
                });
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    this.Interface.UiBuilder.OnBuildUi -= this.Ui.Draw;
                    this.Interface.UiBuilder.OnOpenConfigUi -= this.Ui.OpenSettings;
                    this.Interface.Framework.OnUpdateEvent -= this.MacroHandler.OnFrameworkUpdate;
                    foreach (string command in Commands.COMMANDS.Keys) {
                        this.Interface.CommandManager.RemoveHandler(command);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
