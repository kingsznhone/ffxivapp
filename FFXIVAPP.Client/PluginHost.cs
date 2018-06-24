// FFXIVAPP.Client ~ PluginHost.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml.Linq;
using FFXIVAPP.Client.Helpers;
using FFXIVAPP.Client.Models;
using FFXIVAPP.Client.Reflection;
using FFXIVAPP.Common.Core.Constant;
using FFXIVAPP.Common.Core.Network;
using FFXIVAPP.Common.Models;
using FFXIVAPP.Common.Utilities;
using FFXIVAPP.IPluginInterface;
using FFXIVAPP.IPluginInterface.Events;
using NLog;
using Sharlayan.Core;

namespace FFXIVAPP.Client
{
    using Sharlayan.Core.Enums;

    internal class PluginHost : MarshalByRefObject, IPluginHost
    {
        #region Logger

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion

        #region Declarations

        public AssemblyReflectionManager AssemblyReflectionManager = new AssemblyReflectionManager();

        #endregion

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        public void LoadPlugins(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return;
            }
            try
            {
                if (Directory.Exists(path))
                {
                    var directories = Directory.GetDirectories(path);
                    foreach (var directory in directories)
                    {
                        LoadPlugin(directory);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, new LogItem(ex, true));
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        public void LoadPlugin(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return;
            }
            try
            {
                path = Directory.Exists(path) ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                var settings = $@"{path}\PluginInfo.xml";
                if (!File.Exists(settings))
                {
                    return;
                }
                var xDoc = XDocument.Load(settings);
                foreach (var xElement in xDoc.Descendants()
                                             .Elements("Main"))
                {
                    var xKey = (string) xElement.Attribute("Key");
                    var xValue = (string) xElement.Element("Value");
                    if (String.IsNullOrWhiteSpace(xKey) || String.IsNullOrWhiteSpace(xValue))
                    {
                        return;
                    }
                    switch (xKey)
                    {
                        case "FileName":
                            VerifyPlugin($@"{path}\{xValue}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, new LogItem(ex, true));
            }
        }

        /// <summary>
        /// </summary>
        public void UnloadPlugins()
        {
            foreach (var pluginInstance in Loaded.Cast<PluginInstance>()
                                                 .Where(pluginInstance => pluginInstance.Instance != null))
            {
                pluginInstance.Instance.Dispose();
            }
            Loaded.Clear();
        }

        /// <summary>
        /// </summary>
        public void UnloadPlugin(string name)
        {
            var plugin = Loaded.Find(name);
            if (plugin != null)
            {
                plugin.Instance.Dispose();
                Loaded.Remove(plugin);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyPath"></param>
        private void VerifyPlugin(string assemblyPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(assemblyPath);
                var pAssembly = Assembly.Load(bytes);
                var pType = pAssembly.GetType(pAssembly.GetName()
                                                       .Name + ".Plugin");
                var implementsIPlugin = typeof(IPlugin).IsAssignableFrom(pType);
                if (!implementsIPlugin)
                {
                    Logging.Log(Logger, $"*IPlugin Not Implemented* :: {pAssembly.GetName() .Name}");
                    return;
                }
                var plugin = new PluginInstance
                {
                    Instance = (IPlugin) Activator.CreateInstance(pType),
                    AssemblyPath = assemblyPath
                };
                plugin.Instance.Initialize(Instance);
                plugin.Loaded = true;
                Loaded.Add(plugin);
            }
            catch (Exception ex)
            {
                Logging.Log(Logger, new LogItem(ex, true));
            }
        }

        #region Property Bindings

        private PluginCollectionHelper _loaded;

        public PluginCollectionHelper Loaded
        {
            get { return _loaded ?? (_loaded = new PluginCollectionHelper()); }
            set
            {
                if (_loaded == null)
                {
                    _loaded = new PluginCollectionHelper();
                }
                _loaded = value;
            }
        }

        private static Lazy<PluginHost> _instance = new Lazy<PluginHost>(() => new PluginHost());

        public static PluginHost Instance
        {
            get { return _instance.Value; }
        }

        #endregion

        #region Implementaion of IPluginHost

        /// <summary>
        /// </summary>
        /// <param name="pluginName"></param>
        /// <param name="popupContent"></param>
        public void PopupMessage(string pluginName, PopupContent popupContent)
        {
            if (popupContent == null)
            {
                return;
            }
            var pluginInstance = App.Plugins.Loaded.Find(popupContent.PluginName);
            if (pluginInstance == null)
            {
                return;
            }
            var title = $"[{pluginName}] {popupContent.Title}";
            var message = popupContent.Message;
            Action cancelAction = null;
            if (popupContent.CanCancel)
            {
                cancelAction = delegate { pluginInstance.Instance.PopupResult = MessageBoxResult.Cancel; };
            }
            MessageBoxHelper.ShowMessageAsync(title, message, delegate { pluginInstance.Instance.PopupResult = MessageBoxResult.OK; }, cancelAction);
        }

        public event EventHandler<ActionContainersEvent> ActionContainersUpdated = delegate { };

        public event EventHandler<ChatLogItemEvent> ChatLogItemReceived = delegate { };

        public event EventHandler<ConstantsEntityEvent> ConstantsUpdated = delegate { };

        public event EventHandler<InventoryContainersEvent> InventoryContainersUpdated = delegate { };

        public event EventHandler<ActorItemsEvent> MonsterItemsUpdated = delegate { };

        public event EventHandler<ActorItemsAddedEvent> MonsterItemsAdded = delegate { };

        public event EventHandler<ActorItemsRemovedEvent> MonsterItemsRemoved = delegate { };

        public event EventHandler<NetworkPacketEvent> NetworkPacketReceived = delegate { };

        public event EventHandler<ActorItemsEvent> NPCItemsUpdated = delegate { };

        public event EventHandler<ActorItemsAddedEvent> NPCItemsAdded = delegate { };

        public event EventHandler<ActorItemsRemovedEvent> NPCItemsRemoved = delegate { };

        public event EventHandler<PartyMembersEvent> PartyMembersUpdated = delegate { };

        public event EventHandler<PartyMembersAddedEvent> PartyMembersAdded = delegate { };

        public event EventHandler<PartyMembersRemovedEvent> PartyMembersRemoved = delegate { };

        public event EventHandler<ActorItemsEvent> PCItemsUpdated = delegate { };

        public event EventHandler<ActorItemsAddedEvent> PCItemsAdded = delegate { };

        public event EventHandler<ActorItemsRemovedEvent> PCItemsRemoved = delegate { };

        public event EventHandler<CurrentPlayerEvent> CurrentPlayerUpdated = delegate { };

        public event EventHandler<TargetInfoEvent> TargetInfoUpdated = delegate { };

        public virtual void RaiseActionContainersUpdated(List<ActionContainer> actionContainers)
        {
            var raised = new ActionContainersEvent(this, actionContainers);
            var handler = this.ActionContainersUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseChatLogItemReceived(ChatLogItem chatLogItem)
        {
            var raised = new ChatLogItemEvent(this, chatLogItem);
            var handler = this.ChatLogItemReceived;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseConstantsUpdated(ConstantsEntity constantsEntity)
        {
            var raised = new ConstantsEntityEvent(this, constantsEntity);
            var handler = this.ConstantsUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseInventoryContainersUpdated(List<InventoryContainer> inventoryContainers)
        {
            var raised = new InventoryContainersEvent(this, inventoryContainers);
            var handler = this.InventoryContainersUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseMonsterItemsUpdated(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsEvent(this, actorItems);
            var handler = this.MonsterItemsUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseMonsterItemsAdded(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsAddedEvent(this, actorItems);
            var handler = this.MonsterItemsAdded;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseMonsterItemsRemoved(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsRemovedEvent(this, actorItems);
            var handler = this.MonsterItemsRemoved;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseNetworkPacketReceived(NetworkPacket networkPacket)
        {
            var raised = new NetworkPacketEvent(this, networkPacket);
            var handler = this.NetworkPacketReceived;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseNPCItemsUpdated(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsEvent(this, actorItems);
            var handler = this.NPCItemsUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseNPCItemsAdded(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsAddedEvent(this, actorItems);
            var handler = this.NPCItemsAdded;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseNPCItemsRemoved(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsRemovedEvent(this, actorItems);
            var handler = this.NPCItemsRemoved;
            handler?.Invoke(this, raised);
        }

        public virtual void RaisePartyMembersUpdated(ConcurrentDictionary<uint, PartyMember> partyMembers)
        {
            var raised = new PartyMembersEvent(this, partyMembers);
            var handler = this.PartyMembersUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaisePartyMembersAdded(ConcurrentDictionary<uint, PartyMember> partyMembers)
        {
            var raised = new PartyMembersAddedEvent(this, partyMembers);
            var handler = this.PartyMembersAdded;
            handler?.Invoke(this, raised);
        }

        public virtual void RaisePartyMembersRemoved(ConcurrentDictionary<uint, PartyMember> partyMembers)
        {
            var raised = new PartyMembersRemovedEvent(this, partyMembers);
            var handler = this.PartyMembersRemoved;
            handler?.Invoke(this, raised);
        }

        public virtual void RaisePCItemsUpdated(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsEvent(this, actorItems);
            var handler = this.PCItemsUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaisePCItemsAdded(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsAddedEvent(this, actorItems);
            var handler = this.PCItemsAdded;
            handler?.Invoke(this, raised);
        }

        public virtual void RaisePCItemsRemoved(ConcurrentDictionary<uint, ActorItem> actorItems)
        {
            var raised = new ActorItemsRemovedEvent(this, actorItems);
            var handler = this.PCItemsRemoved;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseCurrentPlayerUpdated(CurrentPlayer currentPlayer)
        {
            var raised = new CurrentPlayerEvent(this, currentPlayer);
            var handler = this.CurrentPlayerUpdated;
            handler?.Invoke(this, raised);
        }

        public virtual void RaiseTargetInfoUpdated(TargetInfo targetInfo)
        {
            var raised = new TargetInfoEvent(this, targetInfo);
            var handler = this.TargetInfoUpdated;
            handler?.Invoke(this, raised);
        }

        #endregion
    }
}
