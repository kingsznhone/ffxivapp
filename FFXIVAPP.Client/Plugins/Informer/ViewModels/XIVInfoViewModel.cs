﻿// FFXIVAPP.Client
// XIVInfoViewModel.cs
// 
// © 2013 Ryan Wilson

#region Usings

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using FFXIVAPP.Client.Delegates;
using FFXIVAPP.Client.Memory;
using SmartAssembly.Attributes;

#endregion

namespace FFXIVAPP.Client.Plugins.Informer.ViewModels
{
    [DoNotObfuscate]
    public class XIVInfoViewModel : INotifyPropertyChanged
    {
        #region Property Bindings

        private static XIVInfoViewModel _instance;
        private IList<NPCEntry> _currentMonsters;
        private IList<NPCEntry> _currentNPCs;
        private NPCEntry _currentTarget;
        private NPCEntry _currentUser;

        public static XIVInfoViewModel Instance
        {
            get { return _instance ?? (_instance = new XIVInfoViewModel()); }
            set { _instance = value; }
        }

        public NPCEntry CurrentUser
        {
            get { return _currentUser ?? (_currentUser = new NPCEntry()); }
            set
            {
                _currentUser = value;
                RaisePropertyChanged();
            }
        }

        public NPCEntry CurrentTarget
        {
            get { return _currentTarget ?? (_currentTarget = new NPCEntry()); }
            set
            {
                _currentTarget = value;
                RaisePropertyChanged();
            }
        }

        public IList<NPCEntry> CurrentNPCs
        {
            get { return _currentNPCs ?? (_currentNPCs = new List<NPCEntry>()); }
            set
            {
                _currentNPCs = value;
                RaisePropertyChanged();
            }
        }

        public IList<NPCEntry> CurrentMonsters
        {
            get { return _currentMonsters ?? (_currentMonsters = new List<NPCEntry>()); }
            set
            {
                _currentMonsters = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Declarations

        public Timer InfoTimer = new Timer(100);

        #endregion

        public XIVInfoViewModel()
        {
            InfoTimer.Elapsed += InfoTimerOnElapsed;
            InfoTimer.Start();
        }

        private void InfoTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            CurrentUser = MonsterWorkerDelegate.CurrentUser;
            CurrentNPCs = NPCWorkerDelegate.NPCEntries;
            CurrentMonsters = MonsterWorkerDelegate.NPCEntries;
            if (CurrentUser.TargetID > 0)
            {
                if (NPCWorkerDelegate.NPCEntries.Any(n => n.NPCID1 == CurrentUser.TargetID))
                {
                    CurrentTarget = NPCWorkerDelegate.NPCEntries.FirstOrDefault(n => n.NPCID1 == CurrentUser.TargetID);
                }
                else if (MonsterWorkerDelegate.NPCEntries.Any(m => m.ID == CurrentUser.TargetID))
                {
                    CurrentTarget = MonsterWorkerDelegate.NPCEntries.FirstOrDefault(m => m.ID == CurrentUser.TargetID);
                }
            }
            else
            {
                CurrentTarget = new NPCEntry();
            }
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
