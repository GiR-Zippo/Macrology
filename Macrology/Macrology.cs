using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;

namespace Macrology {
    public class Macrology : IDalamudPlugin {
        private bool _disposedValue;

        public string Name => "Macrology";

        public DalamudPluginInterface Interface { get; private set; } = null!;
        public GameFunctions Functions { get; private set; } = null!;
        public PluginUi Ui { get; private set; } = null!;
        public MacroHandler MacroHandler { get; private set; } = null!;
        public Configuration Config { get; private set; } = null!;
        private Commands Commands { get; set; } = null!;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");
            this.Functions = new GameFunctions(this);
            this.Ui = new PluginUi(this);
            this.MacroHandler = new MacroHandler(this);
            this.Config = Configuration.Load(this) ?? new Configuration();
            this.Config.Initialise(this);
            this.Commands = new Commands(this);

            this.Interface.UiBuilder.OnBuildUi += this.Ui.Draw;
            this.Interface.UiBuilder.OnOpenConfigUi += this.Ui.OpenSettings;
            this.Interface.Framework.OnUpdateEvent += this.MacroHandler.OnFrameworkUpdate;
            this.Interface.ClientState.OnLogin += this.MacroHandler.OnLogin;
            this.Interface.ClientState.OnLogout += this.MacroHandler.OnLogout;
            foreach (var entry in Commands.Descriptions) {
                this.Interface.CommandManager.AddHandler(entry.Key, new CommandInfo(this.Commands.OnCommand) {
                    HelpMessage = entry.Value,
                });
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (this._disposedValue) {
                return;
            }

            if (disposing) {
                this.Interface.UiBuilder.OnBuildUi -= this.Ui.Draw;
                this.Interface.UiBuilder.OnOpenConfigUi -= this.Ui.OpenSettings;
                this.Interface.Framework.OnUpdateEvent -= this.MacroHandler.OnFrameworkUpdate;
                this.Interface.ClientState.OnLogin -= this.MacroHandler.OnLogin;
                this.Interface.ClientState.OnLogout -= this.MacroHandler.OnLogout;
                foreach (var command in Commands.Descriptions.Keys) {
                    this.Interface.CommandManager.RemoveHandler(command);
                }
            }

            this._disposedValue = true;
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
