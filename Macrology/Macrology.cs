using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using Dalamud.IoC;
using XivCommon;
using Dalamud.Plugin.Services;
using Macrology.Common.Functions;

namespace Macrology
{
    public class Macrology : IDalamudPlugin
    {
        private bool _disposedValue;

        public string Name => "Macrology";

        [PluginService]
        internal IDalamudPluginInterface Interface { get; private init; } = null!;

        [PluginService]
        internal IChatGui ChatGui { get; private init; } = null!;

        [PluginService]
        internal IClientState ClientState { get; private init; } = null!;

        [PluginService]
        internal ICommandManager CommandManager { get; private init; } = null!;

        [PluginService]
        internal IFramework Framework { get; private init; } = null;

        [PluginService]
        internal IPluginLog PluginLog { get; private init; } = null;

        //public XivCommonBase Common { get; }

        //Remove
        public Api api { get; }

        public PluginUi Ui { get; }
        public MacroHandler MacroHandler { get; }
        public Configuration Config { get; }
        private Commands Commands { get; }

        public Macrology(IDalamudPluginInterface pluginInterface, IClientState clientState, IFramework framework, ICommandManager commandManager, IPluginLog pluginLog)
        {
            Interface = pluginInterface;
            ClientState = clientState;
            Framework = framework;
            CommandManager = commandManager;
            PluginLog = pluginLog;

            //Common = new XivCommonBase(pluginInterface);

            //Until XivCommonBase is up again
            api = pluginInterface.Create<Api>();

            Ui = new PluginUi(this);
            MacroHandler = new MacroHandler(this);
            Config = Configuration.Load(this) ?? new Configuration();
            Config.Initialise(this);
            Commands = new Commands(this);

            Interface.UiBuilder.Draw += Ui.Draw;
            Interface.UiBuilder.OpenMainUi += Ui.OpenSettings;
            Interface.UiBuilder.OpenConfigUi += Ui.OpenSettings;
            Framework.Update += MacroHandler.OnFrameworkUpdate;
            ClientState.Login += MacroHandler.OnLogin;
            ClientState.Logout += MacroHandler.OnLogout;
            foreach (var (name, desc) in Commands.Descriptions)
            {
                CommandManager.AddHandler(name, new CommandInfo(Commands.OnCommand)
                {
                    HelpMessage = desc,
                });
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                Interface.UiBuilder.Draw -= Ui.Draw;
                Interface.UiBuilder.OpenMainUi += Ui.OpenSettings;
                Interface.UiBuilder.OpenConfigUi -= Ui.OpenSettings;
                Framework.Update -= MacroHandler.OnFrameworkUpdate;
                ClientState.Login -= MacroHandler.OnLogin;
                ClientState.Logout -= MacroHandler.OnLogout;
                foreach (var command in Commands.Descriptions.Keys)
                {
                    CommandManager.RemoveHandler(command);
                }
            }
            _disposedValue = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
