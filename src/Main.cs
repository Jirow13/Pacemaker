﻿using System.Collections.Generic;
using System.ComponentModel;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Pacemaker
{
    public class Main : MBSubModuleBase
    {
        /* Semantic Versioning (https://semver.org): */
        public static readonly int SemVerMajor = 1;
        public static readonly int SemVerMinor = 1;
        public static readonly int SemVerPatch = 7;
        public static readonly string? SemVerSpecial = "beta1";
        private static readonly string SemVerEnd = (SemVerSpecial is not null) ? "-" + SemVerSpecial : string.Empty;
        public static readonly string Version = $"{SemVerMajor}.{SemVerMinor}.{SemVerPatch}{SemVerEnd}";

        public static readonly string Name = typeof(Main).Namespace;
        public static readonly string DisplayName = Name; // to be shown to humans in-game
        public static readonly string HarmonyDomain = "com.zijistark.bannerlord." + Name.ToLower();

        internal static readonly Color ImportantTextColor = Color.FromUint(0x00F16D26); // orange

        internal static Settings? Settings;
        internal static TimeParams TimeParam = new();
        internal static ExternalSavedValues ExternalSavedValues = new(Name);

        private readonly bool EnableTickTracer = false;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            new Harmony(HarmonyDomain).PatchAll();
            Util.EnableLog = true; // enable various debug logging
            Util.EnableTracer = true; // enable code event tracing (requires enabled logging)
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            var trace = new List<string>();

            if (_loaded)
                trace.Add("Module was already loaded.");
            else
                trace.Add("Module is loading for the first time...");

            if (Settings.Instance is not null && Settings.Instance != Settings)
            {
                Settings = Settings.Instance;

                // register for settings property-changed events
                Settings.PropertyChanged += Settings_OnPropertyChanged;

                trace.Add("\nLoaded Settings:");
                trace.AddRange(Settings.ToStringLines(indentSize: 4));
                trace.Add(string.Empty);

                SetTimeParams(new TimeParams(Settings.DaysPerSeason), trace);
            }

            if (!_loaded)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Loaded {DisplayName}", ImportantTextColor));
                _loaded = true;
            }

            Util.Log.ToFile(trace);
        }

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);
            var trace = new List<string>();

            if (game.GameType is Campaign)
            {
                var initializer = (CampaignGameStarter)starterObject;
                AddBehaviors(initializer, trace);
            }

            Util.EventTracer.Trace(trace);
        }

        private void AddBehaviors(CampaignGameStarter gameInitializer, List<string> trace)
        {
            gameInitializer.AddBehavior(new SaveBehavior());
            trace.Add($"Behavior added: {typeof(SaveBehavior).FullName}");

            gameInitializer.AddBehavior(new FastAgingBehavior());
            trace.Add($"Behavior added: {typeof(FastAgingBehavior).FullName}");

            if (EnableTickTracer && Util.EnableTracer && Util.EnableLog)
            {
                gameInitializer.AddBehavior(new TickTraceBehavior());
                trace.Add($"Behavior added: {typeof(TickTraceBehavior).FullName}");
            }
        }

        internal static TimeParams SetTimeParams(TimeParams newParams, List<string> trace)
        {
            trace.Add($"Setting time parameters for {newParams.DayPerSeason} days/season...");

            var oldParams = TimeParam;
            TimeParam = newParams;

            trace.Add(string.Empty);
            trace.AddRange(TimeParam.ToStringLines(indentSize: 4));

            return oldParams;
        }

        protected static void Settings_OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (sender is Settings settings && args.PropertyName == Settings.SaveTriggered)
            {
                var trace = new List<string> { "Received save-triggered event from Settings..." };
                trace.Add(string.Empty);
                trace.Add("New Settings:");
                trace.AddRange(settings.ToStringLines(indentSize: 4));
                trace.Add(string.Empty);
                SetTimeParams(new TimeParams(settings.DaysPerSeason), trace);
                Util.EventTracer.Trace(trace);
            }
        }

        private bool _loaded;
    }
}
