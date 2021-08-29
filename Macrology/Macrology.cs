using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using XivCommon;

namespace Macrology {
    public class Macrology : IDalamudPlugin {
        private bool _disposedValue;

        public string Name => "Macrology";

        [PluginService]
        internal DalamudPluginInterface Interface { get; private init; } = null!;

        [PluginService]
        internal ChatGui ChatGui { get; private init; } = null!;

        [PluginService]
        internal ClientState ClientState { get; private init; } = null!;

        [PluginService]
        internal CommandManager CommandManager { get; private init; } = null!;

        [PluginService]
        internal Framework Framework { get; private init; } = null!;

        public XivCommonBase Common { get; }
        public PluginUi Ui { get; }
        public MacroHandler MacroHandler { get; }
        public Configuration Config { get; }
        private Commands Commands { get; }

        public Macrology() {
            this.Common = new XivCommonBase();
            this.Ui = new PluginUi(this);
            this.MacroHandler = new MacroHandler(this);
            this.Config = Configuration.Load(this) ?? new Configuration();
            this.Config.Initialise(this);
            this.Commands = new Commands(this);

            this.Interface.UiBuilder.Draw += this.Ui.Draw;
            this.Interface.UiBuilder.OpenConfigUi += this.Ui.OpenSettings;
            this.Framework.Update += this.MacroHandler.OnFrameworkUpdate;
            this.ClientState.Login += this.MacroHandler.OnLogin;
            this.ClientState.Logout += this.MacroHandler.OnLogout;
            foreach (var (name, desc) in Commands.Descriptions) {
                this.CommandManager.AddHandler(name, new CommandInfo(this.Commands.OnCommand) {
                    HelpMessage = desc,
                });
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (this._disposedValue) {
                return;
            }

            if (disposing) {
                this.Interface.UiBuilder.Draw -= this.Ui.Draw;
                this.Interface.UiBuilder.OpenConfigUi -= this.Ui.OpenSettings;
                this.Framework.Update -= this.MacroHandler.OnFrameworkUpdate;
                this.ClientState.Login -= this.MacroHandler.OnLogin;
                this.ClientState.Logout -= this.MacroHandler.OnLogout;
                foreach (var command in Commands.Descriptions.Keys) {
                    this.CommandManager.RemoveHandler(command);
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
