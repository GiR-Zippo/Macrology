using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Game;

namespace Macrology {
    public class MacroHandler {
        private bool _ready;
        private static readonly Regex Wait = new(@"<wait\.(\d+(?:\.\d+)?)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] FastCommands = {
            "/ac",
            "/action",
            "/e",
            "/echo",
        };

        private Macrology Plugin { get; }
        private readonly Channel<string> _commands = Channel.CreateUnbounded<string>();
        public ConcurrentDictionary<Guid, Macro?> Running { get; } = new();
        private readonly ConcurrentDictionary<Guid, bool> _cancelled = new();
        private readonly ConcurrentDictionary<Guid, bool> _paused = new();

        public MacroHandler(Macrology plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Macrology cannot be null");
            this._ready = this.Plugin.ClientState.LocalPlayer != null;
        }

        private static string[] ExtractCommands(string macro) {
            return macro.Split('\n')
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .ToArray();
        }

        public Guid SpawnMacro(Macro macro) {
            if (!this._ready) {
                return Guid.Empty;
            }

            var commands = ExtractCommands(macro.Contents);
            var id = Guid.NewGuid();
            if (commands.Length == 0) {
                // pretend we spawned a task, but actually don't
                return id;
            }

            this.Running.TryAdd(id, macro);
            Task.Run(async () => {
                // the default wait
                TimeSpan? defWait = null;
                // keep track of the line we're at in the macro
                var i = 0;
                do {
                    // cancel if requested
                    if (this._cancelled.TryRemove(id, out var cancel) && cancel) {
                        break;
                    }

                    // wait a second instead of executing if paused
                    if (this._paused.TryGetValue(id, out var paused) && paused) {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    // get the line of the command
                    var command = commands[i];
                    // find the amount specified to wait, if any
                    var wait = ExtractWait(ref command) ?? defWait;
                    // go back to the beginning if the command is loop
                    if (command.Trim() == "/loop") {
                        i = 0;
                        continue;
                    }

                    // set default wait
                    if (command.Trim().StartsWith("/defaultwait ")) {
                        var defWaitStr = command.Split(' ')[1];
                        if (double.TryParse(defWaitStr, out var waitTime)) {
                            defWait = TimeSpan.FromSeconds(waitTime);
                        }

                        i += 1;
                        continue;
                    }

                    // send the command to the channel
                    await this._commands.Writer.WriteAsync(command);

                    // wait a minimum amount of time (<wait.0> to bypass)
                    if (FastCommands.Contains(command.Split(' ')[0])) {
                        wait ??= TimeSpan.FromMilliseconds(10);
                    } else {
                        wait ??= TimeSpan.FromMilliseconds(100);
                    }

                    await Task.Delay((TimeSpan) wait);

                    // increment to next line
                    i += 1;
                } while (i < commands.Length);

                this.Running.TryRemove(id, out _);
            });
            return id;
        }

        public bool IsRunning(Guid id) {
            return this.Running.ContainsKey(id);
        }

        public void CancelMacro(Guid id) {
            if (!this.IsRunning(id)) {
                return;
            }

            this._cancelled.TryAdd(id, true);
        }

        public void PauseMacro(Guid id) {
            this._paused.TryAdd(id, true);
        }

        public void ResumeMacro(Guid id) {
            this._paused.TryRemove(id, out _);
        }

        public bool IsPaused(Guid id) {
            this._paused.TryGetValue(id, out var paused);
            return paused;
        }

        public bool IsCancelled(Guid id) {
            this._cancelled.TryGetValue(id, out var cancelled);
            return cancelled;
        }

        public void OnFrameworkUpdate(Framework framework1) {
            // get a message to send, but discard it if we're not ready
            if (!this._commands.Reader.TryRead(out var command) || !this._ready) {
                return;
            }

            // send the message as if it were entered in the chat box
            this.Plugin.Common.Functions.Chat.SendMessage(command);
        }

        private static TimeSpan? ExtractWait(ref string command) {
            var matches = Wait.Matches(command);
            if (matches.Count == 0) {
                return null;
            }

            var match = matches[matches.Count - 1];
            var waitTime = match.Groups[1].Captures[0].Value;

            if (!double.TryParse(waitTime, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds)) {
                return null;
            }

            command = Wait.Replace(command, "");
            return TimeSpan.FromSeconds(seconds);
        }

        internal void OnLogin(object? sender, EventArgs args) {
            this._ready = true;
        }

        internal void OnLogout(object? sender, EventArgs args) {
            this._ready = false;

            foreach (var id in this.Running.Keys) {
                this.CancelMacro(id);
            }
        }
    }
}
