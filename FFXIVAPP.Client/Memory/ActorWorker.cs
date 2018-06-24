// FFXIVAPP.Client ~ ActorWorker.cs
// 
// Copyright © 2007 - 2017 Ryan Wilson - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using FFXIVAPP.Client.Helpers;
using FFXIVAPP.Client.Properties;
using NLog;
using Sharlayan;

namespace FFXIVAPP.Client.Memory
{
    internal class ActorWorker : INotifyPropertyChanged, IDisposable
    {
        #region Logger

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion

        public ActorWorker()
        {
            _scanTimer = new Timer(100);
            _scanTimer.Elapsed += ScanTimerElapsed;
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            _scanTimer.Elapsed -= ScanTimerElapsed;
        }

        #endregion

        #region Property Bindings

        public bool ReferencesSet { get; set; }
        public bool PCReferencesSet { get; set; }
        public bool NPCReferencesSet { get; set; }
        public bool MonsterReferencesSet { get; set; }

        #endregion

        #region Declarations

        private readonly Timer _scanTimer;
        private bool _isScanning;

        #endregion

        #region Timer Controls

        /// <summary>
        /// </summary>
        public void StartScanning()
        {
            _scanTimer.Enabled = true;
        }

        /// <summary>
        /// </summary>
        public void StopScanning()
        {
            _scanTimer.Enabled = false;
        }

        #endregion

        #region Threads

        public Stopwatch Stopwatch = new Stopwatch();

        /// <summary>
        /// </summary>
        /// <param name="sender"> </param>
        /// <param name="e"> </param>
        private void ScanTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isScanning)
            {
                return;
            }
            _isScanning = true;

            double refresh = 100;
            if (Double.TryParse(Settings.Default.ActorWorkerRefresh.ToString(CultureInfo.InvariantCulture), out refresh))
            {
                _scanTimer.Interval = refresh;
            }

            Func<bool> scanner = delegate
            {
                var readResult = Reader.GetActors();

                #region Notifications

                if (!MonsterReferencesSet && readResult.CurrentMonsters.Any())
                {
                    MonsterReferencesSet = true;
                    AppContextHelper.Instance.RaiseMonsterItemsUpdated(readResult.CurrentMonsters);
                }
                if (!NPCReferencesSet && readResult.CurrentNPCs.Any())
                {
                    NPCReferencesSet = true;
                    AppContextHelper.Instance.RaiseNPCItemsUpdated(readResult.CurrentNPCs);
                }
                if (!PCReferencesSet && readResult.CurrentPCs.Any())
                {
                    PCReferencesSet = true;
                    AppContextHelper.Instance.RaisePCItemsUpdated(readResult.CurrentPCs);
                }

                if (MonsterReferencesSet && NPCReferencesSet && PCReferencesSet)
                {
                    ReferencesSet = true;
                }

                if (readResult.NewMonsters.Any())
                {
                    AppContextHelper.Instance.RaiseMonsterItemsAdded(readResult.NewMonsters);
                }
                if (readResult.NewNPCs.Any())
                {
                    AppContextHelper.Instance.RaiseNPCItemsAdded(readResult.NewNPCs);
                }
                if (readResult.NewPCs.Any())
                {
                    AppContextHelper.Instance.RaisePCItemsAdded(readResult.NewPCs);
                }

                if (readResult.RemovedMonsters.Any())
                {
                    AppContextHelper.Instance.RaiseMonsterItemsRemoved(readResult.RemovedMonsters);
                }
                if (readResult.RemovedNPCs.Any())
                {
                    AppContextHelper.Instance.RaiseNPCItemsRemoved(readResult.RemovedNPCs);
                }
                if (readResult.RemovedPCs.Any())
                {
                    AppContextHelper.Instance.RaisePCItemsRemoved(readResult.RemovedPCs);
                }

                #endregion

                _isScanning = false;
                return true;
            };
            scanner.BeginInvoke(delegate { }, scanner);
        }

        #endregion

        #region Implementation of INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string caller = "")
        {
            PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }

        #endregion
    }
}
