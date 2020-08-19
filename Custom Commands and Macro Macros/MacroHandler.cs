using Dalamud.Game.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CCMM {
    public class MacroHandler {
        private readonly static Regex WAIT = new Regex(@"<wait\.(\d+(?:\.\d+)?)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly CCMMPlugin plugin;
        private readonly Channel<string> commands = Channel.CreateUnbounded<string>();
        public ConcurrentDictionary<Guid, Macro> Running { get; } = new ConcurrentDictionary<Guid, Macro>();
        private readonly ConcurrentDictionary<Guid, bool> cancelled = new ConcurrentDictionary<Guid, bool>();
        private readonly ConcurrentDictionary<Guid, bool> paused = new ConcurrentDictionary<Guid, bool>();

        public MacroHandler(CCMMPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "CCMMPlugin cannot be null");
        }

        private static string[] ExtractCommands(string macro) {
            return macro.Split('\n')
                .Where(line => !line.Trim().StartsWith("#"))
                .ToArray();
        }

        public Guid SpawnMacro(Macro macro) {
            string[] commands = ExtractCommands(macro.Contents);
            Guid id = Guid.NewGuid();
            if (commands.Length == 0) {
                // pretend we spawned a task, but actually don't
                return id;
            }
            this.Running.TryAdd(id, macro);
            Task.Run(async () => {
                int i = 0;
                do {
                    if (this.cancelled.TryRemove(id, out bool cancel) && cancel) {
                        break;
                    }
                    if (this.paused.TryGetValue(id, out bool paused) && paused) {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }
                    string command = commands[i];
                    TimeSpan? wait = this.ExtractWait(ref command);
                    if (command == "/loop") {
                        i = -1;
                    } else {
                        await this.commands.Writer.WriteAsync(command);
                    }
                    if (wait != null) {
                        await Task.Delay((TimeSpan)wait);
                    }
                    i += 1;
                } while (i < commands.Length);
                this.Running.TryRemove(id, out Macro  _);
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

            this.cancelled.TryAdd(id, true);
        }

        public void PauseMacro(Guid id) {
            this.paused.TryAdd(id, true);
        }

        public void ResumeMacro(Guid id) {
            this.paused.TryRemove(id, out _);
        }

        public bool IsPaused(Guid id) {
            this.paused.TryGetValue(id, out bool paused);
            return paused;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "delegate")]
        public void OnFrameworkUpdate(Framework framework) {
            if (!this.commands.Reader.TryRead(out string command)) {
                return;
            }

            this.plugin.Functions.ProcessChatBox(command);
        }

        private TimeSpan? ExtractWait(ref string command) {
            MatchCollection matches = WAIT.Matches(command);
            if (matches.Count == 0) {
                return null;
            }

            Match match = matches[matches.Count - 1];
            string waitTime = match.Groups[1].Captures[0].Value;

            if (double.TryParse(waitTime, out double seconds)) {
                command = WAIT.Replace(command, "");
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }
    }
}
