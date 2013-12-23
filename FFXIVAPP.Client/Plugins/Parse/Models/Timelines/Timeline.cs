﻿// FFXIVAPP.Client
// Timeline.cs
// 
// © 2013 Ryan Wilson

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using FFXIVAPP.Client.Plugins.Parse.Enums;
using FFXIVAPP.Client.Plugins.Parse.Models.Fights;
using FFXIVAPP.Client.Plugins.Parse.Models.LinkedStats;
using FFXIVAPP.Client.Plugins.Parse.Models.StatGroups;
using FFXIVAPP.Client.Plugins.Parse.Models.Stats;
using FFXIVAPP.Client.SettingsProviders.Parse;
using FFXIVAPP.Common.Utilities;
using NLog;
using SmartAssembly.Attributes;

namespace FFXIVAPP.Client.Plugins.Parse.Models.Timelines
{
    [DoNotObfuscate]
    public sealed class Timeline : INotifyPropertyChanged
    {
        #region Property Bindings

        private ParseControl _controller;
        private bool _deathFound;
        private bool _fightingRightNow;
        private FightList _fights;
        private string _lastEngaged;
        private string _lastKilled;

        public bool FightingRightNow
        {
            get { return _fightingRightNow; }
            set
            {
                _fightingRightNow = value;
                RaisePropertyChanged();
            }
        }

        public bool DeathFound
        {
            get { return _deathFound; }
            private set
            {
                _deathFound = value;
                RaisePropertyChanged();
            }
        }

        public string LastKilled
        {
            get { return _lastKilled; }
            set
            {
                _lastKilled = value;
                RaisePropertyChanged();
            }
        }

        public string LastEngaged
        {
            get { return _lastEngaged; }
            set
            {
                _lastEngaged = value;
                RaisePropertyChanged();
            }
        }

        public FightList Fights
        {
            get { return _fights; }
            internal set
            {
                _fights = value;
                RaisePropertyChanged();
            }
        }

        private ParseControl Controller
        {
            get { return _controller; }
            set
            {
                _controller = value;
                RaisePropertyChanged();
            }
        }

        #region Party

        private StatGroup _monster;
        private StatGroup _overall;
        private StatGroup _party;

        public StatGroup Overall
        {
            get { return _overall; }
            internal set
            {
                _overall = value;
                RaisePropertyChanged();
            }
        }

        public StatGroup Party
        {
            get { return _party; }
            internal set
            {
                _party = value;
                RaisePropertyChanged();
            }
        }

        public StatGroup Monster
        {
            get { return _monster; }
            internal set
            {
                _monster = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #endregion

        #region Implementation of TimelineChangedEvent

        public event EventHandler<TimelineChangedEvent> TimelineChangedEvent = delegate { };

        private void RaiseTimelineChangedEvent(object sender, TimelineChangedEvent e)
        {
            TimelineChangedEvent(sender, e);
        }

        #endregion

        #region Declarations

        public readonly Timer FightingTimer = new Timer(2500);
        public readonly Timer StoreHistoryTimer = new Timer(5000);

        #endregion

        /// <summary>
        /// </summary>
        public Timeline(ParseControl parseControl)
        {
            Controller = parseControl;
            FightingRightNow = false;
            DeathFound = false;
            Fights = new FightList();
            // setup party
            Overall = new StatGroup("Overall");
            Party = new StatGroup("Party")
            {
                IncludeSelf = false
            };
            Monster = new StatGroup("Monster")
            {
                IncludeSelf = false
            };
            SetStoreHistoryInterval();
            StoreHistoryTimer.Elapsed += StoreHistoryTimerOnElapsed;
            FightingTimer.Elapsed += FightingTimerOnElapsed;
            InitStats();
        }

        private void FightingTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            FightingRightNow = false;
            FightingTimer.Stop();
        }

        private void SetStoreHistoryInterval()
        {
            try
            {
                double interval;
                double.TryParse(Settings.Default.StoreHistoryInterval, out interval);
                StoreHistoryTimer.Interval = interval;
            }
            catch (Exception ex)
            {
            }
        }

        private void StoreHistoryTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            SetStoreHistoryInterval();
            if (Settings.Default.EnableStoreHistoryReset)
            {
                if (!FightingRightNow && !Controller.IsHistoryBased)
                {
                    Controller.Reset();
                }
            }
            StoreHistoryTimer.Stop();
        }

        /// <summary>
        /// </summary>
        /// <param name="monsterName"> </param>
        /// <param name="type"></param>
        /// <returns> </returns>
        public Monster GetSetMonster(string monsterName, TimelineType type)
        {
            StatGroup statGroup = null;
            switch (type)
            {
                case TimelineType.You:
                case TimelineType.Party:
                    if (Monster.TryGetGroup(monsterName, out statGroup))
                    {
                        return (Monster) statGroup;
                    }
                    statGroup = new Monster(monsterName, Controller);
                    ((Monster) statGroup).Type = type;
                    Monster.AddGroup(statGroup);
                    break;
                case TimelineType.Alliance:
                    break;
            }
            Logging.Log(LogManager.GetCurrentClassLogger(), String.Format("StatEvent : Adding new stat group for monster : {0}", monsterName));
            return (Monster) statGroup;
        }

        /// <summary>
        /// </summary>
        /// <param name="playerName"> </param>
        /// <param name="type"></param>
        /// <returns> </returns>
        public Player GetSetPlayer(string playerName, TimelineType type)
        {
            StatGroup statGroup = null;
            switch (type)
            {
                case TimelineType.You:
                case TimelineType.Party:
                    if (Party.TryGetGroup(playerName, out statGroup))
                    {
                        return (Player) statGroup;
                    }
                    statGroup = new Player(playerName, Controller);
                    ((Player) statGroup).Type = type;
                    Party.AddGroup(statGroup);
                    break;
                case TimelineType.Alliance:
                    break;
            }
            Logging.Log(LogManager.GetCurrentClassLogger(), String.Format("StatEvent : Adding new stat group for player : {0}", playerName));
            return (Player) statGroup;
        }

        /// <summary>
        /// </summary>
        /// <param name="eventType"> </param>
        /// <param name="eventArgs"> </param>
        public void PublishTimelineEvent(TimelineEventType eventType, params object[] eventArgs)
        {
            var args = (eventArgs != null && eventArgs.Any()) ? eventArgs[0] : "(no args)";
            Logging.Log(LogManager.GetCurrentClassLogger(), String.Format("TimelineEvent : {0} {1}", eventType, args));
            if (eventArgs != null)
            {
                var monsterName = eventArgs.First() as String;
                switch (eventType)
                {
                    case TimelineEventType.PartyJoin:
                    case TimelineEventType.PartyDisband:
                    case TimelineEventType.PartyLeave:
                        break;
                    case TimelineEventType.PartyMonsterFighting:
                    case TimelineEventType.AllianceMonsterFighting:
                        DeathFound = false;
                        if (monsterName != null && (monsterName.ToLower()
                                                               .Contains("target") || monsterName == ""))
                        {
                            break;
                        }
                        Fight fighting;
                        if (!Fights.TryGet(monsterName, out fighting))
                        {
                            fighting = new Fight(monsterName);
                            Fights.Add(fighting);
                        }
                        Controller.Timeline.LastEngaged = monsterName;
                        break;
                    case TimelineEventType.PartyMonsterKilled:
                    case TimelineEventType.AllianceMonsterKilled:
                        DeathFound = true;
                        if (monsterName != null && (monsterName.ToLower()
                                                               .Contains("target") || monsterName == ""))
                        {
                            break;
                        }
                        Fight killed;
                        if (!Fights.TryGet(monsterName, out killed))
                        {
                            killed = new Fight(monsterName);
                            Fights.Add(killed);
                        }
                        switch (eventType)
                        {
                            case TimelineEventType.PartyMonsterKilled:
                                GetSetMonster(monsterName, TimelineType.Party)
                                    .SetKill(killed);
                                break;
                            case TimelineEventType.AllianceMonsterKilled:
                                GetSetMonster(monsterName, TimelineType.Alliance)
                                    .SetKill(killed);
                                break;
                        }
                        Controller.Timeline.LastKilled = monsterName;
                        break;
                }
            }
            RaiseTimelineChangedEvent(this, new TimelineChangedEvent(eventType, eventArgs));
        }

        private void InitStats()
        {
            Overall.Stats.Clear();
            var overallStats = OverallStats()
                .Select(s => s.Value)
                .ToList();
            Overall.Stats.AddStats(overallStats);
        }

        private static Dictionary<string, Stat<decimal>> OverallStats()
        {
            var stats = new Dictionary<string, Stat<decimal>>();

            stats.Add("TotalOverallDamage", new TotalStat("TotalOverallDamage"));
            stats.Add("DPS", new PerSecondAverageStat("DPS", stats["TotalOverallDamage"]));
            stats.Add("StaticPlayerDPS", new TotalStat("StaticPlayerDPS"));
            stats.Add("TotalOverallHealing", new TotalStat("TotalOverallHealing"));
            stats.Add("HPS", new PerSecondAverageStat("HPS", stats["TotalOverallHealing"]));
            stats.Add("StaticPlayerHPS", new TotalStat("StaticPlayerHPS"));
            stats.Add("TotalOverallDamageTaken", new TotalStat("TotalOverallDamageTaken"));
            stats.Add("DTPS", new PerSecondAverageStat("DTPS", stats["TotalOverallDamageTaken"]));
            stats.Add("StaticPlayerDTPS", new TotalStat("StaticPlayerDTPS"));
            stats.Add("TotalOverallTP", new TotalStat("TotalOverallTP"));
            stats.Add("TotalOverallMP", new TotalStat("TotalOverallMP"));
            stats.Add("RegularDamage", new TotalStat("RegularDamage"));
            stats.Add("CriticalDamage", new TotalStat("CriticalDamage"));
            stats.Add("RegularHealing", new TotalStat("RegularHealing"));
            stats.Add("CriticalHealing", new TotalStat("CriticalHealing"));
            stats.Add("RegularDamageTaken", new TotalStat("RegularDamageTaken"));
            stats.Add("CriticalDamageTaken", new TotalStat("CriticalDamageTaken"));
            stats.Add("TotalOverallDamageMonster", new TotalStat("TotalOverallDamageMonster"));
            stats.Add("TotalOverallHealingMonster", new TotalStat("TotalOverallHealingMonster"));
            stats.Add("TotalOverallDamageTakenMonster", new TotalStat("TotalOverallDamageTakenMonster"));
            stats.Add("TotalOverallTPMonster", new TotalStat("TotalOverallTPMonster"));
            stats.Add("TotalOverallMPMonster", new TotalStat("TotalOverallMPMonster"));
            stats.Add("RegularDamageMonster", new TotalStat("RegularDamageMonster"));
            stats.Add("CriticalDamageMonster", new TotalStat("CriticalDamageMonster"));
            stats.Add("RegularHealingMonster", new TotalStat("RegularHealingMonster"));
            stats.Add("CriticalHealingMonster", new TotalStat("CriticalHealingMonster"));
            stats.Add("RegularDamageTakenMonster", new TotalStat("RegularDamageTakenMonster"));
            stats.Add("CriticalDamageTakenMonster", new TotalStat("CriticalDamageTakenMonster"));

            return stats;
        }

        /// <summary>
        /// </summary>
        public void Clear()
        {
            Fights.Clear();
            Overall.Clear();
            Party.Clear();
            Monster.Clear();
            InitStats();
        }

        #region Implementation of INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string caller = "")
        {
            PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }

        #endregion
    }
}